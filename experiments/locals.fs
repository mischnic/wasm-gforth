: locals-test ( n n -- n )
    lp- >l >l
    @local# [ 0 cells , ] \ locals.get 0
    @local# [ 1 cells , ] \ locals.get 1
    + \ i32.add
    dup laddr# [ 2 cells , ] ! \ locals.tee 2
    @local# [ 2 cells , ] \ locals.get 2
    + \ i32.add
    lp+ lp+ lp+
    ;
1 2 locals-test .s

bye
