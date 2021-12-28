require leb128.fs
require parser.fs

\ ------------------ Compiler

CREATE FUNCTIONS 32 ALLOT

: compile-load-local ( n -- )
    POSTPONE [ POSTPONE @local# CELLS , \ not sure why ] causes an error here, works anyway
 ;

: compile-store-local ( n -- )
    POSTPONE [ POSTPONE laddr# CELLS , \ not sure why ] causes an error here, works anyway
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

: consume-byte ( c-addr -- c-addr n )
    dup char+ swap c@
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
            \ TODO regardless of early or regular exit, the locals need to be popped
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
    \ use return stack to smuggle the params around the stack elements pushed by :noname
    >r >r
    :noname r> r> POSTPONE compile-function POSTPONE ;
    ;

\ ------------------ Initialization


\ CREATE CODE-temporary
\     0 , \ 0 locals
\     8 , \ 8 bytes code
\     $20 c, $01 c, \ local.get 1
\     $41 c, $00 c, \ i32.const 0
\     $36 c, $02 c, $00 c, \ i32.store align=2 offset=0
\     $0b c, \ return
\ : temporary [
\     0 CODE-temporary compile-function
\ ] ;
\ see temporary cr
\ bye


\ CREATE CODE-temporary
\     1 , \ 1 local
\     11 , \ 11 bytes code
\     $20 c, $01 c, \ local.get 1
\     $20 c, $00 c, \ local.get 0
\     $6a c, \ i32.add
\     $22 c, $02 c, \ local.tee 2
\     $20 c, $02 c, \ local.get 2
\     $6a c, \ i32.add
\     $0b c, \ return
\ : temporary [
\     2 CODE-temporary compile-function
\ ] ;
\ see temporary cr
\ cr
\ 3 11 temporary .s  \ 11 3 + dup + = 28
\ bye

: get-arg ( -- a-addr u )
    next-arg
    dup 0 = IF 
        ." No wasm file specified"
        1 (bye)
    ENDIF
    ;

get-arg open-input
parse-wasm
close-input

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

bye
