(module
    ;; (File Descriptor, *iovs, iovs_len, *nwritten) -> Returns number of bytes written
    (import "wasi_unstable" "fd_write" (func $fd_write (param i32 i32 i32 i32) (result i32)))

    (memory 1)
    (export "memory" (memory 0))

    (data (i32.const 8) "Hello World\n")

    (start $main)

    (func $main
        i32.const 0
        i32.const 8
        i32.store ;; pointer

        i32.const 4
        i32.const 12
        i32.store ;; length

        (i32.const 1) 
        (i32.const 0) 
        (i32.const 1) 
        (i32.const 20)

        call $fd_write
        drop
    )
)
