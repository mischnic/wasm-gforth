int factorial(int i) {
	if (i == 0)
		return 1;
	else
		return i * factorial(i - 1);
}

int main() {
	return factorial(4);
}
