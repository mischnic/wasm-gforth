: wasi_unstable-proc_exit
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
CREATE FN-INFOS 32 CELLS ALLOT \ fid -> pointer to [nbytes nlocals ...code] OR [0 (ptr to host function)]
0 VALUE N-IMPORTED
0 Value MEMORY-SIZE
0 Value MEMORY-PTR
-1 VALUE START-FN

: index-to-fid 
    N-IMPORTED + ;

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
            2 CELLS ALLOCATE throw
            dup FN-INFOS i cells + !
            0 over !
            ['] wasi_unstable-proc_exit swap cell+ !
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
        FN-TYPES i cells + !
    LOOP
    ;

: parse-section-code
    1 skip-bytes
    next-byte
    0 ?DO
        next-byte 1- \ 1 + bytes of code
        dup
        next-byte \ how many locals
        swap dup 2 + cells allocate throw
        dup FN-INFOS i index-to-fid cells + !
        -rot ( nbytes+1 addr nlocals nbytes+1)
        third ! \ store bytes
        over 1 cells + ! \ store locals
        2 cells + swap read-bytes
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
\ dup 3 cells + @ .
\ dup 4 cells + @ .
\ dup 5 cells + @ .
\ dup 6 cells + @ .

\ cr
\ TYPES 0 cells + 2@ . .
\ TYPES 2 cells + 2@ . .

\ cr
\ ." memory size " MEMORY-SIZE . cr
\ ." start " START-FN .

CREATE CODE-ADD
    2 ( nparams ) , 0 ( nlocals ) ,
    6 ( code bytes ) ,
    $20 , $01 , \ local.get 1
    $20 , $00 , \ local.get 0
    $6a , \ i32.add
    $0b , \ return
CREATE CODE-SUB
    2 ( nparams ) , 0 ( nlocals ) ,
    6 ( code bytes ) ,
    $20 , $01 , \ local.get 1
    $20 , $00 , \ local.get 0
    $6b , \ i32.sub
    $0b , \ return
CREATE CODE-MAIN
    0 ( nparams ) , 0 ( nlocals ) ,
    7 ( code bytes ) ,
    $41 , $8 , \ i32.const 8
    $41 , $9 , \ i32.const 9
    $10 , $1 , \ call 1=add, 2=sub
    \ $6a , \ i32.add
    $0b , \ return
CREATE CODE-MAINEXIT
    0 ( nparams ) , 0 ( nlocals ) ,
    5 ( code bytes ) ,
    $41 , $d , \ i32.const 13
    $10 , $0 , \ call 0
    $0b , \ return

CREATE CODES CODE-ADD , CODE-SUB , CODE-MAIN , CODE-MAINEXIT ,

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

: compile-function  ( arr -- )
    dup @
    swap cell + dup @
    rot swap
    2dup compile-function-prelude
    rot cell +
    compile-instructions
    compile-function-postlude 
    ;

\ ------------------ Initialization


\ CREATE CODE-temporary
\     2 ( nparams ) , 1 ( nlocals ) ,
\     11 ( code bytes ) ,
\     $20 , $01 , \ local.get 1
\     $20 , $00 , \ local.get 0
\     $6a , \ i32.add
\     $22 , $02 , \ local.tee 2
\     $20 , $02 , \ local.get 2
\     $6a , \ i32.add
\     $0b , \ return
\ : temporary [
\     CODE-temporary  compile-function
\ ] ;
\ see temporary cr
\ cr
\ 3 11 temporary .s
\ bye


:noname  \ wasi_unstable-proc_exit : n --
    (bye)
; 0 CELLS FUNCTIONS + !

\ TODO how to loop over all functions and create these definitions
:noname [ 
    CODES 0 cells + @
    compile-function
] ; 1 CELLS FUNCTIONS + !
:noname [
    CODES 1 cells + @
    compile-function
] ; 2 CELLS FUNCTIONS + !
:noname [
    CODES 2 cells + @
    compile-function
] ; 3 CELLS FUNCTIONS + !
:noname [
    CODES 3 cells + @
    compile-function
] ; 4 CELLS FUNCTIONS + !

FUNCTIONS 3 CELLS + @ EXECUTE
.s cr

bye
