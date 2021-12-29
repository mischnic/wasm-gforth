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

: run-loop
    ;

: run-block
    ." before 1" cr
    \ block
        ." a" cr
        true if \ br_if
        ." b" cr
        ahead \ br
        ." c" cr
    then \ endblock (duplicate for each br?)
    then
    ." after 1" cr
    ;

: run-loop-loop
    ." before 1" cr
    begin \ loop
        ." before 2" cr
        begin \ loop
            ." b2" cr
            true [ 0 cs-pick ] until \ br_if 0
            \ [ 1 cs-pick ] again \ br 1
            ." c2" cr
        [ cs-drop ] \ end (loop)
        ." after 2" cr

        [ 0 cs-pick ] again \ br 0
    [ cs-drop ] \ end (loop)
    ." after 1" cr
    ;

: run-block-block
    ." before 1" cr
    \ block
        ." before 2" cr
        \ block
            ." a2" cr
            true if \ br_if 0
            ." b2" cr
            true if \ br_if 1
            ." c2" cr
            false if \ br_if 1
            ." d2" cr
        [ 2 cs-roll ] then \ end (block)
        ." after 2" cr
    [ 0 cs-roll ] then \ end (block)
    [ 0 cs-roll ] then \ end (block)
    ." after 1" cr
    ;


: run-block-loop
    ." before 1" cr
    \ block
        ." before 2" cr
        begin \ loop
            ." a2" cr
            true [ 0 cs-pick ] until \ br_if 0
            ." c2" cr
            false if \ br_if 1
            ." d2" cr
        [ 1 cs-roll cs-drop ] \ end (loop)
        ." after 2" cr
    [ 0 cs-roll ] then \ end (block)
    ." after 1" cr
    ;

\ CS-PICK       orig0/dest0 orig1/dest1 ... origu/destu u -- ... orig0/dest0
\ CS-ROLL       destu/origu .. dest0/orig0 u -- .. dest0/orig0 destu/origu
\ CS-DROP       dest --

100 maxdepth-.s !
\ .s cr
run-block-loop cr
\ .s cr

bye
