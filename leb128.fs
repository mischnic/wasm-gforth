: check-clear-byte-msb { n1 -- n2 msb }
    n1 %01111111 and
    n1 %10000000 and 0<>
    ;

: consume-uleb128-base { c-addr -- i u }
    0 0 ( result i )
    BEGIN
        dup chars c-addr + c@ check-clear-byte-msb ( result i byte should-continue )
        >r
        over 7 * lshift
        rot + swap
        1+
        r>
        WHILE
    REPEAT
    ;

: consume-uleb128 { c-addr -- c-addr2 u }
    c-addr consume-uleb128-base
    chars c-addr + swap
    ;

: consume-leb128 { c-addr -- c-addr2 s }
    c-addr consume-uleb128-base
    dup chars c-addr + -rot
    7 * 1-
    -1 swap lshift ( c-addr2 result mask )
    \ sign-extend the 7*i-bit number to 64 bit
    \ if any bit in the mask is set, set all of them
    2dup and 0<> IF
        or
    ELSE
        drop
    ENDIF
    ;


\ CREATE EXAMPLE1 1 ALLOT \ 624485 = $98765
\ $E5 EXAMPLE1 c!
\ $8E EXAMPLE1 1 chars + c!
\ $26 EXAMPLE1 2 chars + c!

\ CREATE EXAMPLE1 1 ALLOT \ 234
\ $EA EXAMPLE1 c!
\ $01 EXAMPLE1 1 chars + c!

\ CREATE EXAMPLE1 1 ALLOT \ 7
\ $07 EXAMPLE1 c!

\ CREATE EXAMPLE1 1 ALLOT \ 234
\ $0 EXAMPLE1 c!

\ CREATE EXAMPLE1 1 ALLOT \ -123456 = $FFFFFFFFFFFE1DC0
\ $C0 EXAMPLE1 c!
\ $BB EXAMPLE1 1 chars + c!
\ $78 EXAMPLE1 2 chars + c!

\ EXAMPLE1 consume-uleb128 . drop cr
\ EXAMPLE1 consume-leb128 . drop cr
\ \ EXAMPLE1 1 chars + .

\ bye
