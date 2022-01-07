require ../cs-stack.fs

: gen-test0 { type-stack cs-stack -- }
    TYPE-BLOCK type-stack stack.push \ fn
    TYPE-BLOCK type-stack stack.push \ block 1
        TYPE-BLOCK type-stack stack.push \ block 2       
            postpone dup postpone 0>
            0 type-stack cs-stack gen-br_if \ br_if 0
            1 postpone literal postpone .
            postpone dup postpone 0<
            1 type-stack cs-stack gen-br_if \ br_if 1
            -1 postpone literal postpone .
        type-stack cs-stack gen-end \ end 2
        postpone drop
        8 postpone literal postpone .
    type-stack cs-stack gen-end \ end 1
    9 postpone literal postpone .
    type-stack cs-stack gen-end \ end 0
    \ postlude to drop locals
; immediate

: gen-test1 { type-stack cs-stack -- }
    TYPE-BLOCK type-stack stack.push \ fn
    TYPE-BLOCK type-stack stack.push \ block 1
        postpone begin \ loop 2
                    TYPE-LOOP type-stack stack.push 
                    type-stack stack.size 1- cs-stack cs-stack.push-loop-begin
            postpone 1-
            postpone dup postpone .
            postpone dup postpone 0<
            0 type-stack cs-stack gen-br_if  \ br 0, absolute: 1
        type-stack cs-stack gen-end \ end 2
        postpone drop
    type-stack cs-stack gen-end \ end 1
    type-stack cs-stack gen-end \ end 0
; immediate

: test 
    [ stack.new ]
    [ stack.new ]
    \ [ 2dup ] gen-test0
    [ 2dup ] gen-test1
    [ stack.destroy ]
    [ stack.destroy ]
    ;

\ see test cr

10 test cr
0 test cr
-2 test cr


bye
