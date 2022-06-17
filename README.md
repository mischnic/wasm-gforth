# wasm-gforth

A Webassembly runtime that compiles/interprets Wasm bytecode _just-in-time_ into/using Gforth code.

Only a (minimal) subset of the MVP feature set is supported.

Developed and tested using [Gforth 0.7.9_20210930](https://www.complang.tuwien.ac.at/forth/gforth/Snapshots/0.7.9_20210930/gforth-0.7.9_20210930.tar.xz)

## Architecture

The Webassembly input is [translated](https://github.com/mischnic/wasm-gforth/blob/674a1f89b03e0b3c113140abbd159d2553933d42/main.fs#L112-L118) into Forth words in a single pass, e.g.

```wat
i32.const 0
i32.const 1
i32.add
```

becomes

```forth
0 1 add
```

This is done for every function (which also become a Forth colon-definitions, which have the same semantics regarding parameters and the return value). Locals are handled via Gforth's locals extension, and control flow is modelled using Forth's control flow stack (the regular `if`/`end`/`until` plus `cs-roll` and `cs-drop`) which eventually result in Assembly-like jumps.

So there is no optimization, but the runtime speed should be similar to an equivalent hand-written Forth program.

## Usage

```
gforth main.fs program-to-run.wasm
```

## Status

Not implemented (in order of priority):

- miscellaneous simple (arithmetic) instructions
- `br_table`
- (interpret code for initial values and offsets in data sections and global sections)

Bugs:

- should fall back to `_start` function (WASI) if no start is defined
- use uleb128 for parsing memory section sizes and global init (currently `(memory 128)` fails to parse)

Would be great to run the spec tests for MVP without newer extensions (https://github.com/turbolent/w2c2/tree/e48a25a/tests)
