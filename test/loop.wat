(module
  (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

  (memory 10)
  (export "memory" (memory 0))

  (func $assert (param $x i64) (param $y i64) 
    block $B0
      local.get $x
      local.get $y
      i64.eq
      br_if $B0
      i32.const 1
      call $proc_exit
    end
  )

  (func $fac (param $n i64) (result i64) (local $result i64) 
    i64.const 1 local.set $result
    loop $L
        local.get $result
        local.get $n
        i64.mul
        local.set $result

        local.get $n
        i64.const 1
        i64.sub
        local.tee $n
        i64.const 0
        i64.ne br_if $L
    end
    local.get $result
  )

  (func $main
    i64.const  4 call $fac  i64.const                  24 call $assert
    i64.const 20 call $fac  i64.const 2432902008176640000 call $assert
  )

  (start $main)
)
