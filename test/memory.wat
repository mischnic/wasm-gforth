(module
    (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

    (memory 32)
    (export "memory" (memory 0))

    (start $main)

    (func $main
        i32.const 0
        i32.const 8
        i32.store

        i32.const 4
        i32.const 5
        i32.store

        i32.const 0
        i32.load
        i32.const 0
        i32.load offset=4

        i32.add

        call $proc_exit
    )
)
