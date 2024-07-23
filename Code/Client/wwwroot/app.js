function hideAllOffcanvases() {
	const offcanvasElementList = document.querySelectorAll('.offcanvas')
	const offcanvasList = [...offcanvasElementList].map(offcanvasEl => bootstrap.Offcanvas.getInstance(offcanvasEl));
	for (var offcanvas of offcanvasList) {
		offcanvas?.hide();
	}
}

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

function saveAsFile(data, filename) {
	const blob = new Blob([data], { type: 'application/octet-stream' });
	const url = URL.createObjectURL(blob);
	const a = document.createElement('a');
	a.style['display'] = 'none';
	a.href = url;
	a.download = filename;
	document.body.appendChild(a);
	a.click();
	document.body.removeChild(a);
	URL.revokeObjectURL(url);
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
