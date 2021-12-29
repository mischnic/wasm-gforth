(module
  (import "wasi_unstable" "args_sizes_get" (func $args_sizes_get (param i32) (param i32) (result i32)))
  (import "wasi_unstable" "args_get"       (func $args_get (param i32) (param i32) (result i32)))
  (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

  (memory 64)
  (export "memory" (memory 0))

  (func $main
    i32.const 0 ;; ptr to num of arguments
    i32.const 4 ;; ptr to total length of string
    call $args_sizes_get drop
    
    i32.const 8 ;; ptr to array of argument positions
    i32.const 32 ;; ptr to buffer
    call $args_get drop
    
    i32.const 12 ;; 8+4, skip first parameter = program name
    i32.load
    i32.load8_u
    i32.const 97
    i32.sub
    call $proc_exit
  )

  (start $main)
)
