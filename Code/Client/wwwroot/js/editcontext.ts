interface EditContextState {
	text: string;
	selectionStart: number;
	selectionEnd: number;
}


const IS_EDIT_CONTEXT_SUPPORTED = "EditContext" in window;

function initializeEditContext(target: HTMLElement, reference: BlazorDotNetReference, output: HTMLElement) {
	if (!IS_EDIT_CONTEXT_SUPPORTED) {
		return null;
	}

	let editContext = new EditContext({
		text: target.innerText
	});
	(target as any).editContext = editContext;

	editContext.addEventListener("textupdate", event => {
		event.preventDefault();
		event.stopImmediatePropagation();

		console.log(event);

		output.innerText = editContext.text;

		reference.invokeMethodAsync<boolean>("UpdateContent", event.text, event.updateRangeStart, event.updateRangeEnd).then(result => {
			if (!result) {
				restoreContextState();
				output.innerText = editContext.text;
				return;
			}

			output.innerText = editContext.text;
		});
	});

	editContext.addEventListener("textformatupdate", event => {
		console.log(event);
	});

	editContext.addEventListener("compositionstart", event => {
		console.log(event);
	});

	document.addEventListener("selectionchange", () => {
		const selection = document.getSelection()!;
		const offsets = fromSelectionToOffsets(selection, target);
		if (offsets) {
			updateSelection(offsets.start, offsets.end);
		}
	});

	output.innerText = editContext.text;

	let currentState = createContextState();

	return {
		afterRender: () => {
			updateText(target.innerText);
			const selection = document.getSelection();
			const offsets = fromSelectionToOffsets(selection, target);
			if (offsets) {
				updateSelection(offsets.start, offsets.end);
			}

			storeContextState();
			output.innerText = editContext.text;
		},
	};

	function createContextState(): EditContextState {
		return {
			text: editContext.text,
			selectionStart: editContext.selectionStart,
			selectionEnd: editContext.selectionEnd,
		};
	}

	function applyContextState(state: EditContextState) {
		editContext.updateText(0, editContext.text.length, state.text);
		editContext.updateSelection(state.selectionStart, state.selectionEnd);
	}

	function storeContextState(): EditContextState {
		return currentState = createContextState();
	}

	function restoreContextState(): EditContextState {
		applyContextState(currentState);
		return currentState;
	}

	function updateText(text: string) {
		editContext.updateText(0, editContext.text.length, text);
	}

	function updateSelection(start: number, end: number) {
		editContext.updateSelection(start, end);
		// Get the bounds of the selection.

		let selection = document.getSelection();
		if (selection) {
			editContext.updateSelectionBounds(selection.getRangeAt(0).getBoundingClientRect());
		}
	}

	function fromSelectionToOffsets(selection: Selection | null, editorEl: HTMLElement) {
		if (!selection)
			return null;

		const treeWalker = document.createTreeWalker(editorEl, NodeFilter.SHOW_TEXT);

		let anchorNodeFound = false;
		let extentNodeFound = false;
		let anchorOffset = 0;
		let extentOffset = 0;

		while (treeWalker.nextNode()) {
			const node = treeWalker.currentNode;
			if (node === selection.anchorNode) {
				anchorNodeFound = true;
				anchorOffset += selection.anchorOffset;
			}

			if (node === selection.focusNode) {
				extentNodeFound = true;
				extentOffset += selection.focusOffset;
			}

			if (node.nodeType == Node.TEXT_NODE) {
				if (!anchorNodeFound) {
					anchorOffset += node.textContent?.length ?? 0;
				}
				if (!extentNodeFound) {
					extentOffset += node.textContent?.length ?? 0;
				}
			}
		}

		if (!anchorNodeFound || !extentNodeFound) {
			return null;
		}

		return { start: anchorOffset, end: extentOffset };
	}
}