function initializeEditor(content, editor, reference, serverSide) {
	function CreateId() {
		return ([1e7] + -1e3 + -4e3 + -8e3 + -1e11).replace(/[018]/g, c =>
			(c ^ crypto.getRandomValues(new Uint8Array(1))[0] & 15 >> c / 4).toString(16)
		);
	}

	editor.innerHTML = content.innerHTML;

	let observer = new MutationObserver(function (mutations) {
		let addedNodes = [];
		let removedNodes = [];
		let modifiedNodes = [];

		let actions = [];

		mutations.forEach(m => {
			if (m.type == 'attributes')
				return;

			let handled = false;
			m.addedNodes.forEach(n => {
				//ignore text nodes
				if (!n.attributes)
					return;

				//ignore BR elements
				if (n?.tagName == 'BR')
					return;
				
				//create an ID for the node
				let id = CreateId();
				actions.push(() => n.setAttribute('data-node-id', id));

				//add to the list
				let parent = n.parentElement;
				let index = Array.prototype.indexOf.call(parent.childNodes, n);
				addedNodes.push({
					id: id,
					parent: n.parentElement.getAttribute('data-node-id'),
					index: index,
					content: n.innerText
				});
				handled = true;
			});

			m.removedNodes.forEach(n => {
				//ignore text nodes
				if (!n.attributes)
					return;

				//ignore BR elements
				if (n?.tagName == 'BR') {
					handled = true;
					return;
				}

				//find ID
				let id = n.getAttribute('data-node-id');
				if (!id)
					return;

				//add to the list
				removedNodes.push(id);
				handled = true;
			});

			//modified?
			if (!handled || m.type != 'childList') {
				let target = m.target;
				if (!target.attributes)
					target = target.parentElement;

				//add modification
				if (target) {
					let id = target.getAttribute('data-node-id');
					if (!modifiedNodes.some(n => n.id == id)) {
						//handle line breaks as empty lines
						let content = target.innerText;
						if (content == "\n")
							content = '';

						//add to the list
						modifiedNodes.push({
							id: id,
							content: content
						});
					}
				}
			}
		});

		//send changes
		if (addedNodes.length || removedNodes.length || modifiedNodes.length) {
			reference.invokeMethodAsync('OnContentModified', addedNodes, removedNodes, modifiedNodes).then(() => {
				for (let i = 0; i < actions.length; i++)
					actions[i]();
			});
		}

		//ignore changes by Blazor
		if (mutations.some(m => m.attributeName === 'data-refreshed')
			|| editor.getAttribute('data-refreshed')) {
			return;
		}
	});

	observer.observe(editor, { attributes: true, childList: true, subtree: true, characterData: true });

	//editor.addEventListener('keydown', function (e) {
	//	if (e.keyCode === 13) {
	//		var selection = window.getSelection();
	//		if (selection.type === 'Caret') {
	//			//get the current line index
	//			var lineIndex = 0;
	//			for (var current = selection.focusNode; current !== editor; current = current.parentElement) {
	//				if (current.parentElement === editor)
	//					lineIndex = Array.prototype.indexOf.call(current.parentElement.children, current);
	//			}

	//			//create a new line
	//			reference.invokeMethodAsync('InsertNewLine', lineIndex + 1).then(() => {
	//				//get the new line
	//				var newLine = editor.children[lineIndex + 1];

	//				//focus the new line
	//				var range = document.createRange();
	//				range.setStart(newLine, 0);
	//				range.collapse(true);
	//				selection.removeAllRanges();
	//				selection.addRange(range);
	//			});
	//		}

	//		e.preventDefault();
	//	}
	//});

	//var observer = new MutationObserver(function (mutations) {
	//	//ignore changes by Blazor
	//	if (mutations.some(m => m.attributeName === 'data-refreshed')
	//		|| editor.getAttribute('data-refreshed')) {
	//		return;
	//	}

	//	console.log(mutations);

	//	//path finding function
	//	const findPath = function (node) {
	//		var result = [];

	//		for (var current = node; current !== editor; current = current.parentElement) {
	//			var parent = current.parentElement;

	//			var id = current.getAttribute ? current.getAttribute('data-node-id') : null;
	//			if (!id)
	//				id = Array.prototype.indexOf.call(parent.children, current);

	//			result.unshift(id.toString());
	//		}

	//		return result;
	//	}

	//	if (modifications.length == 0)
	//		return;

	//	//store the selection
	//	var selection = window.getSelection();
	//	var focusOffset = selection.focusOffset;
	//	var resetSelection = () => {
	//		var range = document.createRange();
	//		range.setStart(selection.focusNode, focusOffset);
	//		selection.removeAllRanges();
	//		selection.addRange(range);
	//		console.log("selection: " + focusOffset);
	//	};

	//	//editor.setAttribute('data-refreshed', '1');
	//	reference.invokeMethodAsync('OnContentModified', modifications).then(() => {
	//		//reset the selection
	//		resetSelection();

	//		updateAfterNodes.forEach(entry => {
	//			//store the selection
	//			var selection = window.getSelection();
	//			var focusOffset = selection.focusOffset;

	//			//correct the node's text
	//			entry.node.innerText = entry.text;

	//			//reset the selection
	//			var range = document.createRange();
	//			range.setStart(selection.focusNode, focusOffset);
	//			selection.removeAllRanges();
	//			selection.addRange(range);
	//		});
	//	});

	//	if (!serverSide) {
	//		resetSelection();
	//	}
	//});

	//editor.removeAttribute('data-refreshed');
	//observer.observe(editor, { attributes: true, childList: true, subtree: true, characterData: true });

	//editor.innerHTML = content.innerHTML;
}

function editorRenderingFinished(content, editor) {
	//editor.removeAttribute('data-refreshed');

	//editor.innerHTML = content.innerHTML;
}