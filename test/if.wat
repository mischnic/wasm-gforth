(module
  (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

  (memory 10)
  (export "memory" (memory 0))

  (func $main
    i32.const 0
    if
      i32.const 1 call $proc_exit
      br 0
      br 1
    else
      i32.const 2 call $proc_exit
      br 0
      br 1
    end
    i32.const 3 call $proc_exit

    ;; block
    ;;   block 
    ;;     i32.const 0 br_if 0
    ;;     i32.const 1 call $proc_exit
    ;;     br 1
    ;;     br 2
    ;;   br 1 end
    ;;   i32.const 2 call $proc_exit
    ;;   br 0
    ;;   br 1
    ;; end
    ;; i32.const 3 call $proc_exit
  )

  (start $main)
)


