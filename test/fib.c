int fib(int i) {
	if (i == 0)
		return 1;
	else
		return i * fib(i - 1);
}

int main() {
	return fib(22);
}
