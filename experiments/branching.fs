(
    block
        ...
        br_if \ conditional jmp after end
        ...
        br_if \ conditional jmp after end
        ...
    end

    ---

    case \ block
        ...
        ?OF endof \ = br_if
        ...
        ?OF endof \ = br_if
        ...
    0 endcase \ endblock
)


(
    loop
        ...
        br_if \ conditional jmp to start
        ...
        br \ jmp to start
        ...
    end
    ---

    case \ loop
        ...
        ?OF contof \ = br_if in loop
        ...
        true ?OF contof \ = br_if in loop
        ...
    0 endcase \ endloop
    drop
)

(
    if \ take condition
        ...
    else 
        ...
    end
    ---

    ...same, but endif instead of end...

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
