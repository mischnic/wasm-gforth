(module
  (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

  (global $a (mut i32) (i32.const 1))
  (global $b i32       (i32.const 2))
  (global $c i32       (i32.const 3))

  (memory 10)
  (export "memory" (memory 0))

  (func $main
    global.get $a
    global.get $b
    global.get $c
    i32.add
    i32.add
    global.set $a

    global.get $b
    global.get $a
    i32.add
    call $proc_exit
  )

  (start $main)
)


