(module 
  (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

  (memory 10)
  (export "memory" (memory 0))

  (func $main
    i32.const 0x80000000
    i32.const 1
    i32.shl
    i32.const 1
    i32.shr_u
    call $proc_exit
  )

  (start $main)
)
