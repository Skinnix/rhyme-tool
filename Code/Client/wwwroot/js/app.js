var supportsSynchronousInvoke = false;

function enableSynchronousInvoke() {
	supportsSynchronousInvoke = true;
}

function invokeBlazor(reference, method, ...args) {
	if (supportsSynchronousInvoke) {
		return new Promise(function (resolve, reject) {
			var result;
			try {
				result = reference.invokeMethod(method, ...args);
			} catch (error) {
				reject(error);
				return;
			}

			resolve(result);
		});
	} else {
		return reference.invokeMethodAsync(method, ...args);
	}
}

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

function registerDropDownHandler(element, reference) {
	element.addEventListener('show.bs.dropdown', function (event) {
		reference.invokeMethodAsync('OnDropDownShow');
	});

	element.addEventListener('hidden.bs.dropdown', function (event) {
		reference.invokeMethodAsync('OnDropDownHidden');
	});
}

function showToast(message, title, delay) {
	//get container
	var toastContainer = document.querySelector('.toast-container');

	//create toast
	var toast = document.createElement('div');
	toast.classList.add('toast');
	toast.setAttribute('role', 'alert');
	toast.setAttribute('aria-live', 'assertive');
	toast.setAttribute('aria-atomic', 'true');
	toast.innerHTML = `
<div class="toast-header">
	<strong class="me-auto">${title}</strong>
	<button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>
</div>
<div class="toast-body">${message}</div>`;

	//append toast to container
	toastContainer.appendChild(toast);
	
	//create BS toast
	var bsToast = new bootstrap.Toast(toast, {
		animation: true,
		autohide: true,
		delay: delay || 5000
	});

	//remove toast when hidden
	toast.addEventListener('hidden.bs.toast', function () {
		bsToast.dispose();
		toast.remove();
	});

	//show
	bsToast.show();
}




function registerResize(element, reference, callbackName) {
	var timeout;
	
	var handler = function () {
		//get character width
		var characterWidth = element.querySelector('.calculator').getBoundingClientRect().width;

		//get line offset
		var line = element.querySelector('.line');
		var lineOffsetX = 0;
		if (line) {
			//find element's child containing the line
			var topParent = line;
			for (; topParent.parentElement != element; topParent = topParent.parentElement);

			//line offset is the difference between the line's width and the parent's width
			lineOffsetX = topParent.getBoundingClientRect().width - line.getBoundingClientRect().width;
		}
		
		//how many characters does the element fit?
		var elementRect = element.getBoundingClientRect();
		var characters = Math.floor((elementRect.width - lineOffsetX) / characterWidth) || 0;

		//has the number of characters changed?
		if (characters == element.getAttribute('data-characters')) {
			return;
		} else {
			element.setAttribute('data-characters', characters);
			element.style['--characters'] = characters;
		}

		//wait for a short while before invoking the callback
		clearTimeout(timeout);
		timeout = setTimeout(function () {
			//handle resize
			invokeBlazor(reference, callbackName, characters);
		}, 20);
	};
	new ResizeObserver(handler).observe(element);
	handler();
}


var actionAfterRender = null;
function notifyRenderFinished(componentName) {
	if (componentName)
		console.log("rerender: " + componentName);

	if (actionAfterRender) {
		var action = actionAfterRender;
		actionAfterRender = null;
		
		action();
		console.log("rerender action executed");
	}
}
function invokeAfterRender(action) {
	actionAfterRender = action;
}




function attachmentStartDrag(event) {
	//get the chord's position
	var attachmentElement = event.target.parentElement;
	var attachmentStart = getLineAndOffset(attachmentElement, 0);

	//get chord length
	var attachmentLength = attachmentElement.textContent.length;
	
	//store the chord's selection in the dataTransfer object
	var selection = {
		start: attachmentStart,
		end: {
			metaline: attachmentStart.metaline,
			line: attachmentStart.line,
			offset: attachmentStart.offset + attachmentLength
		}
	};
	event.dataTransfer.setData('text/json', JSON.stringify({
		drag: selection
	}));

	//store the chord's text in the dataTransfer object
	event.dataTransfer.setData('text', attachmentElement.textContent);
}


