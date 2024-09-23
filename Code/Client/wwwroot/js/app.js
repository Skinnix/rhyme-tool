var __assign = (this && this.__assign) || function () {
    __assign = Object.assign || function(t) {
        for (var s, i = 1, n = arguments.length; i < n; i++) {
            s = arguments[i];
            for (var p in s) if (Object.prototype.hasOwnProperty.call(s, p))
                t[p] = s[p];
        }
        return t;
    };
    return __assign.apply(this, arguments);
};
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
function registerChordEditor(wrapper, reference, callbackName) {
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
    var actionQueue = new ActionQueue();
    var editor;
    var afterRender;
    var callback = function (editor, data, selectionRange, expectRender) {
        var result;
        actionQueue.then(function () {
            var selection = editor.getCurrentSelection();
            var eventData = __assign({ selection: selection, editRange: data.editRange }, data);
            wrapper.classList.add('refreshing');
            selectionRange.collapse(false);
            actionQueue.prepareForNextRender();
            afterRender = expectRender();
            console.log("invoke Blazor");
            return invokeBlazor(reference, callbackName, eventData);
        }).then(function (r) {
            console.log("check result");
            result = r;
            if (result.failReason)
                showToast("".concat(result.failReason.label, " (").concat(result.failReason.code, ")"), 'Bearbeitungsfehler', 5000);
            if (!result.willRender) {
                actionQueue.notifyRender();
            }
            else {
                return actionQueue.awaitRender();
            }
        }).then(function () {
            console.log("after render");
            afterRender();
            if (result.selection) {
                editor.setCurrentSelection(result.selection);
            }
            else if (selectionRange) {
                var selection = getSelection();
                if (selection.rangeCount == 1) {
                    var currentSelectionRange = selection.getRangeAt(0);
                    currentSelectionRange.setStart(selectionRange.startContainer, selectionRange.startOffset);
                    currentSelectionRange.setEnd(selectionRange.endContainer, selectionRange.endOffset);
                }
                else {
                    selection.removeAllRanges();
                    selection.addRange(selectionRange);
                }
            }
            wrapper.classList.remove('refreshing');
        });
    };
    editor = new ModificationEditor(wrapper, callback);
    return {
        notifyAfterRender: actionQueue.notifyRender.bind(actionQueue),
    };
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
    wrapper['data-listener'] = prepareBeforeInput;
    var reverseAllEvents = true;
    if (navigator.userAgent.includes('Chrome') && navigator.userAgent.includes('Mobile')) {
        reverseAllEvents = true;
    }
    wrapper.addEventListener('beforeinput', prepareBeforeInput);
    wrapper.addEventListener('beforeinput', function (event) {
        console.log(event);
    });
    wrapper.addEventListener('compositionstart', function (event) {
        wrapper.setAttribute('readonly', 'true');
    });
    wrapper.addEventListener('compositionupdate', function (event) {
        setTimeout(function () {
            wrapper.removeAttribute('readonly');
        }, 10);
    });
    wrapper.addEventListener('compositionend', function (event) {
        console.log(event);
    });
    var observations = [];
    var observer = new MutationObserver(function (mutations) {
        var observation = observations.shift();
        if (!observations.length)
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
        if (observation === null || observation === void 0 ? void 0 : observation.selection) {
            var range = document.createRange();
            range.setStart(observation.selection.start.node, observation.selection.start.offset);
            range.setEnd(observation.selection.end.node, observation.selection.end.offset);
            selection.addRange(range);
        }
        if (observation === null || observation === void 0 ? void 0 : observation.inputEvent) {
            handleBeforeInput(observation.inputEvent, true);
        }
    });
    function observeAndReverseInput(event) {
        var inputEvent = event;
        var currentSelection = getSelection();
        var selection;
        if (currentSelection.rangeCount == 0) {
            selection = null;
        }
        else {
            var range = currentSelection.getRangeAt(0);
            selection = {
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
        var wasObserving = observations.length;
        observations.push({
            inputEvent: inputEvent,
            selection: selection,
        });
        if (wasObserving) {
            console.log("observing: " + observations.length);
        }
        else {
            console.log("start observing");
            observer.observe(wrapper, { childList: true, subtree: true, characterData: true, characterDataOldValue: true });
        }
    }
    function prepareBeforeInput(event) {
        console.log("input event: ".concat(event.inputType, ", data: ").concat(event.data));
        if (reverseAllEvents || !event.cancelable) {
            observeAndReverseInput(event);
            return true;
        }
        event.preventDefault();
        event.stopPropagation();
        handleBeforeInput(event, false);
    }
    function handleBeforeInput(event, isReversed) {
        console.log("input event: ".concat(event.inputType, ", data: ").concat(event.data));
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
        if (inputType == 'insertCompositionText') {
            var currentContent = originalRange.toString();
            if (content.startsWith(currentContent)) {
                inputType = 'insertText';
                originalRange.collapse(false);
                selection.start = selection.end;
            }
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
function registerModificationEditor(editor, reference, callbackName) {
    var callback = function (editor, data) {
        var selection = editor.getCurrentSelection();
        var eventData = __assign({ selection: selection, editRange: data.editRange }, data);
        setTimeout(function () {
            reference.invokeMethodAsync(callbackName, eventData).then(function (result) {
                editor.setCurrentSelection(result.selection);
            });
        }, 0);
    };
    return new ModificationEditor(editor, callback);
}
var Debouncer = (function () {
    function Debouncer(timeout) {
        this.timeout = timeout;
    }
    Debouncer.prototype.debounce = function (handler, debounce) {
        var _this = this;
        if (this.interval) {
            console.log("debounce: bounced");
            clearInterval(this.interval);
            this.interval = setInterval(this.handler, this.timeout);
            return;
        }
        if (!debounce) {
            console.log("debounce: immediate");
            handler();
            return;
        }
        this.handler = function () {
            console.log("debounce: invoke");
            _this.interval = null;
            _this.handler = null;
            handler();
        };
        setTimeout(this.handler, this.timeout);
        console.log("debounce: started");
    };
    return Debouncer;
}());
var ModificationEditor = (function () {
    function ModificationEditor(editor, callback) {
        this.editor = editor;
        this.callback = callback;
        this.debouncer = new Debouncer(2);
        this.actualCompositionUpdate = false;
        this.revertModifications = true;
        this.editor.addEventListener('beforeinput', this.handleBeforeInput.bind(this));
        this.editor.addEventListener('input', this.handleInput.bind(this));
        this.editor.addEventListener('compositionstart', this.handleCompositionStart.bind(this));
        this.editor.addEventListener('compositionupdate', this.handleCompositionUpdate.bind(this));
        this.editor.addEventListener('compositionend', this.handleCompositionEnd.bind(this));
        var self = this;
        this.observer = new MutationObserver(function (mutations) {
            if (self.revertModifications) {
                self.revertModificationAndRestoreSelection({
                    mutations: mutations
                }, self.revertSelection);
            }
            else {
                if (self.isRenderDone(mutations)) {
                    console.log("render done");
                    if (self.revertModifications) {
                        console.error('revert conflict');
                    }
                    self.revertModifications = true;
                }
            }
            if (self.afterModification) {
                var handler = self.afterModification;
                self.afterModification = null;
                handler({
                    mutations: mutations
                });
            }
            else if (!self.revertModifications) {
                console.log("allowed modification (render?)", mutations);
            }
        });
        this.startObserver();
    }
    ModificationEditor.prototype.destroy = function () {
        this.observer.disconnect();
        this.editor.removeEventListener('beforeinput', this.handleBeforeInput.bind(this));
        this.editor.removeEventListener('input', this.handleInput.bind(this));
        this.editor.removeEventListener('compositionstart', this.handleCompositionStart.bind(this));
        this.editor.removeEventListener('compositionupdate', this.handleCompositionUpdate.bind(this));
        this.editor.removeEventListener('compositionend', this.handleCompositionEnd.bind(this));
    };
    ModificationEditor.prototype.startObserver = function () {
        this.observer.observe(this.editor, {
            childList: true,
            subtree: true,
            characterData: true,
            characterDataOldValue: true,
            attributes: true,
            attributeFilter: ['data-render-key', 'data-render-key-done']
        });
    };
    ModificationEditor.prototype.isRenderDone = function (mutations) {
        for (var i = 0; i < mutations.length; i++) {
            var mutation = mutations[i];
            for (var j = 0; j < mutation.addedNodes.length; j++) {
                var node = mutation.addedNodes[j];
                if (node instanceof HTMLElement) {
                    if (node.getAttribute('data-render-key-done'))
                        return true;
                }
            }
        }
        return false;
    };
    ModificationEditor.prototype.handleBeforeInput = function (event) {
        var _this = this;
        event.stopImmediatePropagation();
        var editRange = null;
        if (event.inputType == 'insertCompositionText') {
            if (!this.actualCompositionUpdate) {
                return;
            }
            this.actualCompositionUpdate = false;
            var targetRange = event.getTargetRanges()[0];
            if (targetRange) {
                editRange = this.getLineSelection(targetRange);
            }
        }
        console.log(event);
        var currentRange = getSelection().getRangeAt(0);
        this.revertSelection = new StaticRange(currentRange);
        if (event.cancelable) {
            event.preventDefault();
            this.callback(this, {
                inputType: event.inputType,
                editRange: editRange,
                data: event.data
            }, currentRange, this.pauseRender.bind(this));
        }
        else {
            this.afterModification = function (modification) {
                var debounce = event.inputType == 'insertCompositionText';
                _this.debouncer.debounce(function () {
                    console.log("callback");
                    var currentRange = getSelection().getRangeAt(0);
                    _this.callback(_this, {
                        inputType: event.inputType,
                        editRange: editRange,
                        data: event.data
                    }, currentRange, _this.pauseRender.bind(_this));
                }, debounce);
            };
        }
    };
    ModificationEditor.prototype.handleInput = function (event) {
        if (event.inputType == 'insertCompositionText') {
            if (!this.actualCompositionUpdate) {
                return;
            }
        }
        if (!this.afterModification) {
            console.error("Unhandled input event!", event);
        }
    };
    ModificationEditor.prototype.handleCompositionStart = function (event) {
        console.log('compositionstart', event);
    };
    ModificationEditor.prototype.handleCompositionUpdate = function (event) {
        this.actualCompositionUpdate = true;
        console.log('compositionupdate', event);
    };
    ModificationEditor.prototype.handleCompositionEnd = function (event) {
        console.log('compositionend', event);
    };
    ModificationEditor.prototype.pauseRender = function () {
        var _this = this;
        this.revertModifications = false;
        return function () {
            _this.revertModifications = true;
        };
    };
    ModificationEditor.prototype.revertModificationAndRestoreSelection = function (modification, selection) {
        if (modification) {
            this.observer.disconnect();
            this.revertModification(modification);
            this.startObserver();
        }
        if (selection !== undefined) {
            this.restoreSelection(selection);
        }
    };
    ;
    ModificationEditor.prototype.revertModification = function (modification) {
        console.log("Revert modification", modification);
        var mutations = Array.from(modification.mutations.reverse());
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
    };
    ModificationEditor.prototype.restoreSelection = function (selection) {
        console.log("Restore selection", selection);
        var currentSelection = getSelection();
        if (selection) {
            if (currentSelection.rangeCount == 1) {
                var currentRange = currentSelection.getRangeAt(0);
                currentRange.setStart(selection.startContainer, selection.startOffset);
                currentRange.setEnd(selection.endContainer, selection.endOffset);
            }
            else {
                currentSelection.removeAllRanges();
                var currentRange = document.createRange();
                currentRange.setStart(selection.startContainer, selection.startOffset);
                currentRange.setEnd(selection.endContainer, selection.endOffset);
                currentSelection.addRange(currentRange);
            }
        }
        else {
            currentSelection.removeAllRanges();
        }
    };
    ModificationEditor.prototype.getCurrentSelection = function () {
        var _a;
        var range = (_a = getSelection()) === null || _a === void 0 ? void 0 : _a.getRangeAt(0);
        if (!range)
            return null;
        return this.getLineSelection(range);
    };
    ModificationEditor.prototype.setCurrentSelection = function (selection) {
        var documentSelection = getSelection();
        if (!selection) {
            if (documentSelection.rangeCount != 0)
                documentSelection.removeAllRanges();
            return;
        }
        var startMetaline = this.editor.querySelector(".metaline[data-metaline=\"".concat(selection.start.metaline, "\"]"));
        var endMetaline = selection.end.metaline == selection.start.metaline ? startMetaline
            : this.editor.querySelector(".metaline[data-metaline=\"".concat(selection.end.metaline, "\"]"));
        var startLine = findLine(startMetaline, selection.start.line);
        var endLine = selection.end.metaline == selection.start.metaline && selection.end.line == selection.start.line ? startMetaline
            : findLine(endMetaline, selection.end.line);
        var start = this.getNode(startLine, selection.start.offset);
        var end = selection.end.metaline == selection.start.metaline && selection.end.line == selection.start.line && selection.end.offset == selection.start.offset ? start
            : this.getNode(endLine, selection.end.offset);
        var range;
        if (documentSelection.rangeCount) {
            range = documentSelection.getRangeAt(0);
            range.setStart(start.node, start.offset);
            range.setEnd(end.node, end.offset);
        }
        else {
            range = document.createRange();
            range.setStart(start.node, start.offset);
            range.setEnd(end.node, end.offset);
            documentSelection.addRange(range);
        }
        this.revertSelection = new StaticRange(range);
        function findLine(metaline, line) {
            if (line === ModificationEditor.FIRST_LINE_ID) {
                return metaline.querySelector('.line[data-line]');
            }
            else if (line === ModificationEditor.LAST_LINE_ID) {
                var lines = metaline.querySelectorAll('.line[data-line]');
                return lines[lines.length - 1];
            }
            return metaline.querySelector(".line[data-line=\"".concat(selection.start.line, "\"]"));
        }
    };
    ModificationEditor.prototype.getLineSelection = function (range) {
        var start = this.getTextIndex(range.startContainer, this.getLineInfo, range.startOffset);
        var end = range.endContainer !== range.startContainer ? this.getTextIndex(range.endContainer, this.getLineInfo, range.endOffset) : {
            offset: start.offset + range.endOffset - range.startOffset,
            data: start.data,
        };
        return {
            start: {
                metaline: start.data.metaline,
                line: start.data.line,
                offset: start.offset
            },
            end: {
                metaline: end.data.metaline,
                line: end.data.line,
                offset: end.offset
            }
        };
    };
    ModificationEditor.prototype.getTextIndex = function (node, stopCondition, offset) {
        var _a;
        if (!offset)
            offset = 0;
        var data = stopCondition(node);
        for (var current = node; !data; current = current.parentElement, data = stopCondition(current)) {
            for (var sibling = current.previousSibling; sibling; sibling = sibling.previousSibling) {
                if (sibling.nodeType == Node.COMMENT_NODE)
                    continue;
                offset += ((_a = sibling.textContent) === null || _a === void 0 ? void 0 : _a.length) || 0;
            }
        }
        return {
            offset: offset,
            data: data
        };
    };
    ModificationEditor.prototype.getNode = function (node, offset) {
        if (offset < 0)
            return getFromEnd(node, -offset - 1);
        else
            return getFromStart(node, offset);
        function getFromStart(node, offset) {
            if (!(node instanceof HTMLElement))
                return {
                    node: node,
                    offset: offset,
                };
            var currentOffset = 0;
            for (var i = 0; i < node.childNodes.length; i++) {
                var child = node.childNodes[i];
                if (child.nodeType == Node.COMMENT_NODE)
                    continue;
                var offsetAfter = currentOffset + child.textContent.length;
                if (offset < offsetAfter || (offsetAfter == offset && i == node.childNodes.length - 1))
                    return getFromStart(child, offset - currentOffset);
                currentOffset = offsetAfter;
            }
            return {
                node: node,
                offset: offset,
            };
        }
        function getFromEnd(node, offset) {
            if (!(node instanceof HTMLElement))
                return {
                    node: node,
                    offset: node.textContent.length - offset,
                };
            var currentEndOffset = 0;
            for (var i = node.childNodes.length - 1; i >= 0; i--) {
                var child = node.childNodes[i];
                if (child.nodeType == Node.COMMENT_NODE)
                    continue;
                var offsetBefore = currentEndOffset + child.textContent.length;
                if (offset < offsetBefore || (offsetBefore == offset && i == 0))
                    return getFromEnd(child, offset - currentEndOffset);
                currentEndOffset = offsetBefore;
            }
            return {
                node: node,
                offset: offset,
            };
        }
    };
    ModificationEditor.prototype.getLineInfo = function (node) {
        if (!(node instanceof HTMLElement))
            return null;
        var nodeElement = node;
        var lineId = null;
        if (nodeElement.classList.contains('line')) {
            lineId = parseInt(nodeElement.getAttribute('data-line'));
            nodeElement = nodeElement.parentElement.parentElement;
        }
        else if (nodeElement.classList.contains('metaline-lines')) {
            nodeElement = nodeElement.parentElement;
        }
        else if (!nodeElement.classList.contains('metaline')) {
            return null;
        }
        var metalineId = nodeElement.getAttribute('data-metaline');
        return {
            metaline: metalineId,
            line: lineId
        };
    };
    ModificationEditor.FIRST_LINE_ID = -1;
    ModificationEditor.LAST_LINE_ID = -2;
    return ModificationEditor;
}());
var ActionQueue = (function () {
    function ActionQueue() {
        this.queue = null;
        this.resolveRender = null;
        this.awaitRenderPromise = null;
    }
    Object.defineProperty(ActionQueue.prototype, "isBusy", {
        get: function () {
            return !!this.queue;
        },
        enumerable: false,
        configurable: true
    });
    ActionQueue.prototype.then = function (onfulfilled, onrejected) {
        var self = this;
        var promise = null;
        function checkRemove(next) {
            if (next && next.then) {
                return next.then(function (afterNext) {
                    return checkRemove(afterNext);
                });
            }
            if (self.queue === promise) {
                self.queue = null;
                console.log("Queue is empty");
            }
            else {
            }
            return next;
        }
        ;
        var handler = function (value) { return checkRemove(onfulfilled(value)); };
        if (this.queue) {
            this.queue = promise = this.queue.then(handler);
        }
        else {
            this.queue = promise = new Promise(function (resolve, reject) {
                resolve(handler(undefined));
            });
        }
        return this;
    };
    ActionQueue.prototype.prepareForNextRender = function () {
        if (this.resolveRender)
            return;
        var self = this;
        this.awaitRenderPromise = new Promise(function (resolve, reject) {
            self.resolveRender = function () {
                resolve();
                self.resolveRender = null;
                self.awaitRenderPromise = null;
            };
        });
    };
    ActionQueue.prototype.awaitRender = function () {
        if (!this.awaitRenderPromise)
            return;
        console.log("Awaiting next render");
        var promise = this.awaitRenderPromise;
        return promise;
    };
    ActionQueue.prototype.notifyRender = function () {
        console.log("Render called");
        if (!this.resolveRender)
            return;
        this.resolveRender();
    };
    return ActionQueue;
}());
//# sourceMappingURL=app.js.map