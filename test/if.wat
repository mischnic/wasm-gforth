(module
  (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

  (memory 10)
  (export "memory" (memory 0))

  (func $main
    i32.const 0 i32.const 1 i32.eq
    if (result i32)
      i32.const 1
    else
      i32.const 2
    end
    call $proc_exit
  )

  (start $main)
)


