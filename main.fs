: check-clear-byte-msb { n1 -- n2 msb }
    n1 %01111111 and
    n1 %10000000 and 0<>
    ;

: consume-uleb128-base { c-addr -- i u }
    0 0 ( result i )
    BEGIN
        dup chars c-addr + c@ check-clear-byte-msb ( result i byte should-continue )
        >r
        over 7 * lshift
        rot + swap
        1+
        r>
        WHILE
    REPEAT
    ;

: consume-uleb128 { c-addr -- c-addr2 u }
    c-addr consume-uleb128-base
    chars c-addr + swap
    ;

: consume-leb128 { c-addr -- c-addr2 s }
    c-addr consume-uleb128-base
    dup chars c-addr + -rot
    7 * 1-
    -1 swap lshift ( c-addr2 result mask )
    \ sign-extend the 7*i-bit number to 64 bit
    \ if any bit in the mask is set, set all of them
    2dup and 0<> IF
        or
    ELSE
        drop
    ENDIF
    ;


\ CREATE EXAMPLE1 1 ALLOT \ 624485 = $98765
\ $E5 EXAMPLE1 c!
\ $8E EXAMPLE1 1 chars + c!
\ $26 EXAMPLE1 2 chars + c!

\ CREATE EXAMPLE1 1 ALLOT \ 234
\ $EA EXAMPLE1 c!
\ $01 EXAMPLE1 1 chars + c!

\ CREATE EXAMPLE1 1 ALLOT \ 7
\ $07 EXAMPLE1 c!

\ CREATE EXAMPLE1 1 ALLOT \ 234
\ $0 EXAMPLE1 c!

\ CREATE EXAMPLE1 1 ALLOT \ -123456 = $FFFFFFFFFFFE1DC0
\ $C0 EXAMPLE1 c!
\ $BB EXAMPLE1 1 chars + c!
\ $78 EXAMPLE1 2 chars + c!

\ EXAMPLE1 consume-uleb128 . drop cr
\ EXAMPLE1 consume-leb128 . drop cr
\ \ EXAMPLE1 1 chars + .

\ bye


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
CREATE FN-INFOS 32 CELLS ALLOT \ fid -> pointer to [nlocals nbytes ...packed-code] OR [0 0 (ptr to host function)]
0 VALUE COUNT-FN
0 VALUE COUNT-FN-IMPORTED
0 Value MEMORY-SIZE
0 Value MEMORY-PTR
0 Value GLOBALS-PTR
-1 VALUE START-FN

: index-to-fid  ( u -- u )
    COUNT-FN-IMPORTED + ;

: ptr-to-real-addr ( ptr -- c-addr )
    MEMORY-PTR +
    ;

: wasi_unstable.proc_exit ( s -- )
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
            allocate throw TO MEMORY-PTR
            MEMORY-PTR MEMORY-SIZE erase \ TODO use chars?
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
        dup chars 2 cells + allocate throw
        dup FN-INFOS i index-to-fid cells + !
        rot
        over !
        cell+
        2dup !
        cell+ swap read-bytes-packed
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

: parse-section-global
    1 skip-bytes
    next-byte
    dup cells allocate throw TO GLOBALS-PTR
    0 ?DO
        1 skip-bytes \ TODO there could be more, assume `7f` = i32
        1 skip-bytes \ ignore info about mutability
        1 skip-bytes next-byte 1 skip-bytes \ initial value TODO interpret instead of assuming `i32.const X end`
        i cells GLOBALS-PTR + !
    LOOP
    ;

CREATE SECTION-HANDLERS
    ( 0 ) ' noop ,
    ( 1 ) ' parse-section-type ,
    ( 2 ) ' parse-section-imports ,
    ( 3 ) ' parse-section-functions ,
    ( 4 ) ' parse-section-noop ,
    ( 5 ) ' parse-section-memory ,
    ( 6 ) ' parse-section-global ,
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

: consume-byte ( c-addr -- c-addr n )
    dup char+ swap c@
;

: compile-apply-memarg ( c-addr -- ptr )
    char+ \ ignore alignment
    consume-uleb128 \ offset
    POSTPONE LITERAL POSTPONE +
    POSTPONE MEMORY-PTR POSTPONE + \ don't compile the value of MEMORY_PTR because it can change
    ;

: truncate-i32 ( n -- n )
    $ffffffff AND
    ;

: compile-instruction ( c-addr -- c-addr )
    consume-byte
    CASE
        $1A OF \ drop : v --
            POSTPONE drop
        ENDOF
        $41 OF \ i32.const [lit] : -- lit
            consume-leb128
            POSTPONE LITERAL
        ENDOF
        $6a OF \ i32.add : a b -- c
            POSTPONE + POSTPONE truncate-i32
        ENDOF
        $6b OF \ i32.sub : a b -- c
            POSTPONE - POSTPONE truncate-i32
        ENDOF
        $6c OF \ i32.mul : a b -- c
            POSTPONE * POSTPONE truncate-i32
        ENDOF
        $74 OF \ i32.shl : a n -- b
            POSTPONE lshift POSTPONE truncate-i32
        ENDOF
        $76 OF \ i32.shr_u : a n -- b
            POSTPONE rshift POSTPONE truncate-i32
        ENDOF
        $46 OF \ i32.eq : a b -- c
            POSTPONE = POSTPONE 1 POSTPONE AND
        ENDOF
        $10 OF \ call [idx] : --
            consume-uleb128 cells FUNCTIONS +
            POSTPONE LITERAL POSTPONE @ POSTPONE EXECUTE
        ENDOF
        $20 OF \ local.get [lit] : -- v
            consume-uleb128
            compile-load-local
        ENDOF
        $21 OF \ local.set [lit] : v --
            consume-uleb128
            compile-store-local POSTPONE !
        ENDOF
        $22 OF \ local.tee [lit] : v --
            POSTPONE dup
            consume-uleb128
            compile-store-local POSTPONE !
        ENDOF
        $23 OF \ global.get : [lit] -- v
            consume-uleb128 cells GLOBALS-PTR + POSTPONE LITERAL \ GLOBALS-PTR is constant
            POSTPONE @
        ENDOF
        $24 OF \ global.set : [lit] v --
            consume-uleb128 cells GLOBALS-PTR + POSTPONE LITERAL \ GLOBALS-PTR is constant
            POSTPONE !
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
            \ TODO distinguish endif/return/repeat/endblock
            POSTPONE EXIT
        ENDOF
        ( n ) ." unhandled instruction " hex. bye
    ENDCASE
    ;

: compile-instructions  ( a-addr -- )
    dup dup @ chars + cell + swap
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