function registerBeforeInput(wrapper, reference, callbackName) {
	//prevent double registration
	var existingReference = wrapper['data-reference'];
	if (existingReference === reference) {
		return;
	} else if (existingReference) {
		var existingListener = wrapper['data-listener'];
		if (existingListener) {
			wrapper.removeEventListener('beforeinput', existingListener);
		}
	}

	//store current request promise and running requests
	var currentRequest = null;
	
	//store listener data
	wrapper['data-reference'] = reference;
	wrapper['data-listener'] = handleBeforeInput;

	//add listener
	wrapper.addEventListener('beforeinput', handleBeforeInput);

	function handleBeforeInput(event) {
		//stop normal event
		event.preventDefault();
		event.stopPropagation();

		//get content and additional data
		var content = event.data;
		var dragSelection = null;
		if (content === null && event.dataTransfer) {
			content = event.dataTransfer.getData('text');
			var json = event.dataTransfer.getData('text/json');
			if (json) {
				var jsonData = JSON.parse(json);
				dragSelection = jsonData.drag;
			}
		}

		//store necessary event data
		var inputType = event.inputType;

		//is there already a request?
		if (currentRequest) {
			//chain the request
			currentRequest = currentRequest.then(function () {
				return sendInputEvent(inputType, content, dragSelection);
			});
		} else {
			currentRequest = sendInputEvent(inputType, content, dragSelection);
		}
	}
	
	function sendInputEvent(inputType, content, dragSelection) {
        var originalSelection = getSelection();
		var originalRange = originalSelection.rangeCount == 0 ? null : originalSelection.getRangeAt(0);

		//mobile browsers like creating selections when deleting content
		if (originalRange && inputType == 'deleteContent' || inputType == 'deleteContentBackward' || inputType == 'deleteContentForward') {
			var content = originalRange.toString();
			if (!content) {
				//collapse the selection
				originalRange.collapse(inputType == 'deleteContentForward');
			}
		}

        var copyRange = originalRange.cloneRange();
		var selection = getSelectionRange(originalSelection, wrapper);
		
        //collapse empty, line-spanning selections
		if (originalRange.toString() === '' && selection.start.line != selection.end.line) {
            if (inputType == 'deleteContent' || inputType == 'deleteContentBackward') {
				selection.start = selection.end;
            } else if (inputType == 'deleteContentForward') {
				selection.end = selection.start;
            }
        }
		
        //hide caret
        wrapper.classList.add('refreshing');
        originalRange.collapse(true);

        //drag selection?
        if (dragSelection) {
            //invoke drag event first
            return invokeLineEdit(reference, callbackName, 'deleteByDrag', null, dragSelection, wrapper, null, true).then(function() {
                //invoke actual edit event
                invokeLineEdit(reference, callbackName, inputType, content, selection, wrapper, copyRange);
			});
        } else {
            //invoke edit event
			return invokeLineEdit(reference, callbackName, inputType, content, selection, wrapper, copyRange);
        }
    }
}

