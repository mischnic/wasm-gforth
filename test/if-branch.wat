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
    block $B0 (result i32)
      i32.const 10
      local.get $x i32.const 1 i32.eq
      br_if $B0
      i32.const 1
      i32.add
    end
  )

  (func $b (param $x i32) (result i32)
    block $B0 (result i32)
      i32.const 10
      local.get $x i32.const 1 i32.eq
      br_if $B0
      i32.const 1
      i32.add
      return
    end
    i32.const 2
    i32.add
  )

  (func $fac-rec (param $x i32) (result i32)
    i32.const 1
    local.get $x i32.const 0 i32.eq
    br_if 0
    local.get $x i32.const 1 i32.sub call $fac-rec
    i32.mul
    local.get $x i32.mul
  )

  (func $main
    i32.const 1 call $a     i32.const 10 call $assert
    i32.const 0 call $a     i32.const 11 call $assert

    i32.const 1 call $b     i32.const 12 call $assert
    i32.const 0 call $b     i32.const 11 call $assert

    i32.const 10 call $fac-rec  i32.const 3628800 call $assert
  )

  (start $main)
)


