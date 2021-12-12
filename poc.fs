\ : locals-test ( n n -- n )
\     lp- >l >l
\     @local# [ 0 cells , ] \ locals.get 0
\     @local# [ 1 cells , ] \ locals.get 1
\     + \ i32.add
\     dup laddr# [ 2 cells , ] ! \ locals.tee 2
\     @local# [ 2 cells , ] \ locals.get 2
\     + \ i32.add
\     lp+ lp+ lp+
\     ;
\ 1 2 locals-test .s


(
    block
        ...
        br_if \ conditional jmp after end
        ...
        br_if \ conditional jmp after end
        ...
    end
    ---
        ...
        if
        ...
    endif
)
(
    loop
        ...
        br_if \ conditional jmp to start end
        ...
        br \ jmp to start end
        ...
    end
    ---
)
(
    if \ take condition
        ...
    else 
        ...
    end
    ---
)
(
    block 
        ...
        block
            ...
            local.get $p
            br_table
                2   ;; xp == 0 => br 2
                1   ;; p == 1 => br 1
                0   ;; p == 2 => br 0
                3 ;; else => br 3
        end
    end                
)

\ : startblock BEGIN ; IMMEDIATE
\ : endblock POSTPONE ENDIF ; IMMEDIATE
\ : br_if ( levels -- ) 
\     drop
\     \ POSTPONE >r
\     \ 0 ?DO 
\     \     POSTPONE CS-DROP
\     \ LOOP
\     \ POSTPONE r>
\     POSTPONE INVERT POSTPONE IF
\     ; IMMEDIATE

\ : run
\     startblock
\         startblock
\             true [ 0 ] br_if
\             ." a"
\         endblock
\         ." b"
\     endblock
\     ." c"
\     ;

: run-block
    0 .
    case \ block
        1 .
        false ?OF endof \ = br_if
        3 .
        false ?OF endof \ = br_if
        5 .
    0 endcase \ endblock
    6 .
    ;

: run
    \ 0 .
    5
    case \ loop
        dup .
        1-
        dup 0> ?OF contof \ = br_if in loop
        ." neg "
        dup -2 > ?OF contof \ = br_if in loop
    0 endcase \ endloop
    drop
    ;

see run cr

.s cr
run cr
.s cr

bye






CREATE FN 0 , 0 ,

: gen-code
    0 = IF
        POSTPONE 1 POSTPONE .
    ELSE 
        POSTPONE 22 POSTPONE .
    ENDIF
    
    ; IMMEDIATE

: create-function ( n -- xt )
    \ use return stack to smuggle data around the stack elements pushed by :noname
    >r
    :noname r> POSTPONE gen-code POSTPONE ;
    ;

123 create-function FN !

cr cr
FN @ xt-see
\ cr cr
\ FN @ EXECUTE

bye









\ CREATE CODE1 0 , \ ( a b -- a+b)
\ CREATE CODE1 0 , 1 , \ ( a b c -- a+(b-c))
CREATE CODE1
    6 ,
    $41 , $8 , \ i32.const 8
    $41 , $9 , \ i32.const 9
    $6a , \ i32.add
    $0b , \ return

CREATE FUNCTIONS 2 ALLOT

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
        $0b OF \ return
            POSTPONE EXIT
        ENDOF
        ( n ) ." unhandled \n" ( n )
    ENDCASE
    cell +
    ;

: compilation-loop  ( arr -- )
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

\ TODO loop over all functions
:noname [ 
    CODE1
    compilation-loop
] ; 0 CELLS FUNCTIONS + !

FUNCTIONS @ EXECUTE
.



bye
