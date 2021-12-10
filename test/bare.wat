(module
  (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

  (memory 10)
  (export "memory" (memory 0))

  (func $main
    (call $main)
    ;; (call $proc_exit (i32.const 7))
  )

  (start $main)
)
