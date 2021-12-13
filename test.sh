#!/bin/bash
for f in test/*.wasm; do
    case $f in 
        test/hello_world_rs.wasm) continue ;;
        test/if-nested.wasm) continue ;;
        test/if.wasm) continue ;;
    esac

    echo ─────── $f ───────
    refOut=$(wasmtime $f)
    refExit=$?
    runOut=$(gforth main.fs $f)
    runExit=$?
    diff <(echo -n $refOut) <(echo -n $runOut)
    if [[ $refExit != $runExit  ]]; then
        echo "Exit code $runExit != $refExit"
    fi
done
