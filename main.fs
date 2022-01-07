require leb128.fs
require parser.fs
require cs-stack.fs

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

: compile-instruction ( ..cs-items.. c-addr1 type-stack cs-stack -- ..cs-items.. c-addr2 )
    { c-addr type-stack cs-stack }
    c-addr consume-byte
    CASE
        $01 OF \ nop
        ENDOF
        $02 OF \ block
            consume-leb128 drop \ ignore blocktype
            TYPE-BLOCK type-stack stack.push
        ENDOF
        $03 OF \ loop
            consume-leb128 drop \ ignore blocktype
            TO c-addr
                postpone begin
            c-addr
            TYPE-LOOP type-stack stack.push 
            type-stack stack.size 1- cs-stack cs-stack.push-loop-begin
        ENDOF
        $04 OF \ if
            consume-leb128 drop \ ignore blocktype
            TO c-addr
                postpone 0<>
                postpone if
            c-addr
            cs-stack cs-stack.push-ignore
            TYPE-IFELSE type-stack stack.push
        ENDOF
        $05 OF \ else
            TO c-addr
                type-stack cs-stack gen-end
                cs-stack cs-stack.pop-index-ignore cs-roll
                postpone ahead
                1 cs-roll
                postpone endif
                cs-stack cs-stack.push-ignore
                TYPE-IFELSE type-stack stack.push
            c-addr
        ENDOF
        $0b OF \ end : --
            TO c-addr
                type-stack stack.head TYPE-IFELSE = IF
                    cs-stack cs-stack.pop-index-ignore cs-roll postpone endif
                ENDIF
                type-stack cs-stack gen-end
            c-addr
        ENDOF
        $0c OF \ br [lit] : --
            consume-leb128 swap
            TO c-addr
                type-stack cs-stack gen-br
            c-addr
        ENDOF
        $0D OF \ br_if [lit] : v --
            consume-leb128 swap
            TO c-addr
                type-stack cs-stack gen-br_if
            c-addr
        ENDOF
        \ $0D OF \ br_table
        \ ENDOF
        $0F OF \ return
            TO c-addr
                type-stack stack.size 1- 
                type-stack cs-stack gen-br
            c-addr
        ENDOF
        $10 OF \ call [idx] : --
            consume-uleb128 cells FUNCTIONS +
            POSTPONE LITERAL POSTPONE @ POSTPONE EXECUTE
        ENDOF
        \ $11 OF \ call_indirect
        \ ENDOF

        $1A OF \ drop : v --
            POSTPONE drop
        ENDOF
        $41 OF \ i32.const [lit] : -- lit
            consume-leb128
            POSTPONE LITERAL
        ENDOF
        $42 OF \ i64.const [lit] : -- lit
            consume-leb128
            POSTPONE LITERAL
        ENDOF
        $46 OF \ i32.eq : a b -- c
            POSTPONE = POSTPONE 1 POSTPONE AND
        ENDOF
        $47 OF \ i32.ne : a b -- c
            POSTPONE <> POSTPONE 1 POSTPONE AND
        ENDOF
        $51 OF \ i64.eq : a b -- c
            POSTPONE = POSTPONE 1 POSTPONE AND
        ENDOF
        $52 OF \ i64.ne : a b -- c
            POSTPONE <> POSTPONE 1 POSTPONE AND
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
        $7C OF \ i64.add : a b -- c
            POSTPONE +
        ENDOF
        $7D OF \ i64.sub : a b -- c
            POSTPONE -
        ENDOF
        $7E OF \ i64.mul : a b -- c
            POSTPONE *
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
        $2D OF \ i32.load8_u : addr -- v
            compile-apply-memarg
            POSTPONE c@
        ENDOF
        ( n ) ." unhandled instruction " hex. bye
    ENDCASE
    ;

: compile-instructions  ( a-addr -- )
    dup dup @ chars + cell+ swap
    cell+
    stack.new
    stack.new
    { end pos type-stack cs-stack }
    TYPE-BLOCK type-stack stack.push \ function is a block
    BEGIN
        pos type-stack cs-stack 
        compile-instruction
        TO pos
        end pos <=
    UNTIL
    \ TODO function "end" is optional?
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

    \ "quotations" appear to be the indetended mechanism for this, but dynamically generating code doesn't work
    \ [: [ POSTPONE . ] ;]
    ;

\ ------------------ Initialization


\ CREATE CODE-temporary
\     0 , \ 0 local
\     21 , \ 6 bytes code
\     \ $02 c, $40 c, \ block
\         $02 c, $40 c, \ block
\             $41 c, $00 c, \ i32.const
\             $0d c, $01 c, \ br_if 0
\             $41 c, $05 c, \ i32.const 5
\             $0c c, $01 c, \ br 1
\         $0b c, \ end
\         $41 c, $04 c, \ i32.const 4
\         $41 c, $01 c, \ i32.const
\         $0d c, $00 c, \ br_if 0
\         $41 c, $01 c, \ i32.const 1
\         $6a c, \ i32.add
\     \ $0b c, \ end
\     $0b c, \ end
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
\ temporary . cr
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

:noname 
    START-FN -1 = IF
        ." No start function found"
        1 (bye)
    ENDIF
; EXECUTE

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
