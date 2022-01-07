#!/bin/bash
result=0
for f in test/*.wasm; do
    arguments=
    case $f in 
        test/params.wasm) 
            arguments="lm n"
        ;;
        test/fac.wasm) continue ;;
        test/hello_world_rs.wasm) continue ;;
    esac

    echo ─────── $f ───────
    refOut=$(wasmtime $f $arguments)
    refExit=$?
    runOut=$(./wasm-gforth $f $arguments)
    runExit=$?
    diff <(echo -n $refOut) <(echo -n $runOut)
    if [[ $refExit != $runExit  ]]; then
        echo "Exit code $runExit != $refExit"
        result=1
    fi
done

exit $result
