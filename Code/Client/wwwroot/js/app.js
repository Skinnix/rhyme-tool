var __spreadArray = (this && this.__spreadArray) || function (to, from, pack) {
    if (pack || arguments.length === 2) for (var i = 0, l = from.length, ar; i < l; i++) {
        if (ar || !(i in from)) {
            if (!ar) ar = Array.prototype.slice.call(from, 0, i);
            ar[i] = from[i];
        }
    }
    return to.concat(ar || Array.prototype.slice.call(from));
};
var supportsSynchronousInvoke = false;
function enableSynchronousInvoke() {
    supportsSynchronousInvoke = true;
}
function invokeBlazor(reference, method) {
    var args = [];
    for (var _i = 2; _i < arguments.length; _i++) {
        args[_i - 2] = arguments[_i];
    }
    if (supportsSynchronousInvoke) {
        return new Promise(function (resolve, reject) {
            var result;
            try {
                result = reference.invokeMethod.apply(reference, __spreadArray([method], args, false));
            }
            catch (error) {
                reject(error);
                return;
            }
            resolve(result);
        });
    }
    else {
        return reference.invokeMethodAsync.apply(reference, __spreadArray([method], args, false));
    }
}
function hideAllOffcanvases() {
    document.querySelectorAll('.offcanvas').forEach(function (offcanvasEl) { var _a; return (_a = bootstrap.Offcanvas.getInstance(offcanvasEl)) === null || _a === void 0 ? void 0 : _a.hide(); });
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
    var blob = new Blob([data], { type: 'application/octet-stream' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
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
    var toastContainer = document.querySelector('.toast-container');
    var toast = document.createElement('div');
    toast.classList.add('toast');
    toast.setAttribute('role', 'alert');
    toast.setAttribute('aria-live', 'assertive');
    toast.setAttribute('aria-atomic', 'true');
    toast.innerHTML = "\n<div class=\"toast-header\">\n\t<strong class=\"me-auto\">".concat(title, "</strong>\n\t<button type=\"button\" class=\"btn-close\" data-bs-dismiss=\"toast\" aria-label=\"Close\"></button>\n</div>\n<div class=\"toast-body\">").concat(message, "</div>");
    toastContainer.appendChild(toast);
    var bsToast = new bootstrap.Toast(toast, {
        animation: true,
        autohide: true,
        delay: delay || 5000
    });
    toast.addEventListener('hidden.bs.toast', function () {
        bsToast.dispose();
        toast.remove();
    });
    bsToast.show();
}
function registerResize(element, reference, callbackName) {
    var timeout;
    var handler = function () {
        var characterWidth = element.querySelector('.calculator').getBoundingClientRect().width;
        var line = element.querySelector('.line');
        var lineOffsetX = 0;
        if (line) {
            var topParent = line;
            for (; topParent.parentElement != element; topParent = topParent.parentElement)
                ;
            lineOffsetX = topParent.getBoundingClientRect().width - line.getBoundingClientRect().width;
        }
        var elementRect = element.getBoundingClientRect();
        var characters = Math.floor((elementRect.width - lineOffsetX) / characterWidth) || 0;
        if ('' + characters == element.getAttribute('data-characters')) {
            return;
        }
        else {
            element.setAttribute('data-characters', '' + characters);
            element.style['--characters'] = characters;
        }
        clearTimeout(timeout);
        timeout = setTimeout(function () {
            invokeBlazor(reference, callbackName, characters);
        }, 20);
    };
    new ResizeObserver(handler).observe(element);
    handler();
}
var promiseAfterRender = null;
var resolveAfterRender = null;
function notifyRenderFinished(componentName) {
    if (componentName)
        console.log("rerender: " + componentName);
    if (resolveAfterRender) {
        var resolve = resolveAfterRender;
        resolveAfterRender = null;
        resolve();
        console.log("rerender action executed");
    }
}
function invokeAfterRender(action) {
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
function attachmentStartDrag(event) {
    var attachmentElement = event.target.parentElement;
    var attachmentStart = getLineAndOffset(attachmentElement, 0);
    var attachmentLength = attachmentElement.textContent.length;
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
    event.dataTransfer.setData('text', attachmentElement.textContent);
}
function registerBeforeInput(wrapper, reference, callbackName) {
    var existingReference = wrapper['data-reference'];
    if (existingReference === reference) {
        return;
    }
    else if (existingReference) {
        var existingListener = wrapper['data-listener'];
        if (existingListener) {
            wrapper.removeEventListener('beforeinput', existingListener);
        }
    }
    var currentRequest = null;
    wrapper['data-reference'] = reference;
    wrapper['data-listener'] = handleBeforeInput;
    if (navigator.userAgent.includes('Chrome')) {
        var selectionRange;
        var inputEvent;
        var observing = 0;
        var observer = new MutationObserver(function (mutations) {
            observing--;
            if (!observing)
                observer.disconnect();
            var mutations = Array.from(mutations.reverse());
            while (mutations.length != 0) {
                for (var m = 0; m < mutations.length; m++) {
                    var mutation = mutations[m];
                    if (mutation.type == 'childList') {
                        if (mutation.removedNodes.length > 0) {
                            if (mutation.nextSibling) {
                                if (!mutation.target.contains(mutation.nextSibling))
                                    continue;
                                for (var i = 0; i < mutation.removedNodes.length; i++) {
                                    var node = mutation.removedNodes[i];
                                    mutation.target.insertBefore(node, mutation.nextSibling);
                                }
                            }
                            else {
                                for (var i = 0; i < mutation.removedNodes.length; i++) {
                                    var node = mutation.removedNodes[i];
                                    mutation.target.appendChild(node);
                                }
                            }
                        }
                        if (mutation.addedNodes.length > 0) {
                            var found = true;
                            for (var j = 0; j < mutation.addedNodes.length; j++) {
                                var node = mutation.addedNodes[j];
                                if (!node.parentElement) {
                                    found = false;
                                    break;
                                }
                            }
                            if (!found)
                                continue;
                            for (var j = 0; j < mutation.addedNodes.length; j++) {
                                var node = mutation.addedNodes[j];
                                node.parentElement.removeChild(node);
                            }
                        }
                    }
                    else if (mutation.type == 'characterData') {
                        mutation.target.nodeValue = mutation.oldValue;
                    }
                    mutations.splice(m, 1);
                    m--;
                }
            }
            var selection = getSelection();
            selection.removeAllRanges();
            if (selectionRange) {
                var range = document.createRange();
                range.setStart(selectionRange.start.node, selectionRange.start.offset);
                range.setEnd(selectionRange.end.node, selectionRange.end.offset);
                selection.addRange(range);
            }
            if (inputEvent) {
                var event = inputEvent;
                inputEvent = null;
                handleBeforeInput(event);
            }
        });
        wrapper.addEventListener('beforeinput', function (event) {
            inputEvent = event;
            var selection = getSelection();
            if (selection.rangeCount == 0) {
                selectionRange = null;
            }
            else {
                var range = getSelection().getRangeAt(0);
                selectionRange = {
                    start: {
                        node: range.startContainer,
                        offset: range.startOffset
                    },
                    end: {
                        node: range.endContainer,
                        offset: range.endOffset
                    }
                };
            }
            if (observing) {
                observing++;
                console.log("observing: " + observing);
            }
            else {
                observing++;
                observer.observe(wrapper, { childList: true, subtree: true, characterData: true, characterDataOldValue: true });
            }
        });
    }
    else {
        wrapper.addEventListener('beforeinput', handleBeforeInput);
    }
    function handleBeforeInput(event) {
        event.preventDefault();
        event.stopPropagation();
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
        var inputType = event.inputType;
        if (currentRequest) {
            currentRequest = currentRequest.then(function () {
                if (isWaitingForRender()) {
                    return invokeAfterRender(function () {
                        sendInputEvent(inputType, content, dragSelection);
                    });
                }
                else {
                    return sendInputEvent(inputType, content, dragSelection);
                }
            });
        }
        else {
            currentRequest = sendInputEvent(inputType, content, dragSelection);
        }
        return false;
    }
    function sendInputEvent(inputType, content, dragSelection) {
        var originalSelection = getSelection();
        var originalRange = originalSelection.rangeCount == 0 ? null : originalSelection.getRangeAt(0);
        if (originalRange && inputType == 'deleteContent' || inputType == 'deleteContentBackward' || inputType == 'deleteContentForward') {
            var content = originalRange.toString();
            if (!content || content == "\n") {
                if (inputType == 'deleteContentForward') {
                    originalRange.setEnd(originalRange.startContainer, originalRange.startOffset);
                }
                else {
                    originalRange.setStart(originalRange.endContainer, originalRange.endOffset);
                }
            }
        }
        var copyRange = originalRange.cloneRange();
        var selection = getSelectionRange(originalSelection, wrapper);
        if (selection.start.line != selection.end.line && originalRange.toString() === '') {
            if (inputType == 'deleteContent' || inputType == 'deleteContentBackward') {
                selection.start = selection.end;
            }
            else if (inputType == 'deleteContentForward') {
                selection.end = selection.start;
            }
        }
        if (inputType == 'insertText' && content == '') {
            inputType = 'deleteContent';
        }
        wrapper.classList.add('refreshing');
        originalRange.collapse(true);
        if (!isWaitingForRender()) {
            invokeAfterRender(function () { });
        }
        if (dragSelection) {
            return invokeLineEdit(reference, callbackName, 'deleteByDrag', null, dragSelection, wrapper, null, true).then(function () {
                return invokeLineEdit(reference, callbackName, inputType, content, selection, wrapper, copyRange);
            });
        }
        else {
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
        var oldRange = copyRange;
        var afterRender = function () {
            if (!ignoreSelection) {
                if (result.selection) {
                    setSelectionRange(wrapper, result.selection.metaline, result.selection.lineId, result.selection.lineIndex, result.selection.range);
                }
                else if (oldRange) {
                    var selection = getSelection();
                    selection.removeAllRanges();
                    selection.addRange(oldRange);
                }
                wrapper.classList.remove('refreshing');
            }
        };
        if (result.failReason)
            showToast("".concat(result.failReason.label, " (").concat(result.failReason.code, ")"), 'Bearbeitungsfehler', 5000);
        if (!result.willRender || supportsSynchronousInvoke) {
            afterRender();
            notifyRenderFinished();
        }
        else {
            return invokeAfterRender(afterRender);
        }
    });
}
function getLineId(node) {
    var metalineId;
    var lineId;
    for (; node; node = node.parentElement) {
        if (!('getAttribute' in node))
            continue;
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
    if (!node || !('classList' in node))
        return null;
    var element = node;
    var lineId = null;
    if (element.classList.contains('line')) {
        lineId = parseInt(element.getAttribute('data-line-index'));
        element = element.parentElement.parentElement;
    }
    else if (element.classList.contains('metaline-lines')) {
        element = element.parentElement;
    }
    else if (!element.classList.contains('metaline')) {
        return null;
    }
    if (lineId === null) {
        var line = element.querySelector('.line');
        if (line) {
            lineId = parseInt(line.getAttribute('data-line-index'));
        }
        else {
            lineId = 0;
        }
    }
    var metalineId = element.getAttribute('data-metaline');
    return {
        metaline: metalineId,
        line: lineId
    };
}
function getLineAndOffset(node, offset) {
    var _a;
    if (!offset)
        offset = 0;
    var lineInfo = checkIsLine(node);
    for (; !lineInfo; node = node.parentElement, lineInfo = checkIsLine(node)) {
        for (var current = node.previousSibling; current; current = current.previousSibling) {
            if (current.nodeType == Node.COMMENT_NODE)
                continue;
            offset += ((_a = current.textContent) === null || _a === void 0 ? void 0 : _a.length) || 0;
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
    var metalineElement = wrapper.querySelector('.metaline[data-metaline="' + metaline + '"]');
    var lineElement;
    if (lineId != null) {
        lineElement = metalineElement === null || metalineElement === void 0 ? void 0 : metalineElement.querySelector('.line[data-line-index="' + lineId + '"]');
    }
    else {
        var lines = metalineElement === null || metalineElement === void 0 ? void 0 : metalineElement.querySelectorAll('.line');
        lineElement = lineIndex < 0 ? lines[lines.length + lineIndex] : lines[lineIndex];
    }
    if (!lineElement)
        return false;
    var start = findNodeAndOffset(lineElement, selectionRange.start);
    var end = selectionRange.start == selectionRange.end ? start : findNodeAndOffset(lineElement, selectionRange.end);
    var selection = document.getSelection();
    var range;
    if (selection.rangeCount == 1) {
        var range = selection.getRangeAt(0);
        range.setStart(start.node, start.offset);
        range.setEnd(end.node, end.offset);
    }
    else {
        if (selection.rangeCount > 0)
            selection.removeAllRanges();
        var range = new Range();
        range.setStart(start.node, start.offset);
        range.setEnd(end.node, end.offset);
        document.getSelection().addRange(range);
    }
    for (var focusElement = selection.focusNode; focusElement && !('scrollIntoView' in focusElement); focusElement = focusElement.parentElement) { }
    if (focusElement)
        focusElement.scrollIntoView({
            block: 'nearest',
            inline: 'nearest'
        });
    return true;
    function findNodeAndOffset(element, offset) {
        if (offset < 0)
            return findNodeAndOffsetFromEnd(element, -offset - 1);
        else
            return findNodeAndOffsetFromStart(element, offset);
        function findNodeAndOffsetFromStart(element, offsetFromStart) {
            var currentOffsetFromStart = 0;
            for (var i = 0; i < element.childNodes.length; i++) {
                var child = element.childNodes[i];
                if (child.nodeType == Node.COMMENT_NODE)
                    continue;
                var afterOffset = currentOffsetFromStart + child.textContent.length;
                if (offsetFromStart < afterOffset)
                    return findNodeAndOffsetFromStart(child, offsetFromStart - currentOffsetFromStart);
                if (afterOffset == offsetFromStart && i == element.childNodes.length - 1)
                    return findNodeAndOffsetFromStart(child, offsetFromStart - currentOffsetFromStart);
                currentOffsetFromStart = afterOffset;
            }
            return {
                node: element,
                offset: offsetFromStart
            };
        }
        function findNodeAndOffsetFromEnd(element, offsetFromEnd) {
            var currentOffsetFromEnd = 0;
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
        dragTargets.clear();
        document.documentElement.classList.remove('dragover');
    });
});
//# sourceMappingURL=app.js.map