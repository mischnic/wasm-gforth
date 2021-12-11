: wasi_unstable.proc_exit
    (bye) ;

\ ------------------ Parser

0 Value fd-in
: open-input ( addr u -- ) r/o open-file throw to fd-in ;
: close-input ( -- ) fd-in close-file throw ;
: next-byte ( -- n )
    fd-in key-file
    ;
: skip-bytes ( n -- )
    0 ?DO
        next-byte drop
    LOOP
    ;    
: read-bytes-packed ( addr n -- )
    fd-in read-line throw 2drop
    ;    
: read-bytes ( addr n -- )
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

: index-to-fid 
    COUNT-FN-IMPORTED + ;

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
        next-byte dup 
        IMPORT-READ-BUFFER swap read-bytes-packed
        IMPORT-READ-BUFFER swap
        s" wasi_unstable"
        compare invert
        next-byte dup 
        IMPORT-READ-BUFFER swap read-bytes-packed
        IMPORT-READ-BUFFER swap
        s" proc_exit"
        compare invert
        and
        IF
            3 CELLS ALLOCATE throw
            dup FN-INFOS i cells + !
            0 over !
            0 over cell+ !
            ['] wasi_unstable.proc_exit swap 2 cells + !
        ENDIF
        2 skip-bytes \ TODO other import types
    LOOP
    ;

: parse-section-memory
    1 skip-bytes
    next-byte 1 = IF
        next-byte 0 = IF 
            next-byte cells \ TODO cells or bytes?
            dup TO MEMORY-SIZE
            allocate throw TO MEMORY-PTR \ TODO fill with 0?
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
        swap dup 2 + cells allocate throw
        dup FN-INFOS i index-to-fid cells + !
        rot
        over !
        cell+
        2dup !
        cell+ swap read-bytes
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
    ( 11 ) ' noop ,

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

s" test/bare.wasm" open-input
parse-wasm
close-input

\ cr cr
\ FN-INFOS @
\ dup @ .
\ dup 1 cells + @ .
\ dup 2 cells + @ .
\ drop

\ cr cr
\ FN-INFOS cell+ @
\ dup @ .
\ dup 1 cells + @ .
\ dup 2 cells + @ .
\ dup 3 cells + @ .
\ drop

\ cr
\ TYPES 0 cells + 2@ . .
\ TYPES 2 cells + 2@ . .

\ cr
\ ." memory size " MEMORY-SIZE . cr
\ ." start " START-FN .

\ bye

\ ------------------ Compiler

CREATE FUNCTIONS 32 ALLOT

: compile-load-local ( n -- )
    POSTPONE [ POSTPONE @local# CELLS , \ not sure why ] causes an error here, works anyway
 ;

: compile-store-local ( n -- )
    POSTPONE [ POSTPONE laddr# CELLS , \ not sure why ] causes an error here, works anyway
 ;

: compile-instruction ( ptrNext1 -- ptrNext2 )
    dup @
    CASE
        $41 OF \ i32.const [lit] : -- lit
            cell + dup @
            POSTPONE LITERAL
        ENDOF
        $6a OF \ i32.add : a b -- c
            POSTPONE + \ TODO truncate
        ENDOF
        $6b OF \ i32.sub : a b -- c
            POSTPONE - \ TODO truncate
        ENDOF
        $10 OF \ call [idx] : --
            cell + dup @ cells FUNCTIONS +
            POSTPONE LITERAL POSTPONE @ POSTPONE EXECUTE
        ENDOF
        $20 OF \ local.get [lit] : -- v
            cell + dup @
            compile-load-local
        ENDOF
        $21 OF \ local.set [lit] : v --
            cell + dup @
            compile-store-local POSTPONE !
        ENDOF
        $22 OF \ local.tee [lit] : v --
            POSTPONE dup
            cell + dup @
            compile-store-local POSTPONE !
        ENDOF
        \ $36 OF \ i32.store : addr v --
            \ l!
        \ ENDOF
        \ $28 OF \ i32.load : addr -- v
            \ ul@
        \ ENDOF
        $0b OF \ return
            POSTPONE EXIT
        ENDOF
        ( n ) ." unhandled " . ( n )
    ENDCASE
    cell +
    ;

: compile-instructions  ( arr -- )
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

: compile-function  ( nparams arr -- )
    dup @
    rot swap
    ( arr nparams nlocals )
    2dup compile-function-prelude
    rot cell+
    compile-instructions
    compile-function-postlude 
    ; IMMEDIATE

\ ------------------ Initialization


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


: create-function ( nparams arr -- xt )
    \ use return stack to smuggle data around the stack elements pushed by :noname
    >r >r
    :noname r> r> POSTPONE compile-function POSTPONE ;
    ;

: compile-functions
    \ 4 1 ?DO
    COUNT-FN 0 ?DO
        FN-INFOS i CELLS + @ \ pointer to [nlocals nbytes ...code] OR [0 0 (ptr to host function)]
        dup @ \ nlocals
        over cell+ @ \ nbytes
        + 0 = IF
            \ host function
            2 cells + @
            \ i . dup xt-see cr
            FUNCTIONS i CELLS + !
        ELSE
            \ compile
            FN-TYPES i CELLS + @ ( arr tid )
            2 * CELLS cell+ TYPES + @ ( arr nparams )
            swap
            create-function
            \ i . dup xt-see cr
            FUNCTIONS i CELLS + !
        ENDIF
    LOOP
;

compile-functions

\ :noname [ 
\     CODES 0 cells + @
\     compile-function
\ ] ; 1 CELLS FUNCTIONS + !
\ :noname [
\     CODES 1 cells + @
\     compile-function
\ ] ; 2 CELLS FUNCTIONS + !
\ :noname [
\     CODES 2 cells + @
\     compile-function
\ ] ; 3 CELLS FUNCTIONS + !
\ :noname [
\     CODES 3 cells + @
\     compile-function
\ ] ; 4 CELLS FUNCTIONS + !


FUNCTIONS START-FN CELLS + @ EXECUTE
\ FUNCTIONS 0 CELLS + @ EXECUTE

\ FUNCTIONS 0 CELLS + @ xt-see
\ FUNCTIONS 1 CELLS + @ xt-see
\ .s cr

bye
