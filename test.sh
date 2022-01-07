#!/bin/bash
for f in test/*.wasm; do
    arguments=
    case $f in 
        test/params.wasm) 
            arguments="lm n"
        ;;
        test/fac.wasm) continue ;;
        test/fac.opt.wasm) continue ;;
        test/hello_world_rs.wasm) continue ;;
        test/if.wasm) continue ;;
    esac

    echo ─────── $f ───────
    refOut=$(wasmtime $f $arguments)
    refExit=$?
    runOut=$(gforth main.fs $f $arguments)
    runExit=$?
    diff <(echo -n $refOut) <(echo -n $runOut)
    if [[ $refExit != $runExit  ]]; then
        echo "Exit code $runExit != $refExit"
    fi
done
