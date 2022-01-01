#define __WASI_SYSCALL_NAME(name)                                                                  \
	__attribute__((__import_module__("wasi_unstable"), __import_name__(#name)))

void exit(int) __WASI_SYSCALL_NAME(proc_exit) __attribute__((noreturn));

int factorial(int i) {
	// return 42;
	if (i == 0)
		return 1;
	else
		return i * factorial(i - 1);
}

int _start() {
	exit(factorial(5));
}
