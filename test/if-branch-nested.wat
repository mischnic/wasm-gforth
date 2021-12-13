(module
  (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

  (memory 10)
  (export "memory" (memory 0))

  (func $main
    block $outer
      block $inner
        i32.const 0 i32.const 0 i32.eq
        br_if $outer
        i32.const 22
        call $proc_exit
      end
      i32.const 33
      call $proc_exit
    end
    i32.const 55
    call $proc_exit
  )

  (start $main)
)