function invokeLineEdit(reference, callbackName, inputType, content, selection, wrapper, copyRange, ignoreSelection) {
    return invokeBlazor(reference, callbackName, {
        inputType: inputType,
		data: content,
        selection: selection,
	}).then(function (result) {
		if (!ignoreSelection) {
			if (result.selection) {
				if (supportsSynchronousInvoke) {
					//set new range
					setSelectionRange(wrapper, result.selection.metaline, result.selection.lineId, result.selection.lineIndex, result.selection.range);
				} else {
					//set new range as soon as rendering is done
					invokeAfterRender(function () {
						//set selection
						setSelectionRange(wrapper, result.selection.metaline, result.selection.lineId, result.selection.lineIndex, result.selection.range);

						//show caret
						wrapper.classList.remove('refreshing');
					});
					return;
				}
			} else if (copyRange) {
				//restore old range
				getSelection().addRange(copyRange);
			}

			//show caret
			wrapper.classList.remove('refreshing');
		}

        //show error
        if (result.failReason)
            showToast(`${result.failReason.label} (${result.failReason.code})`, 'Bearbeitungsfehler', 5000);
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

function checkIsLine(node) {
	if (!node || !node.classList)
		return null;

	var lineId = null;
	if (node.classList.contains('line')) {
		lineId = parseInt(node.getAttribute('data-line-index'));
		node = node.parentElement.parentElement;
	} else if (node.classList.contains('metaline-lines')) {
		node = node.parentElement;
	} else if (!node.classList.contains('metaline')) {
		return null;
	}

	if (lineId === null) {
		//take the first line ID
		var line = node.querySelector('.line');
		if (line) {
			lineId = parseInt(line.getAttribute('data-line-index'));
		} else {
			lineId = 0;
		}
	}

	var metalineId = node.getAttribute('data-metaline');
	return {
		metaline: metalineId,
		line: lineId
	};
}

function getLineAndOffset(node, offset) {
	if (!offset)
		offset = 0;

	var lineInfo = checkIsLine(node);
	for (; !lineInfo; node = node.parentElement, lineInfo = checkIsLine(node)) {
		for (var current = node.previousSibling; current; current = current.previousSibling) {
			if (current.nodeType == Node.COMMENT_NODE)
				continue;

			offset += current.textContent?.length || 0;
		}
	}

	return {
		metaline: lineInfo.metaline,
		line: lineInfo.line,
		offset: offset
	};
}

function getSelectionRange(selection, wrapper) {
	if (selection.rangeCount == 0)
		return null;
	var range = selection.getRangeAt(0);

	if (!wrapper.contains(range.startContainer) || !wrapper.contains(range.endContainer))
		return null;

	var start = getLineAndOffset(range.startContainer, range.startOffset);
	var end = range.endContainer == range.startContainer ? {
		metaline: start.metaline,
		line: start.line,
		offset: start.offset + Math.abs(range.endOffset - range.startOffset)
	} : getLineAndOffset(range.endContainer, range.endOffset);
	
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

	//no line?
	if (!lineElement)
		return false;

	var start = findNodeAndOffset(lineElement, selectionRange.start);
	var end = selectionRange.start == selectionRange.end ? start : findNodeAndOffset(lineElement, selectionRange.end);

	//set selection
	var selection = document.getSelection();
	var range;
	if (selection.rangeCount == 1) {
		var range = selection.getRangeAt(0);
		range.setStart(start.node, start.offset);
		range.setEnd(end.node, end.offset);
	} else {
		if (selection.rangeCount > 0)
			selection.removeAllRanges();

		var range = new Range();
		range.setStart(start.node, start.offset);
		range.setEnd(end.node, end.offset);
		document.getSelection().addRange(range);
	}

	//scroll the selection into view
	for (var focusElement = selection.focusNode; focusElement && !focusElement.getBoundingClientRect; focusElement = focusElement.parentElement) { }
	if (focusElement)
		focusElement.scrollIntoView({
			block: 'nearest',
			inline: 'nearest'
		});
	return true;

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

		//end?
		if (offsetFromEnd == 0 && element.childNodes.length > 0)
			return findNodeAndOffsetFromEnd(element.childNodes[element.childNodes.length - 1], 0);
		else
			return {
				node: element,
				offset: element.textContent.length
			};

		for (var i = element.childNodes.length - 1; i >= 0; i--) {
			var child = element.childNodes[i];
			var beforeOffset = currentOffsetFromEnd + child.textContent.length;
			if (offsetFromEnd > beforeOffset)
				return findNodeAndOffsetFromEnd(child, offsetFromEnd - currentOffsetFromEnd);
				
			//start of content?
			if (beforeOffset == offsetFromEnd && i == 0)
				return findNodeAndOffsetFromEnd(child, offsetFromEnd - currentOffsetFromEnd);

			currentOffsetFromEnd = beforeOffset;
		}

		return {
			node: element,
			offset: element.textContent.length - offsetFromEnd
		};
	}
}



//global event handlers
window.addEventListener('load', function () {
	var dragTargets = new Set();

	document.documentElement.addEventListener('dragenter', function (e) {
		document.documentElement.classList.add('dragover');
		dragTargets.add(e.target);
	});

	document.documentElement.addEventListener('dragleave', function (e) {
		dragTargets.delete(e.target);

		if (dragTargets.size == 0) {
			document.documentElement.classList.remove('dragover');
		}
	});

	document.documentElement.addEventListener('drop', function (e) {
		dragTargets.clear()
		document.documentElement.classList.remove('dragover');
	});
});
