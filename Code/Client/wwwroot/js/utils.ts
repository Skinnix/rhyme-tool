function downloadFile(url: string, fileName?: string | null) {
	const a = document.createElement('a');
	if (!fileName) {
		fileName = url.split('/').pop() ?? null;
	}

	if (fileName)
		a.download = fileName;

	a.click();
}