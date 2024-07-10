function startScrollSpy(element) {
	var scrollSpy = bootstrap.ScrollSpy.getInstance(document.body);
	if (scrollSpy) {
		if (scrollSpy._config.target === element)
			return;

		scrollSpy.dispose();
	}

	scrollSpy = new bootstrap.ScrollSpy(document.body, {
		target: element
	});
}

function registerBeforeInput(element, reference) {
	var existingReference = element['data-reference'];
	if (existingReference === reference) {
		return;
	} else if (existingReference) {
		var existingListener = element['data-listener'];
		if (existingListener) {
			element.removeEventListener('beforeinput', existingListener);
		}
	}

	var listener = function (event) {
		handleBeforeInput(element, reference, event);
	};
	element['data-reference'] = reference;
	element['data-listener'] = listener;

	element.addEventListener('beforeinput', listener);
}

function handleBeforeInput(element, reference, event) {
	event.preventDefault();
	event.stopPropagation();

	//get data
	var data = event.data;
	if (data === null && event.dataTransfer) {
		data = event.dataTransfer.getData('text');
	}

	//get selection
	var originalSelection = getSelection();
	var originalRange = originalSelection.rangeCount == 0 ? null : originalSelection.getRangeAt(0);
	var selectionRange = getSelectionRange(originalSelection, element, function (node) {
		return node && node.classList && node.classList.contains('line');
	});
	var selectionStartLine = getLineId(selectionRange.start.node);
	var selectionEndLine = getLineId(selectionRange.end.node);
	var selection = {
		start: {
			metaline: selectionStartLine.metaline,
			line: selectionStartLine.line,
			offset: selectionRange.start.offset
		},
		end: {
			metaline: selectionEndLine.metaline,
			line: selectionEndLine.line,
			offset: selectionRange.end.offset
		}
	};

	//handle event
	document.getSelection().removeAllRanges();
	reference.invokeMethodAsync('OnBeforeInput', {
		inputType: event.inputType,
		data: data,
		selection: selection,
	}).then(function (result) {
		if (result.selection) {
			//set new range
			setSelectionRange(element, result.selection.metaline, result.selection.lineId, result.selection.lineIndex, result.selection.range);
		} else {
			//restore old range
			getSelection().addRange(originalRange);
		}
	});
}

function getLineId(node) {
	var metalineId;
	var lineId;
	for (; node; node = node.parentElement) {
		if (metalineId == null)
			metalineId = node.getAttribute('data-metaline');
		if (lineId == null)
			lineId = node.getAttribute('data-line-index');

		if (metalineId && lineId)
			return {
				metaline: metalineId,
				line: parseInt(lineId)
			};
	}
}

function getSelectionRange(selection, wrapper, elementCondition) {
	function getNodeAndOffset(wrapper, elementCondition, node, offset) {
		if (!offset)
			offset = 0;

		for (; !elementCondition(node); node = node.parentElement) {
			for (var current = node.previousSibling; current; current = current.previousSibling) {
				if (current.nodeType == Node.COMMENT_NODE)
					continue;

				offset += current.textContent?.length || 0;
			}
		}

		return {
			node: node,
			offset: offset
		};
	};

	if (selection.anchorNode === selection.extentNode && elementCondition(selection.anchorNode)) {
		return {
			start: {
				node: selection.anchorNode,
				offset: selection.anchorOffset,
			},
			end: {
				node: selection.extentNode,
				offset: selection.extentOffset,
			}
		};
	}

	if (selection.rangeCount == 0)
		return null;
	var range = selection.getRangeAt(0);

	if (!wrapper.contains(range.startContainer) || !wrapper.contains(range.endContainer))
		return null;

	var start = getNodeAndOffset(wrapper, elementCondition, range.startContainer, range.startOffset);
	var end = getNodeAndOffset(wrapper, elementCondition, range.endContainer, range.endOffset);
	
	return {
		start: start,
		end: end
	};
}

function setSelectionRange(wrapper, metaline, lineId, lineIndex, selectionRange) {
	//find metaline
	var metalineElement = wrapper.querySelector('.metaline[data-metaline="' + metaline + '"]');

	//find line
	var lineElement;
	if (lineId != null) {
		lineElement = metalineElement.querySelector('.line[data-line-index="' + lineId + '"]');
	} else {
		var lines = metalineElement.querySelectorAll('.line');
		lineElement = lineIndex < 0 ? lines[lines.length + lineIndex] : lines[lineIndex];
	}

	//find selection anchors
	function findNodeAndOffset(element, offset) {
		if (offset < 0)
			return findNodeAndOffsetFromEnd(element, -offset - 1);
		else
			return findNodeAndOffsetFromStart(element, offset);
	}
	function findNodeAndOffsetFromStart(element, offsetFromStart) {
		var currentOffsetFromStart = 0;
		for (var i = 0; i < element.childNodes.length; i++) {
			var child = element.childNodes[i];
			if (child.nodeType == Node.COMMENT_NODE)
				continue;

			var afterOffset = currentOffsetFromStart + child.textContent.length;
			if (offsetFromStart < afterOffset)
				return findNodeAndOffset(child, offsetFromStart - currentOffsetFromStart);

			//end of content?
			if (afterOffset == offsetFromStart && i == element.childNodes.length - 1)
				return findNodeAndOffset(child, offsetFromStart - currentOffsetFromStart);

			currentOffsetFromStart = afterOffset;
		}

		return {
			node: element,
			offset: offsetFromStart
		};
	}
	function findNodeAndOffsetFromEnd(element, offsetFromEnd) {
		var currentOffsetFromEnd = 0;
		for (var i = element.childNodes.length - 1; i >= 0; i--) {
			var child = element.childNodes[i];
			var beforeOffset = currentOffsetFromEnd + child.textContent.length;
			if (offsetFromEnd > beforeOffset)
				return findNodeAndOffsetFromEnd(child, offsetFromEnd - currentOffsetFromEnd);

			//start of content?
			if (beforeOffset == offsetFromEnd && i == 0)
				return findNodeAndOffsetFromEnd(child, offsetFromEnd - currentOffsetFromEnd);

			currentOffsetFromStart = beforeOffset;
		}

		return {
			node: element,
			offset: element.textContent.length - offsetFromEnd
		};
	}

	var start = findNodeAndOffset(lineElement, selectionRange.start);
	var end = selectionRange.start == selectionRange.end ? start : findNodeAndOffset(lineElement, selectionRange.end);

	//set selection
	var range = new Range();
	range.setStart(start.node, start.offset);
	range.setEnd(end.node, end.offset);

	document.getSelection().addRange(range);
}



//global event handlers
window.addEventListener('load', function () {
	var dragTargets = new Set();

	document.documentElement.addEventListener('dragenter', function (e) {
		document.documentElement.classList.add('dragover');
		dragTargets.add(e.target);

		console.log('dragenter');
		console.log(e.target);
	});

	document.documentElement.addEventListener('dragleave', function (e) {
		dragTargets.delete(e.target);

		if (dragTargets.size == 0) {
			document.documentElement.classList.remove('dragover');
		}

		console.log('dragleave');
		console.log(e.target);
	});

	document.documentElement.addEventListener('drop', function (e) {
		dragTargets.clear()
		document.documentElement.classList.remove('dragover');
	});
});






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