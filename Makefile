TESTS=test/add.wasm test/bare.wasm test/fac.wasm test/global.wasm test/hello_world_rs.wasm test/hello_world.wasm \
	  test/if.wasm test/if-branch.wasm test/if-branch-nested.wasm test/locals.wasm test/memory.wasm test/overflow.wasm \
	  test/params.wasm

.PHONY: all clean tests

all: tests

clean: 
	rm -f test/*.wasm

make test: tests
	./test.sh

tests: $(TESTS) test/fac.opt.wasm


test/fac.opt.wasm: test/fac.wasm
	wasm-opt -O4 test/fac.wasm -o test/fac.opt.wasm

%.wasm: %.wat
	wat2wasm -o $@ $<

%.wasm: %.rs
	rustc -C opt-level=z -C lto -o $@ $< --target wasm32-wasi
#     rustc $(CFLAGS) -c -o $@ $<

%.wasm: %.c
	/usr/local/opt/llvm/bin/clang --target=wasm32 -Oz \
		-nostdlib -Wl,--export-all -o $@ $< 

%.opt.wasm: %.wasm
	wasm-opt -o $@ $< -Oz
