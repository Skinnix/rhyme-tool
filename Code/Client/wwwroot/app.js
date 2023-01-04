function initializeEditor(content, editor, reference, serverSide) {
	editor.addEventListener('keydown', function (e) {
		if (e.keyCode === 13) {
			var selection = window.getSelection();
			if (selection.type === 'Caret') {
				//get the current line index
				var lineIndex = 0;
				for (var current = selection.focusNode; current !== editor; current = current.parentElement) {
					if (current.parentElement === editor)
						lineIndex = Array.prototype.indexOf.call(current.parentElement.children, current);
				}

				//create a new line
				reference.invokeMethodAsync('InsertNewLine', lineIndex + 1).then(() => {
					//get the new line
					var newLine = editor.children[lineIndex + 1];

					//focus the new line
					var range = document.createRange();
					range.setStart(newLine, 0);
					range.collapse(true);
					selection.removeAllRanges();
					selection.addRange(range);
				});
			}

			e.preventDefault();
		}
	});

	var observer = new MutationObserver(function (mutations) {
		//ignore changes by Blazor
		if (mutations.some(m => m.attributeName === 'data-refreshed')
			|| editor.getAttribute('data-refreshed')) {
			return;
		}

		console.log(mutations);

		//path finding function
		const findPath = function (node) {
			var result = [];

			for (var current = node; current !== editor; current = current.parentElement) {
				var parent = current.parentElement;

				var id = current.getAttribute ? current.getAttribute('data-node-id') : null;
				if (!id)
					id = Array.prototype.indexOf.call(parent.children, current);

				result.unshift(id.toString());
			}

			return result;
		}

		//process modifications
		var modifications = [];
		var updateAfterNodes = [];
		mutations.forEach(mutation => {
			var addedNodes = [...mutation.addedNodes];
			var removedNodes = [...mutation.removedNodes];

			//if the same text was added and removed, ignore the mutation
			for (var i = 0; i < addedNodes.length; i++) {
				var addedNode = addedNodes[i];

				//look for a matching removed node
				for (var j = 0; j < removedNodes.length; j++) {
					var removedNode = removedNodes[j];
					if (removedNode.innerText === addedNode.innerText) {
						//remove both
						addedNodes.splice(i, 1);
						removedNodes.splice(j, 1);
						i--;
						break;
					}
				}
			}

			//added nodes
			for (var i = 0; i < addedNodes.length; i++) {
				var node = addedNodes[i];
				if (typeof node.attributes === 'undefined')
					node = node.parentElement;

				//ignore BR-elements
				if (node?.tagName == 'BR')
					continue;

				//ignore nodes which already have an ID
				var id = node?.getAttribute('data-node-id');
				if (id) {
					//add to modifications
					modifications.push({
						action: 'change',
						id: id,
						content: node.innerText
					});

					//remove the text again afterwards (otherwise Blazor would duplicate it)
					updateAfterNodes.push({
						node: node,
						text: node.innerText
					});
				} else {
					//find path
					var path = findPath(node);

					//add to modifications
					modifications.push({
						action: 'add',
						path: path,
						tag: node.tagName,
						content: node.innerText
					});
				}
			}

			//removed nodes
			for (var i = 0; i < removedNodes.length; i++) {
				var node = removedNodes[i];
				if (typeof node.attributes === 'undefined')
					node = node.parentElement;

				//add the node back in (so Blazor can remove it again)
				var index = node.getAttribute('data-node-index');
				var nextNode = Array.prototype.find.call(mutation.target.children, child => {
					return child.getAttribute('data-node-index') == +index + 1;
				});
				var replacement = document.createElement(node.tagName);
				[...node.attributes].forEach(attribute => {
					replacement.setAttribute(attribute.name, attribute.value);
				});
				replacement = node;
				if (nextNode)
					mutation.target.insertBefore(replacement, nextNode);
				else
					mutation.target.appendChild(replacement);

				//ignore nodes without ID
				var id = node?.getAttribute('data-node-id');
				if (id) {
					modifications.push({
						action: 'remove',
						id: id
					});
				}
			}

			//changed content
			if (mutation.target && addedNodes.length == 0 && removedNodes.length == 0) {
				var node = mutation.target;
				if (typeof node.attributes === 'undefined')
					node = node.parentElement;

				//ignore nodes without ID
				var id = node?.getAttribute('data-node-id');
				if (id) {
					modifications.push({
						action: 'change',
						id: id,
						content: node.innerText
					});
				}
			}
		});

		if (modifications.length == 0)
			return;

		//store the selection
		var selection = window.getSelection();
		var focusOffset = selection.focusOffset;
		var resetSelection = () => {
			var range = document.createRange();
			range.setStart(selection.focusNode, focusOffset);
			selection.removeAllRanges();
			selection.addRange(range);
			console.log("selection: " + focusOffset);
		};

		//editor.setAttribute('data-refreshed', '1');
		reference.invokeMethodAsync('OnContentModified', modifications).then(() => {
			//reset the selection
			resetSelection();

			updateAfterNodes.forEach(entry => {
				//store the selection
				var selection = window.getSelection();
				var focusOffset = selection.focusOffset;

				//correct the node's text
				entry.node.innerText = entry.text;

				//reset the selection
				var range = document.createRange();
				range.setStart(selection.focusNode, focusOffset);
				selection.removeAllRanges();
				selection.addRange(range);
			});
		});

		if (!serverSide) {
			resetSelection();
		}
	});

	editor.removeAttribute('data-refreshed');
	observer.observe(editor, { attributes: true, childList: true, subtree: true, characterData: true });

	editor.innerHTML = content.innerHTML;
}

function editorRenderingFinished(content, editor) {
	editor.removeAttribute('data-refreshed');

	editor.innerHTML = content.innerHTML;
}