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
