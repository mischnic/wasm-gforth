TESTS=test/add.wasm test/bare.wasm test/global.wasm test/hello_world_rs.wasm test/hello_world.wasm test/if.wasm test/if-branch.wasm test/if-branch-nested.wasm test/locals.wasm test/memory.wasm test/overflow.wasm

.PHONY: all clean tests

all: tests

clean: 
	rm -f test/*.wasm

make test: tests
	./test.sh

tests: $(TESTS)

%.wasm: %.wat
	wat2wasm -o $@ $<

%.wasm: %.rs
	rustc -C opt-level=z -C lto -o $@ $< --target wasm32-wasi
#     rustc $(CFLAGS) -c -o $@ $<

%.wasm: %.c
	/usr/local/opt/llvm/bin/clang --target=wasm32 -O3 \
		-nostdlib -Wl,--no-entry -Wl,--export-all -o $@ $< 

%.opt.wasm: %.wasm
	wasm-opt -o $@ $< -Oz
