\ stack = [ size capacity ...data ]
: stack.new ( -- stack )
    16 cells allocate throw
    0 over !
    16 over cell+ !
;
: stack.destroy ( stack -- )
    free throw
    ;
\ : cs-stack.resize ( cap stack -- )
\     swap
\     2dup cells swap resize
\ ;
: stack.size ( stack -- u )
    @
    ;
: stack.bot-addr ( stack -- a-addr )
    2 cells +
    ;
\ undefined behaviour for empty stacks
: stack.top-addr ( stack -- a-addr )
    dup @ 1+ cells +
    ;
: stack.push ( v stack -- )
    \ todo resize if needed
    1 over +!
    stack.top-addr
    !
;
: stack.get ( index-top stack -- v )
    stack.top-addr swap cells - @
;
: stack.head ( stack -- v )
    stack.top-addr @
;
: stack.pop ( stack -- v )
    -1 over +!
    stack.top-addr cell+ @
;
: stack.remove { index stack -- v }
    stack stack.top-addr index cells - 
    dup @
    swap
    dup cell+ swap index cells move
    -1 stack +!
;

0 CONSTANT TYPE-BLOCK
1 CONSTANT TYPE-IFELSE
2 CONSTANT TYPE-LOOP
$8000000000000000 CONSTANT CS-STACK-IGNORE
$4000000000000000 CONSTANT CS-STACK-LOOP-MASK

: cs-stack.push-ignore ( stack -- )
    CS-STACK-IGNORE ( only 1st bit is set )
    swap
    stack.push
;
: cs-stack.push-loop-begin ( index stack -- )
    swap
    CS-STACK-LOOP-MASK ( only 2nd bit is set ) or
    swap
    stack.push
;
: cs-stack.push-block-branch ( index stack -- )
    stack.push
;

\ Returns the index of the branche with that index and removes it
: cs-stack.pop-index-block-branches { index stack -- index }
    stack stack.size
    0
    ?DO
        i stack stack.get index = IF
            i stack stack.remove drop
            i
            unloop
            exit
        ENDIF
    LOOP
    -1

    \ this returns -1 v1 .. vn:
    \ -1 \ the -1 "vararg-end" return value
    \ 0 \ number of items removed so far, to undo offset from removal
    \ 0
    \ BEGIN
    \     dup stack stack.get index = IF
    \         dup stack stack.remove drop
    \         dup over + -rot
    \         swap 1+ swap
    \     ELSE
    \         1+
    \     ENDIF
    \ dup stack stack.size >=
    \ UNTIL
    \ 2drop
;

\ Returns the index of the loop-begin with that index
: cs-stack.find-index-loop-dest { index stack -- index }
    stack stack.size
    0
    ?DO
        i stack stack.get CS-STACK-LOOP-MASK index or = IF
            i
            unloop
            exit
        ENDIF
    LOOP
    -1
;
\ Returns the index of topmost loop-begin and removes it
\ undefined behaviour if there is no loop begin on the stack
: cs-stack.pop-index-loop-dest ( stack -- index )
    dup stack.size
    0
    ?DO
        i over stack.get CS-STACK-LOOP-MASK and IF
            i swap stack.remove drop
            i
            unloop
            exit
        ENDIF
    LOOP
    -1
;
\ Returns the index of topmost ignore and removes it
: cs-stack.pop-index-ignore ( stack -- index )
    dup stack.size
    0
    ?DO
        i over stack.get CS-STACK-IGNORE = IF
            i swap stack.remove drop
            i
            unloop
            exit
        ENDIF
    LOOP
    -1
;

\ stack.new
\ 1 over cs-stack.push-block-branch
\ 2 over cs-stack.push-block-branch
\ 8 over cs-stack.push-loop-begin
\ 2 over cs-stack.push-block-branch
\ dup 5 2 + cells dump
\ \ 3 over stack.get . cr
\ \ 2 over stack.remove . cr
\ \ dup stack.pop . cr
\ \ dup stack.bot-addr hex . cr
\ \ dup stack.top-addr hex . cr
\ \ dup cs-stack.pop-index-loop-dest . cr
\ 8 over cs-stack.find-index-loop-dest . cr
\ 2 over cs-stack.pop-index-block-branches . cr
\ 2 over cs-stack.pop-index-block-branches . cr
\ 2 over cs-stack.pop-index-block-branches . cr
\ 5 2 + cells dump
\ bye


100 maxdepth-.s !

: gen-endloop ( stack -- )
    cs-stack.pop-index-loop-dest cs-roll cs-drop
    ;
: gen-endblock ( index stack -- )
    BEGIN
        2dup cs-stack.pop-index-block-branches
        dup -1 <> IF
            -rot >r >r dup >r
            cs-roll
            postpone endif
            r> r> r> rot
        ENDIF
        -1 =
    UNTIL
    2drop
;

: gen-end { type-stack cs-stack -- }
    type-stack stack.pop TYPE-LOOP = IF
        cs-stack gen-endloop
    ELSE \ TYPE-BLOCK or TYPE-IFELSE
        type-stack stack.size cs-stack gen-endblock
    ENDIF
;

: gen-br_if { offset type-stack cs-stack }
    type-stack stack.size 1- offset - \ absolute index
    offset type-stack stack.get TYPE-LOOP = IF
        cs-stack cs-stack.find-index-loop-dest cs-pick
        postpone 0= postpone until
    ELSE \ TYPE-BLOCK or TYPE-IFELSE
        cs-stack cs-stack.push-block-branch
        postpone 0= postpone if
    ENDIF
;

: gen-br { offset type-stack cs-stack }
    type-stack stack.size 1- offset - \ absolute index
    offset type-stack stack.get TYPE-LOOP = IF
        cs-stack cs-stack.find-index-loop-dest cs-pick
        postpone again
    ELSE \ TYPE-BLOCK or TYPE-IFELSE
        cs-stack cs-stack.push-block-branch
        postpone ahead
    ENDIF
;
