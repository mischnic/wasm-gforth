(module 
  (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

  (memory 10)
  (export "memory" (memory 0))

  (func $add (param $x i32) (param $y i32) (result i32)
    local.get $y
    local.get $x
    nop
    i32.add
  )

  (func $main
    i32.const 1
    i32.const 4
    call $add
    call $proc_exit
  )

  (start $main)
)
