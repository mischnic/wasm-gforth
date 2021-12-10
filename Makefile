.PHONY: all clean tests

all: tests

clean: 
	rm test/*.wasm

tests: test/add.wasm test/bare.wasm test/locals.wasm test/hello_world.wasm test/hello_world_rs.wasm

%.wasm: %.rs
	rustc -C opt-level=z -C lto -o $@ $< --target wasm32-wasi
#     rustc $(CFLAGS) -c -o $@ $<

%.wasm: %.wat
	wat2wasm -o $@ $<

%.opt.wasm: %.wasm
	wasm-opt -o $@ $< -Oz
