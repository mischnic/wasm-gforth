\ ------------------ Parser

0 Value fd-in
: open-input ( c-addr u -- ) r/o open-file throw to fd-in ;
: close-input ( -- ) fd-in close-file throw ;
: next-byte ( -- n )
    fd-in key-file
    ;
: skip-bytes ( n -- )
    0 ?DO
        next-byte drop
    LOOP
    ;    
: read-bytes-packed ( c-addr n -- )
    \ read-line sometimes corrups the last byte?
    \ fd-in read-line throw 2drop
    over + swap +DO
        next-byte i !
    LOOP
    ;    
: read-bytes ( a-addr n -- )
    cells over + swap +DO
        next-byte i !
    cell +LOOP
    ;    

CREATE TYPES 32 2 * CELLS ALLOT \ tid*2 -> [nreturn nparams]
CREATE FN-TYPES 32 CELLS ALLOT \ fid -> tid
CREATE FN-INFOS 32 CELLS ALLOT \ fid -> pointer to [nlocals nbytes ...code] OR [0 0 (ptr to host function)]
0 VALUE COUNT-FN
0 VALUE COUNT-FN-IMPORTED
0 Value MEMORY-SIZE
0 Value MEMORY-PTR
-1 VALUE START-FN

: index-to-fid  ( n -- n )
    COUNT-FN-IMPORTED + ;

: ptr-to-real-addr ( ptr -- c-addr )
    MEMORY-PTR +
    ;

: wasi_unstable.proc_exit ( n -- )
    (bye) ;

: wasi_unstable.fd_write { fd ptr n addr-nwritten -- nwritten }
    fd 1 <> IF ." fd_write: unsupported fd" bye ENDIF
    n 0 ?DO
        ptr ptr-to-real-addr
        dup ul@ ptr-to-real-addr \ c-addr
        swap 4 + ul@ \ u
        type
    LOOP
    0
    ;

: parse-section-noop
    next-byte skip-bytes
    ;

: parse-section-type
    1 skip-bytes
    next-byte \ number of types
    0 ?DO
        1 skip-bytes \ TODO handle other types apart from 60=function?
        next-byte \ numbers of params
        dup skip-bytes
        next-byte \ numbers of return values
        dup skip-bytes
        TYPES i 2 * cells + 2!
    LOOP
    ;

