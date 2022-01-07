(module
  (import "wasi_unstable" "proc_exit"      (func $proc_exit (param i32)))

  (memory 10)
  (export "memory" (memory 0))

  (func $assert (param $x i32) (param $y i32) 
    block $B0
      local.get $x
      local.get $y
      i32.eq
      br_if $B0
      i32.const 1
      call $proc_exit
    end
  )

  (func $a (param $x i32) (result i32)
    local.get $x
    i32.const 4
    i32.eq
    if (result i32)
      i32.const 25
    else
      i32.const 60
    end
  )

  (func $b (param $x i32) (result i32)
    local.get $x
    i32.const 4
    i32.eq
    if
      local.get $x
      i32.const 5
      i32.add
      local.set $x
    end
    local.get $x
  )

  (func $main
    i32.const 0 call $a     i32.const 60 call $assert
    i32.const 4 call $a     i32.const 25 call $assert

    i32.const 3 call $b     i32.const  3 call $assert
    i32.const 4 call $b     i32.const  9 call $assert
  )

  (start $main)
)


