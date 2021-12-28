\ ------------------ Parser

0 Value fd-in
: open-input ( c-addr u -- ) r/o open-file throw to fd-in ;
: close-input ( -- ) fd-in close-file throw ;
: next-byte ( -- n )
    fd-in key-file
    ;
: skip-bytes ( n -- )
    0 ?DO
        next-byte drop
    LOOP
    ;    
: read-bytes-packed ( c-addr n -- )
    \ read-line sometimes corrups the last byte?
    \ fd-in read-line throw 2drop
    over + swap +DO
        next-byte i !
    LOOP
    ;    
: read-bytes ( a-addr n -- )
    cells over + swap +DO
        next-byte i !
    cell +LOOP
    ;    

CREATE TYPES 32 2 * CELLS ALLOT \ tid*2 -> [nreturn nparams]
CREATE FN-TYPES 32 CELLS ALLOT \ fid -> tid
CREATE FN-INFOS 32 CELLS ALLOT \ fid -> pointer to [nlocals nbytes ...packed-code] OR [0 0 (ptr to host function)]
0 VALUE COUNT-FN
0 VALUE COUNT-FN-IMPORTED
0 Value MEMORY-SIZE
0 Value MEMORY-PTR
0 Value GLOBALS-PTR
-1 VALUE START-FN

: index-to-fid  ( u -- u )
    COUNT-FN-IMPORTED + ;

: ptr-to-real-addr ( ptr -- c-addr )
    MEMORY-PTR +
    ;

: wasi_unstable.proc_exit ( s -- )
    (bye) ;

: wasi_unstable.fd_write { fd ptr n addr-nwritten -- nwritten }
    fd 1 <> IF ." fd_write: unsupported fd" bye ENDIF
    n 0 ?DO
        ptr ptr-to-real-addr
        dup ul@ ptr-to-real-addr \ c-addr
        swap 4 + ul@ \ u
        type
    LOOP
    0
    ;

: parse-section-noop
    next-byte skip-bytes
    ;

: parse-section-type
    1 skip-bytes
    next-byte \ number of types
    0 ?DO
        1 skip-bytes \ TODO handle other types apart from 60=function?
        next-byte \ numbers of params
        dup skip-bytes
        next-byte \ numbers of return values
        dup skip-bytes
        TYPES i 2 * cells + 2!
    LOOP
    ;

CREATE IMPORT-READ-BUFFER 128 ALLOT
: parse-section-imports
    1 skip-bytes
    next-byte
    dup TO COUNT-FN-IMPORTED
    0 ?DO
        next-byte dup  IMPORT-READ-BUFFER swap read-bytes-packed
        IMPORT-READ-BUFFER swap
        s" wasi_unstable" compare
        IF ." import from unknown module" bye ENDIF

        3 CELLS ALLOCATE throw
        dup FN-INFOS i cells + !
        0 over !
        0 over cell+ !
        2 cells +
        CASE
            next-byte dup  IMPORT-READ-BUFFER swap read-bytes-packed
            IMPORT-READ-BUFFER swap
            2dup s" proc_exit" compare invert ?OF
                2drop
                ['] wasi_unstable.proc_exit
            ENDOF
            2dup s" fd_write" compare invert ?OF
                2drop
                ['] wasi_unstable.fd_write
            ENDOF
            ." unknown import name" bye
        ENDCASE
        swap !
        2 skip-bytes \ TODO other import types
    LOOP
    ;

: parse-section-memory
    1 skip-bytes
    next-byte 1 = IF
        next-byte 0 = IF 
            next-byte
            dup TO MEMORY-SIZE
            allocate throw TO MEMORY-PTR
            MEMORY-PTR MEMORY-SIZE erase \ TODO use chars ?
        ENDIF
    ENDIF
    ;

: parse-section-start
    1 skip-bytes
    next-byte TO START-FN
    ;

: parse-section-functions
    1 skip-bytes
    next-byte
    0 ?DO
        next-byte
        FN-TYPES i index-to-fid cells + !
    LOOP
    ;

: parse-section-code
    1 skip-bytes
    next-byte \ number of functions
    dup COUNT-FN-IMPORTED + TO COUNT-FN
    0 ?DO
        next-byte 1- \ bytes of code
        next-byte \ how many locals
        dup 2 * skip-bytes \ skip locals descriptor TODO there might be more possibilites
        swap over 2 * -
        dup chars 2 cells + allocate throw
        dup FN-INFOS i index-to-fid cells + !
        rot
        over !
        cell+
        2dup !
        cell+ swap read-bytes-packed
    LOOP
    ;

: parse-section-data
    1 skip-bytes
    next-byte
    0 ?DO
        1 skip-bytes \ type TODO there could be more, assume `00`
        1 skip-bytes next-byte 1 skip-bytes \ offset TODO interpret instead of assuming `i32.const X end`
        ptr-to-real-addr
        next-byte ( target length )
        read-bytes-packed
    LOOP
    ;

: parse-section-global
    1 skip-bytes
    next-byte
    dup cells allocate throw TO GLOBALS-PTR
    0 ?DO
        1 skip-bytes \ TODO there could be more, assume `7f` = i32
        1 skip-bytes \ ignore info about mutability
        1 skip-bytes next-byte 1 skip-bytes \ initial value TODO interpret instead of assuming `i32.const X end`
        i cells GLOBALS-PTR + !
    LOOP
    ;

CREATE SECTION-HANDLERS
    ( 0 ) ' noop ,
    ( 1 ) ' parse-section-type ,
    ( 2 ) ' parse-section-imports ,
    ( 3 ) ' parse-section-functions ,
    ( 4 ) ' parse-section-noop ,
    ( 5 ) ' parse-section-memory ,
    ( 6 ) ' parse-section-global ,
    ( 7 ) ' parse-section-noop ,
    ( 8 ) ' parse-section-start ,
    ( 9 ) ' parse-section-noop ,
    ( 10 ) ' parse-section-code ,
    ( 11 ) ' parse-section-data ,

: parse-wasm 
    8 skip-bytes
    BEGIN
        next-byte dup -1 <>
    WHILE
        \ dup .
        11 min
        CELLS SECTION-HANDLERS + @ EXECUTE
    REPEAT
    drop
;
