\ --------- Parser

CREATE CODE-ADD
    2 ,
    $6a , \ i32.add
    $0b , \ return
CREATE CODE-SUB
    2 ,
    $6b , \ i32.sub
    $0b , \ return
CREATE CODE-MAIN
    7 ,
    $41 , $8 , \ i32.const 8
    $41 , $9 , \ i32.const 9
    $10 , $1 , \ call 0
    \ $6a , \ i32.add
    $0b , \ return
CREATE CODES CODE-ADD , CODE-SUB , CODE-MAIN ,

\ --------- Compiler

CREATE FUNCTIONS 32 ALLOT

: compile-instruction, ( ptr -- ptr )
    dup @
    CASE
        $41 OF \ i32.const [lit] : -- a
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
        $0b OF \ return
            POSTPONE EXIT
        ENDOF
        ( n ) ." unhandled " . ( n )
    ENDCASE
    cell +
    ;

: compile-function  ( arr -- )
    dup dup @ cells + swap
    cell +
    ( end start )
    BEGIN 
        compile-instruction,
        ( end pos )
        2dup <=
    UNTIL
    drop drop
;

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


\ : temporary [
\     CODE-MAIN
\     compile-function
\ ] ;
\ see temporary cr

\ --------- Initialization

FUNCTIONS 3 CELLS + @ EXECUTE
.s

bye
