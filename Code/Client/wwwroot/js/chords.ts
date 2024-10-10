declare class BlazorDotNetReference {
	invokeMethod<T = any>(method: string, ...args: any[]): T;
	invokeMethodAsync<T = any>(method: string, ...args: any[]): Promise<T>;
}

declare const bootstrap: any;

declare interface NodeSelectionAnchor {
	node: Node,
	offset: number,
}

declare interface NodeSelection {
	start: NodeSelectionAnchor,
	end: NodeSelectionAnchor,
}

declare interface SimpleInputEvent {
	inputType: string,
	data: string,
	dataTransfer: DataTransfer,
}

declare interface JsMetalineEditResult {
	success: boolean,
	willRender: boolean,
	selection?: JsMetalineSelectionRange,
	failReason?: {
		label: string,
		code: string,
	},
}

declare interface JsMetalineSelectionRange {
	metaline: string,
	lineId?: number,
	lineIndex?: number,
	range: {
		start: number,
		end: number,
	},
}

var supportsSynchronousInvoke = false;

function enableSynchronousInvoke() {
	supportsSynchronousInvoke = true;
}

function invokeBlazor<T extends any>(reference: BlazorDotNetReference, method: string, ...args: any[]): Promise<T> {
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

function hideAllOffcanvases(): void {
	document.querySelectorAll('.offcanvas').forEach(offcanvasEl => bootstrap.Offcanvas.getInstance(offcanvasEl)?.hide());
}

function startScrollSpy(element: HTMLElement): void {
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

function saveAsFile(data: Uint8Array, filename: string): void {
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

function registerDropDownHandler(element: HTMLElement, reference: BlazorDotNetReference): void {
	element.addEventListener('show.bs.dropdown', function (event) {
		reference.invokeMethodAsync('OnDropDownShow');
	});

	element.addEventListener('hidden.bs.dropdown', function (event) {
		reference.invokeMethodAsync('OnDropDownHidden');
	});
}

function showToast(message: string, title: string, delay: number): void {
	//get container
	var toastContainer = document.querySelector('.toast-container')!;

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




function registerResize(element: HTMLElement, reference: BlazorDotNetReference, callbackName: string): Destructible {
	var timeout: number;
	
	var handler = function () {
		//get character width
		var characterWidth = element.querySelector('.calculator')!.getBoundingClientRect().width;

		//get line offset
		var line = element.querySelector('.line');
		var lineOffsetX = 0;
		if (line) {
			//find element's child containing the line
			let topParent = line;
			for (; topParent.parentElement != element; topParent = topParent.parentElement);

			//line offset is the difference between the line's width and the parent's width
			lineOffsetX = topParent.getBoundingClientRect().width - line.getBoundingClientRect().width;
		}
		
		//how many characters does the element fit?
		var elementRect = element.getBoundingClientRect();
		var characters = Math.floor((elementRect.width - lineOffsetX) / characterWidth) || 0;

		//has the number of characters changed?
		if ('' + characters == element.getAttribute('data-characters')) {
			return;
		} else {
			element.setAttribute('data-characters', '' + characters);
			(element.style as any)['--characters'] = characters;
		}

		//wait for a short while before invoking the callback
		clearTimeout(timeout);
		timeout = setTimeout(function () {
			//handle resize
			invokeBlazor(reference, callbackName, characters);
		}, 20);
	};

	let observer = new ResizeObserver(handler);
	observer.observe(element);
	handler();
	return {
		destroy: () => {
			observer.disconnect();
		},
	};
}





var promiseAfterRender: Promise<any> = null;
var resolveAfterRender: (arg?: any) => void = null;
function notifyRenderFinished(componentName?: string) {
	if (componentName)
		console.log("rerender: " + componentName);

	if (resolveAfterRender) {
		var resolve = resolveAfterRender;
		resolveAfterRender = null;
		resolve();
		console.log("rerender action executed");
	}
}
function invokeAfterRender(action: () => any) {
	if (!resolveAfterRender) {
		promiseAfterRender = new Promise(function (resolve) {
			resolveAfterRender = resolve;
		});
	}

	return promiseAfterRender = promiseAfterRender.then(action);
}
function isWaitingForRender() {
	return resolveAfterRender != null;
}




function registerChordEditor(wrapper: HTMLElement, reference: BlazorDotNetReference, callbackName: string) {
	//prevent double registration
	var existingReference = (wrapper as any)['data-reference'];
	if (existingReference === reference) {
		return;
	} else if (existingReference) {
		var existingListener = (wrapper as any)['data-listener'];
		if (existingListener) {
			wrapper.removeEventListener('beforeinput', existingListener);
		}
	}

	//create action queue
	let actionQueue = new ActionQueue();
	
	//create editor wrapper (delayed)
	let handler: (event: Event) => void;
	let editor: ModificationEditor = null;
	let selectionObserver: SelectionObserver = null;
	handler = (event) => {
		wrapper.removeEventListener('focus', handler);
		createEditor();
	};
	wrapper.addEventListener('focus', handler);

	//create return object
	return {
		//notifyBeforeRender: editor.stopRevertingModifications.bind(editor),
		notifyAfterRender: actionQueue.notifyRender.bind(actionQueue),
		destroy: () => {
			editor?.destroy();
			selectionObserver?.destroy();
		}
	};

    function createEditor() {
        let afterRender: () => void;
        const callback: EditorCallback = (editor, data, selectionRange, expectRender) => {
            let result: MetalineEditResult;
            actionQueue.then(() => {
                //prepare event data
                var selection = editor.getCurrentSelection();
                var eventData = {
                    selection: selection,
                    editRange: data.editRange,
                    ...data
                };

                //hide caret
                wrapper.classList.add('refreshing');
                selectionRange.collapse(false);

                //a new render is coming
                actionQueue.prepareForNextRender();
                afterRender = expectRender();

                //invoke the callback
                console.log("invoke Blazor");
                return invokeBlazor<MetalineEditResult>(reference, callbackName, eventData);
            }).then(r => {
                console.log("check result");

                //store result
                result = r;

                //show error
                if (result.failReason)
                    showToast(`${result.failReason.label} (${result.failReason.code})`, 'Bearbeitungsfehler', 5000);

                //no render coming (anymore)?
                if (!result.willRender) {
                    //trigger render
                    actionQueue.notifyRender();
                } else {
                    //await render
                    return actionQueue.awaitRender();
                }
            }).then(() => {
                console.log("after render");

                //rendered
                afterRender();

				let selection: Selection;
				if (result.selection && data.inputType != 'deleteByDrag') {
                    //set new selection range
                    selection = editor.setCurrentSelection(result.selection);
                } else if (selectionRange) {
                    //restore old range
                    selection = getSelection();
                    if (selection.rangeCount == 1) {
                        var currentSelectionRange = selection.getRangeAt(0);
                        currentSelectionRange.setStart(selectionRange.startContainer, selectionRange.startOffset);
						currentSelectionRange.setEnd(selectionRange.endContainer, selectionRange.endOffset);
                    } else {
                        selection.removeAllRanges();
                        selection.addRange(selectionRange);
                    }
				}

				//scroll the selection into view
				for (var focusElement = selection.focusNode; focusElement && !('scrollIntoView' in focusElement); focusElement = focusElement.parentElement) { }
				if (focusElement) {
					(focusElement as HTMLElement).scrollIntoView({
						block: 'nearest',
						inline: 'nearest'
					});
				}

                //show caret
                wrapper.classList.remove('refreshing');
            });
        };
		editor = new ModificationEditor(wrapper, callback);
		selectionObserver = new SelectionObserver(editor);
    }
}





















function attachmentStartDrag(event: DragEvent) {
	//get the chord's position
	var attachmentElement = (event.target as HTMLElement).parentElement;
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

function checkIsLine(node: Node): LineIdentifier {
	if (!node || !('classList' in node))
		return null;

	let element = <HTMLElement>node;

	var lineId = null;
	if (element.classList.contains('line')) {
		lineId = parseInt(element.getAttribute('data-line-index'));
		element = element.parentElement.parentElement;
	} else if (element.classList.contains('metaline-lines')) {
		element = element.parentElement;
	} else if (!element.classList.contains('metaline')) {
		return null;
	}

	if (lineId === null) {
		//take the first line ID
		var line = element.querySelector('.line');
		if (line) {
			lineId = parseInt(line.getAttribute('data-line-index'));
		} else {
			lineId = 0;
		}
	}

	var metalineId = element.getAttribute('data-metaline');
	return {
		metaline: metalineId,
		line: lineId
	};
}

function getLineAndOffset(node: Node, offset: number): MetalineLineAnchor {
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

function getSelectionRange(selection: Selection, wrapper: HTMLElement): AnchorSelection<MetalineLineAnchor> {
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

function setSelectionRange(wrapper: HTMLElement, metaline: string, lineId: number | null, lineIndex: number, selectionRange: {start: number, end: number}) {
	//find metaline
	var metalineElement = wrapper.querySelector('.metaline[data-metaline="' + metaline + '"]');

	//find line
	var lineElement: HTMLElement;
	if (lineId != null) {
		lineElement = metalineElement?.querySelector('.line[data-line-index="' + lineId + '"]');
	} else {
		var lines = metalineElement?.querySelectorAll('.line');
		lineElement = lineIndex < 0 ? <HTMLElement>lines[lines.length + lineIndex] : <HTMLElement>lines[lineIndex];
	}

	//no line?
	if (!lineElement)
		return false;

	var start = findNodeAndOffset(lineElement, selectionRange.start);
	var end = selectionRange.start == selectionRange.end ? start : findNodeAndOffset(lineElement, selectionRange.end);

	//set selection
	var selection = document.getSelection();
	var range: Range;
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
	for (var focusElement = selection.focusNode; focusElement && !('scrollIntoView' in focusElement); focusElement = focusElement.parentElement) { }
	if (focusElement)
		(focusElement as HTMLElement).scrollIntoView({
			block: 'nearest',
			inline: 'nearest'
		});
	return true;

	//find selection anchors
	function findNodeAndOffset(element: HTMLElement, offset: number) {
		if (offset < 0)
			return findNodeAndOffsetFromEnd(element, -offset - 1);
		else
			return findNodeAndOffsetFromStart(element, offset);
			
		function findNodeAndOffsetFromStart(element: Node, offsetFromStart: number) {
			var currentOffsetFromStart = 0;
			for (var i = 0; i < element.childNodes.length; i++) {
				var child = element.childNodes[i];
				if (child.nodeType == Node.COMMENT_NODE)
					continue;

				var afterOffset = currentOffsetFromStart + child.textContent.length;
				if (offsetFromStart < afterOffset)
					return findNodeAndOffsetFromStart(child, offsetFromStart - currentOffsetFromStart);

				//end of content?
				if (afterOffset == offsetFromStart && i == element.childNodes.length - 1)
					return findNodeAndOffsetFromStart(child, offsetFromStart - currentOffsetFromStart);

				currentOffsetFromStart = afterOffset;
			}

			return {
				node: element,
				offset: offsetFromStart
			};
		}
		function findNodeAndOffsetFromEnd(element: Node, offsetFromEnd: number) {
			var currentOffsetFromEnd = 0;

			//end?
			if (offsetFromEnd == 0) {
				if (element.childNodes.length > 0)
					return findNodeAndOffsetFromEnd(element.childNodes[element.childNodes.length - 1], 0);
				else
					return {
						node: element,
						offset: element.textContent.length
					};
			}

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