CREATE IMPORT-READ-BUFFER 128 ALLOT
: parse-section-imports
    1 skip-bytes
    next-byte \ number of imports
    dup TO COUNT-FN-IMPORTED
    0 ?DO
        next-byte dup  IMPORT-READ-BUFFER swap read-bytes-packed
        IMPORT-READ-BUFFER swap
        s" wasi_unstable" compare
        IF ." import from unknown module" bye ENDIF

        3 CELLS ALLOCATE throw
        dup FN-INFOS i cells + !
        0 over !
        0 over cell+ !
        2 cells +
        CASE
            next-byte dup  IMPORT-READ-BUFFER swap read-bytes-packed
            IMPORT-READ-BUFFER swap
            2dup s" proc_exit" compare invert ?OF
                2drop
                ['] wasi_unstable.proc_exit
            ENDOF
            2dup s" fd_write" compare invert ?OF
                2drop
                ['] wasi_unstable.fd_write
            ENDOF
            ." unknown import name" bye
        ENDCASE
        swap !
        2 skip-bytes \ TODO other import types
    LOOP
    ;

: parse-section-memory
    1 skip-bytes
    next-byte 1 = IF
        next-byte 0 = IF 
            next-byte
            dup TO MEMORY-SIZE
            allocate throw TO MEMORY-PTR \ TODO fill with 0
        ENDIF
    ENDIF
    ;

: parse-section-start
    1 skip-bytes
    next-byte TO START-FN
    ;

: parse-section-functions
    1 skip-bytes
    next-byte
    0 ?DO
        next-byte
        FN-TYPES i index-to-fid cells + !
    LOOP
    ;

: parse-section-code
    1 skip-bytes
    next-byte \ number of functions
    dup COUNT-FN-IMPORTED + TO COUNT-FN
    0 ?DO
        next-byte 1- \ bytes of code
        next-byte \ how many locals
        dup 2 * skip-bytes \ skip descriptor TODO there might be more possibilites
        swap over 2 * -
        dup 2 + cells allocate throw
        dup FN-INFOS i index-to-fid cells + !
        rot
        over !
        cell+
        2dup !
        cell+ swap read-bytes
    LOOP
    ;

: parse-section-data
    1 skip-bytes
    next-byte \ number of segments
    0 ?DO
        1 skip-bytes \ type TODO there could be more, assume `00`
        1 skip-bytes next-byte 1 skip-bytes \ offset TODO interpret instead of assuming `i32.const X end`
        ptr-to-real-addr
        next-byte ( target length )
        read-bytes-packed
    LOOP
    ;


CREATE SECTION-HANDLERS
    ( 0 ) ' noop ,
    ( 1 ) ' parse-section-type ,
    ( 2 ) ' parse-section-imports ,
    ( 3 ) ' parse-section-functions ,
    ( 4 ) ' parse-section-noop ,
    ( 5 ) ' parse-section-memory ,
    ( 6 ) ' parse-section-noop ,
    ( 7 ) ' parse-section-noop ,
    ( 8 ) ' parse-section-start ,
    ( 9 ) ' parse-section-noop ,
    ( 10 ) ' parse-section-code ,
    ( 11 ) ' parse-section-data ,

: parse-wasm 
    8 skip-bytes
    BEGIN
        next-byte dup -1 <>
    WHILE
        \ dup .
        11 min
        CELLS SECTION-HANDLERS + @ EXECUTE
    REPEAT
    drop
;

\ ------------------ Compiler

CREATE FUNCTIONS 32 ALLOT

: compile-load-local ( n -- )
    POSTPONE [ POSTPONE @local# CELLS , \ not sure why ] causes an error here, works anyway
 ;

: compile-store-local ( n -- )
    POSTPONE [ POSTPONE laddr# CELLS , \ not sure why ] causes an error here, works anyway
 ;

: read-next-cell 
    cell+ dup @
;

: compile-apply-memarg ( ptr -- ptr )
    cell+ \ ignore alignment
    read-next-cell \ offset
    POSTPONE LITERAL POSTPONE +
    POSTPONE MEMORY-PTR POSTPONE +
    ;

: compile-truncate-i32
    POSTPONE $ffffffff POSTPONE AND
    ;

: compile-instruction ( ptrNext1 -- ptrNext2 )
    dup @
    CASE
        $1A OF \ drop : v --
            POSTPONE drop
        ENDOF
        $41 OF \ i32.const [lit] : -- lit
            read-next-cell
            POSTPONE LITERAL
        ENDOF
        $6a OF \ i32.add : a b -- c
            POSTPONE + \ TODO compile-truncate-i32
        ENDOF
        $6b OF \ i32.sub : a b -- c
            POSTPONE - \ TODO compile-truncate-i32
        ENDOF
        $46 OF \ i32.eq : a b -- c
            POSTPONE = POSTPONE 1 POSTPONE AND
        ENDOF
        $10 OF \ call [idx] : --
            read-next-cell cells FUNCTIONS +
            POSTPONE LITERAL POSTPONE @ POSTPONE EXECUTE
        ENDOF
        $20 OF \ local.get [lit] : -- v
            read-next-cell
            compile-load-local
        ENDOF
        $21 OF \ local.set [lit] : v --
            read-next-cell
            compile-store-local POSTPONE !
        ENDOF
        $22 OF \ local.tee [lit] : v --
            POSTPONE dup
            read-next-cell
            compile-store-local POSTPONE !
        ENDOF
        $36 OF \ i32.store : addr v --
            POSTPONE swap
            compile-apply-memarg
            POSTPONE l!
        ENDOF
        $28 OF \ i32.load : addr -- v
            compile-apply-memarg
            POSTPONE ul@
        ENDOF
        $0b OF \ return
            \ TODO might need unloop/done
            POSTPONE EXIT
        ENDOF
        ( n ) ." unhandled instruction " hex. bye
    ENDCASE
    cell +
    ;

: compile-instructions  ( a-addr -- )
    dup dup @ cells + swap
    cell +
    ( end start )
    BEGIN 
        compile-instruction
        ( end nextpos )
        2dup <=
    UNTIL
    drop drop
;

: compile-function-prelude ( nparams nlocals -- )
    0 ?DO
        POSTPONE lp-
    LOOP
    0 ?DO
        POSTPONE >l
    LOOP
    ;

: compile-function-postlude ( nparams nlocals -- )
    +
    0 ?DO
        POSTPONE lp+
    LOOP
    ;

: compile-function  ( nparams a-addr -- )
    dup @
    rot swap
    ( a-addr nparams nlocals )
    2dup compile-function-prelude
    rot cell+
    compile-instructions
    compile-function-postlude 
    ; IMMEDIATE


: create-function ( nparams a-addr -- xt )
    \ use return stack to smuggle data around the stack elements pushed by :noname
    >r >r
    :noname r> r> POSTPONE compile-function POSTPONE ;
    ;

\ ------------------ Initialization


\ CREATE CODE-temporary
\     0 , \ 0 local
\     6 , \ 11 bytes code
\     $20 , $01 , \ local.get 1
\     $41 , $00 , \ i32.const 0
\     $36 , $02 , $00 , \ i32.store align=2 offset=0
\     $0b , \ return
\ : temporary [
\     2 CODE-temporary compile-function
\ ] ;
\ see temporary cr
\ bye


\ CREATE CODE-temporary
\     1 , \ 1 local
\     11 , \ 11 bytes code
\     $20 , $01 , \ local.get 1
\     $20 , $00 , \ local.get 0
\     $6a , \ i32.add
\     $22 , $02 , \ local.tee 2
\     $20 , $02 , \ local.get 2
\     $6a , \ i32.add
\     $0b , \ return
\ : temporary [
\     2 CODE-temporary compile-function
\ ] ;
\ see temporary cr
\ cr
\ 3 11 temporary .s  \ (3 + 11) + (3 + 11)
\ bye

: get-arg ( -- a-addr u )
    next-arg
    dup 0 = IF 
        ." You need to specify a wasm file to run"
        1 (bye)
    ENDIF
    ;

get-arg open-input
parse-wasm
close-input

\ cr cr
\ FN-INFOS cell+ @
\ dup @ .
\ dup 1 cells + @ .
\ dup 2 cells + @ .
\ dup 3 cells + @ .
\ drop
\ bye

: compile-functions
    COUNT-FN 0 ?DO
        FN-INFOS i CELLS + @ \ pointer to [nlocals nbytes ...code] OR [0 0 (ptr to host function)]
        dup @ \ nlocals
        over cell+ @ \ nbytes
        + 0 = IF \ host function
            2 cells + @
            \ i . dup xt-see cr
            FUNCTIONS i CELLS + !
        ELSE \ compile
            FN-TYPES i CELLS + @ ( a-addr tid )
            2 * CELLS cell+ TYPES + @ ( a-addr nparams )
            swap
            create-function
            \ i . dup xt-see cr
            FUNCTIONS i CELLS + !
        ENDIF
    LOOP
;

compile-functions
FUNCTIONS START-FN CELLS + @ EXECUTE

\ FUNCTIONS 0 CELLS + @ xt-see
\ .s cr

bye
