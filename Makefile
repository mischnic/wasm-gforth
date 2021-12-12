.PHONY: all clean tests

all: tests

clean: 
	rm test/*.wasm

tests: test/add.wasm test/bare.wasm test/locals.wasm test/hello_world.wasm test/hello_world_rs.wasm test/if.wasm test/if-nested.wasm

%.wasm: %.wat
	wat2wasm -o $@ $<

%.wasm: %.rs
	rustc -C opt-level=z -C lto -o $@ $< --target wasm32-wasi
#     rustc $(CFLAGS) -c -o $@ $<

%.wasm: %.c
	PATH="/usr/local/opt/llvm/bin:${PATH}" \
	LDFLAGS="-L/usr/local/opt/llvm/lib -Wl,-rpath,/usr/local/opt/llvm/lib" \
	CPPFLAGS="-I/usr/local/opt/llvm/include" \
	/usr/local/opt/llvm/bin/clang --target=wasm32 -O3 \
		-nostdlib -Wl,--no-entry -Wl,--export-all -o $@ $< 

%.opt.wasm: %.wasm
	wasm-opt -o $@ $< -Oz
