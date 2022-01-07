# wasm-gforth

A Webassembly runtime that compiles Wasm bytecode _just-in-time_ into Gforth code.

Developed and tested using [Gforth 0.7.9_20210930](https://www.complang.tuwien.ac.at/forth/gforth/Snapshots/0.7.9_20210930/gforth-0.7.9_20210930.tar.xz)

## Usage

```
gforth main.fs program-to-run.wasm
```

## Roadmap

For now, the goal is to support the MVP feature set.

Not implemented yet (in order of priority):

- miscellaneous (arithmetic) instructions
- (interpret code for initial values and offsets in data sections and global sections)

Bugs:

- fall back to `_start` function (WASI) if no start is defined
- use uleb128 for parsing memory section sizes and global init (currently `(memory 128)` fails to parse)
- is memory automatically resized if accessed outside of bounds?
- can memory accesses can be unaligned?

Find and run the spec tests for MVP without newer extensions (https://github.com/turbolent/w2c2/tree/e48a25a/tests)
