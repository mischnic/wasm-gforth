\ ------------------ Parser

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
CREATE CODES CODE-ADD , CODE-SUB , CODE-MAIN ,

\ ------------------ Compiler

CREATE FUNCTIONS 32 ALLOT

: compile-load-local ( n -- )
    POSTPONE [ POSTPONE @local# CELLS , \ not sure why ] causes an error here, works anyway
 ;

: compile-instruction ( ptrNext1 -- ptrNext2 )
    dup @
    CASE
        $41 OF \ i32.const [lit] : -- lit
            cell + dup @
            POSTPONE LITERAL
        ENDOF
        $6a OF \ i32.add : a b -- c
            POSTPONE +
        ENDOF
        $6b OF \ i32.sub : a b -- c
            POSTPONE -
        ENDOF
        $10 OF \ call [idx] : --
            cell + dup @ cells FUNCTIONS +
            POSTPONE LITERAL POSTPONE @ POSTPONE EXECUTE
        ENDOF
        $20 OF \ local.get [lit] : -- v;
            cell + dup @
            compile-load-local
        ENDOF
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
\     2 ( nparams ) , 0 ( nlocals ) ,
\     6 ( code bytes ) ,
\     $20 , $01 , \ local.get 1
\     $20 , $00 , \ local.get 0
\     $6a , \ i32.add
\     $0b , \ return
\ : temporary [
\     CODE-temporary  compile-function
\ ] ;
\ see temporary cr
\ 3 11 temporary .s
\ bye


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


FUNCTIONS 3 CELLS + @ EXECUTE
.s

bye
