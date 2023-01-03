function initializeEditor(editor) {
	var observer = new MutationObserver(function (e) {
		console.log(e);
	});

	observer.observe(editor, { childList: true, subtree: true, characterData: true });
}