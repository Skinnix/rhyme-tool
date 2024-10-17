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
    var observer = new ResizeObserver(handler);
    observer.observe(element);
    handler();
    return {
        destroy: function () {
            observer.disconnect();
        },
    };
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
    var handler;
    var editor = null;
    var selectionObserver = null;
    handler = function (event) {
        wrapper.removeEventListener('focus', handler);
        createEditor();
    };
    wrapper.addEventListener('focus', handler);
    return {
        notifyAfterRender: actionQueue.notifyRender.bind(actionQueue),
        destroy: function () {
            editor === null || editor === void 0 ? void 0 : editor.destroy();
            selectionObserver === null || selectionObserver === void 0 ? void 0 : selectionObserver.destroy();
        }
    };
    function createEditor() {
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
                var selection;
                if (result.selection && data.inputType != 'deleteByDrag') {
                    selection = editor.setCurrentSelection(result.selection);
                }
                else if (selectionRange) {
                    selection = getSelection();
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
                for (var focusElement = selection.focusNode; focusElement && !('scrollIntoView' in focusElement); focusElement = focusElement.parentElement) { }
                if (focusElement) {
                    focusElement.scrollIntoView({
                        block: 'nearest',
                        inline: 'nearest'
                    });
                }
                wrapper.classList.remove('refreshing');
            });
        };
        editor = new ModificationEditor(wrapper, callback);
        selectionObserver = new SelectionObserver(editor);
        if (window.isDebugging) {
            window.editor = editor;
            window.selectionObserver = selectionObserver;
        }
    }
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
        this.isUncancelable = function (event) { return false; };
        this.editor.addEventListener('beforeinput', this.handleBeforeInput.bind(this));
        this.editor.addEventListener('input', this.handleInput.bind(this));
        this.editor.addEventListener('compositionstart', this.handleCompositionStart.bind(this));
        this.editor.addEventListener('compositionupdate', this.handleCompositionUpdate.bind(this));
        this.editor.addEventListener('compositionend', this.handleCompositionEnd.bind(this));
        if (/Chrome.*Mobile/.test(navigator.userAgent)) {
            this.isUncancelable = function (event) {
                if (event.inputType == 'deleteContent' || event.inputType == 'deleteContentBackward' || event.inputType == 'deleteContentForward') {
                    return true;
                }
                return false;
            };
        }
        var self = this;
        this.observer = new MutationObserver(function (mutations) {
            if (self.ignoreMutation(mutations, editor))
                return;
            if (self.isRenderDone(mutations)) {
                self.revertModifications = true;
                console.log("render done");
                return;
            }
            if (self.revertModifications) {
                self.revertModificationAndRestoreSelection({
                    mutations: mutations
                }, self.revertSelection);
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
    ModificationEditor.prototype.ignoreMutation = function (mutations, editor) {
        for (var i = 0; i < mutations.length; i++) {
            var mutation = mutations[i];
            for (var current = mutation.target; current != editor; current = current.parentElement) {
                if (current instanceof HTMLElement) {
                    if (current.classList.contains('metaline-controls') || current.classList.contains('line-controls')) {
                        return true;
                    }
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
        var data = event.data;
        if (!event.data && event.dataTransfer) {
            data = event.dataTransfer.getData('text');
        }
        console.log(event);
        var currentRange = getSelection().getRangeAt(0);
        if (event.inputType == 'deleteContent' || event.inputType == 'deleteContentBackward' || event.inputType == 'deleteContentForward') {
            if (currentRange.endOffset == 0 && currentRange.startContainer !== currentRange.endContainer) {
                var rangeString = currentRange.toString();
                if (rangeString === '' || rangeString === "\n") {
                    currentRange.collapse(event.inputType == 'deleteContentForward');
                }
            }
        }
        this.revertSelection = new StaticRange(currentRange);
        if (event.cancelable && !this.isUncancelable(event)) {
            event.preventDefault();
            this.callback(this, {
                inputType: event.inputType,
                editRange: editRange,
                data: data
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
                        data: data
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
            this.revertModification(modification);
            this.observer.takeRecords();
        }
        if (selection !== undefined) {
            this.restoreSelection(selection);
        }
    };
    ;
    ModificationEditor.prototype.revertModification = function (modification) {
        console.log("Revert modification", modification);
        console.log(JSON.stringify(modification, null, 2));
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
    ModificationEditor.prototype.getCurrentSelection = function (documentSelection) {
        if (documentSelection === undefined)
            documentSelection = getSelection();
        if (!documentSelection || documentSelection.rangeCount == 0)
            return null;
        var range = getSelection().getRangeAt(0);
        if (!range)
            return null;
        if (!this.editor.contains(range.startContainer) || !this.editor.contains(range.endContainer))
            return null;
        return this.getLineSelection(range);
    };
    ModificationEditor.prototype.setCurrentSelection = function (lineSelection) {
        var documentSelection = getSelection();
        if (!lineSelection) {
            if (documentSelection.rangeCount != 0)
                documentSelection.removeAllRanges();
            this.revertSelection = null;
            return documentSelection;
        }
        var range;
        if (documentSelection.rangeCount) {
            range = documentSelection.getRangeAt(0);
        }
        else {
            range = document.createRange();
            documentSelection.addRange(range);
        }
        this.setLineSelectionRange(documentSelection, range, lineSelection);
        this.revertSelection = new StaticRange(range);
        return documentSelection;
    };
    ModificationEditor.prototype.setLineSelectionRange = function (documentSelection, range, lineSelection) {
        var startLine = lineSelection.start.lineNode;
        var endLine = lineSelection.end.lineNode;
        console.log('selecting', lineSelection);
        if (!(startLine instanceof HTMLElement && endLine instanceof HTMLElement)) {
            var metalineLineSelection = lineSelection;
            var startMetaline = startLine ? null : this.editor.querySelector(".metaline[data-metaline=\"".concat(metalineLineSelection.start.metaline, "\"]"));
            var endMetaline = metalineLineSelection.end.metaline == metalineLineSelection.start.metaline ? startMetaline
                : endLine ? null
                    : this.editor.querySelector(".metaline[data-metaline=\"".concat(metalineLineSelection.end.metaline, "\"]"));
            startLine !== null && startLine !== void 0 ? startLine : (startLine = findLine(startMetaline, metalineLineSelection.start.line));
            endLine !== null && endLine !== void 0 ? endLine : (endLine = metalineLineSelection.end.metaline == metalineLineSelection.start.metaline && metalineLineSelection.end.line == metalineLineSelection.start.line ? startLine
                : findLine(endMetaline, metalineLineSelection.end.line));
        }
        if (lineSelection.start.offset < 0 || lineSelection.start.offset >= startLine.textContent.length) {
            var current = startLine;
            while (current.lastChild)
                current = current.lastChild;
            range.setStart(current, current.textContent.length);
            range.collapse(true);
            for (var i_1 = -1; i_1 > lineSelection.start.offset; i_1--)
                documentSelection.modify('move', 'backward', 'character');
        }
        else {
            range.setStart(startLine, 0);
            range.collapse(true);
            for (var i = 0; i < lineSelection.start.offset; i++)
                documentSelection.modify('move', 'forward', 'character');
        }
        if (startLine === endLine) {
            if (lineSelection.start.offset == lineSelection.end.offset) {
                return;
            }
            else if (lineSelection.end.offset >= 0) {
                for (var i = lineSelection.start.offset; i < lineSelection.end.offset; i++)
                    documentSelection.modify('extend', 'forward', 'character');
                return;
            }
        }
        if (lineSelection.end.offset < 0 || lineSelection.end.offset >= endLine.textContent.length) {
            var current = endLine;
            while (current.lastChild)
                current = current.lastChild;
            range.setEnd(current, current.textContent.length);
            for (var i_2 = -1; i_2 > lineSelection.end.offset; i_2--)
                documentSelection.modify('extend', 'backward', 'character');
        }
        else {
            range.setEnd(endLine, 0);
            for (var i = 0; i < lineSelection.end.offset; i++)
                documentSelection.modify('extend', 'forward', 'character');
        }
        function findLine(metaline, line) {
            if (line === ModificationEditor.FIRST_LINE_ID) {
                return metaline.querySelector('.line[data-line]');
            }
            else if (line === ModificationEditor.LAST_LINE_ID) {
                var lines = metaline.querySelectorAll('.line[data-line]');
                return lines[lines.length - 1];
            }
            return metaline.querySelector(".line[data-line=\"".concat(line, "\"]"));
        }
    };
    ModificationEditor.prototype.getLineSelection = function (range, includeNode) {
        var start = this.getTextIndex(range.startContainer, this.getLineInfo, range.startOffset);
        var end = range.endContainer !== range.startContainer ? this.getTextIndex(range.endContainer, this.getLineInfo, range.endOffset) : {
            offset: start.offset + range.endOffset - range.startOffset,
            data: start.data,
            node: start.node,
        };
        return {
            start: {
                metaline: start.data.metaline,
                line: start.data.line,
                offset: start.offset,
                lineNode: includeNode ? start.node : undefined,
            },
            end: {
                metaline: end.data.metaline,
                line: end.data.line,
                offset: end.offset,
                lineNode: includeNode ? end.node : undefined,
            }
        };
    };
    ModificationEditor.prototype.getTextIndex = function (node, stopCondition, offset) {
        var _a;
        if (!offset)
            offset = 0;
        var data = stopCondition(node);
        var current;
        for (current = node; current && !data; current = current.parentElement, data = stopCondition(current)) {
            for (var sibling = current.previousSibling; sibling; sibling = sibling.previousSibling) {
                if (sibling.nodeType == Node.COMMENT_NODE)
                    continue;
                offset += ((_a = sibling.textContent) === null || _a === void 0 ? void 0 : _a.length) || 0;
            }
        }
        if (!data)
            return null;
        return {
            node: current,
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
        if (node.classList.contains('line')) {
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
var SelectionObserver = (function () {
    function SelectionObserver(modificationEditor) {
        this.modificationEditor = modificationEditor;
        this.supportsMultipleRanges = false;
        this.editor = modificationEditor.editor;
        this.editorWrapper = this.editor.parentElement;
        this.customSelection = this.editorWrapper.querySelector('.custom-selection');
        document.addEventListener('selectionchange', this.handleSelectionChange.bind(this));
        this.editor.addEventListener('dragstart', this.handleDragStart.bind(this));
    }
    SelectionObserver.prototype.destroy = function () {
        document.removeEventListener('selectionchange', this.handleSelectionChange.bind(this));
        this.editor.removeEventListener('dragstart', this.handleDragStart.bind(this));
    };
    SelectionObserver.prototype.handleSelectionChange = function (event) {
        var _a, _b;
        var documentSelection = getSelection();
        if (!documentSelection || documentSelection.rangeCount == 0)
            return this.resetCustomSelections();
        var range = documentSelection.getRangeAt(0);
        if (documentSelection.rangeCount > 1) {
            var firstRange = range;
            var lastRange = documentSelection.getRangeAt(documentSelection.rangeCount - 1);
            if (lastRange.startContainer.compareDocumentPosition(firstRange.startContainer) & Node.DOCUMENT_POSITION_FOLLOWING) {
                firstRange = lastRange;
                lastRange = range;
            }
            range = document.createRange();
            if (firstRange.endContainer.compareDocumentPosition(firstRange.startContainer) & Node.DOCUMENT_POSITION_FOLLOWING) {
                range.setStart(lastRange.endContainer, lastRange.endOffset);
                range.setEnd(firstRange.startContainer, firstRange.startOffset);
            }
            else {
                range.setStart(firstRange.startContainer, firstRange.startOffset);
                range.setEnd(lastRange.endContainer, lastRange.endOffset);
            }
        }
        if (!this.editor.contains(range.startContainer) || !this.editor.contains(range.endContainer))
            return this.resetCustomSelections();
        var lineSelection = this.modificationEditor.getLineSelection(range, true);
        if (lineSelection.start.metaline != lineSelection.end.metaline)
            return this.resetCustomSelections();
        var metaline = (_b = (_a = lineSelection.start.lineNode) === null || _a === void 0 ? void 0 : _a.parentElement) === null || _b === void 0 ? void 0 : _b.parentElement;
        if (!metaline)
            return this.resetCustomSelections();
        var selectionType = metaline.getAttribute('data-selection');
        if (!selectionType)
            return this.resetCustomSelections();
        switch (selectionType) {
            case 'box':
                this.adjustBoxSelection(documentSelection, range, lineSelection);
                break;
            default:
                this.resetCustomSelections();
                break;
        }
    };
    SelectionObserver.prototype.resetCustomSelections = function () {
        this.customSelection.className = 'custom-selection';
    };
    SelectionObserver.prototype.adjustBoxSelection = function (documentSelection, range, lineSelection) {
        if (range.collapsed) {
            var newEnd = extendRangeByOne(range.startContainer, range.startOffset);
            if (typeof newEnd === 'number') {
                var newRange = document.createRange();
                newRange.setStart(range.endContainer, range.endOffset);
                newRange.setEnd(range.endContainer, newEnd);
                range = newRange;
            }
            else if (newEnd) {
                var newRange = document.createRange();
                newRange.setStart(newEnd, 0);
                newRange.setEnd(newEnd, 1);
                range = newRange;
            }
        }
        if (range.collapsed)
            return this.resetCustomSelections();
        if (!this.supportsMultipleRanges)
            return emulateBoxSelection(this, documentSelection, range);
        if (lineSelection.end.lineNode.compareDocumentPosition(lineSelection.start.lineNode) & Node.DOCUMENT_POSITION_FOLLOWING) {
            lineSelection = {
                start: lineSelection.end,
                end: lineSelection.start,
            };
        }
        var startLine = lineSelection.start.lineNode;
        var endLine = lineSelection.end.lineNode;
        if (!(startLine instanceof HTMLElement) || !(endLine instanceof HTMLElement))
            return this.resetCustomSelections();
        var lines = [startLine];
        for (var current = startLine.nextSibling; current != endLine; current = current.nextSibling) {
            if (!(current instanceof HTMLElement) || !current.classList.contains('line'))
                continue;
            lines.push(current);
        }
        lines.push(endLine);
        var startOffset = lineSelection.start.offset;
        var endOffset = lineSelection.end.offset;
        documentSelection.removeAllRanges();
        for (var i = 0; i < lines.length; i++) {
            var line = lines[i];
            var lineRange = document.createRange();
            documentSelection.addRange(lineRange);
            this.modificationEditor.setLineSelectionRange(documentSelection, lineRange, {
                start: {
                    lineNode: line,
                    offset: startOffset,
                },
                end: {
                    lineNode: line,
                    offset: endOffset,
                }
            });
        }
        if (documentSelection.rangeCount != lines.length) {
            this.supportsMultipleRanges = false;
            emulateBoxSelection(this, documentSelection, range);
        }
        function emulateBoxSelection(self, documentSelection, range) {
            var lineSelection = self.modificationEditor.getLineSelection(range, false);
            if (lineSelection.start.offset == lineSelection.end.offset) {
                var newEnd = extendRangeByOne(range.endContainer, range.endOffset);
                if (typeof newEnd === 'number') {
                    var newRange = document.createRange();
                    newRange.setStart(range.startContainer, range.startOffset);
                    newRange.setEnd(range.endContainer, newEnd);
                    range = newRange;
                }
                else if (newEnd) {
                    var newRange = document.createRange();
                    newRange.setStart(range.startContainer, range.startOffset);
                    newRange.setEnd(newEnd, 1);
                    range = newRange;
                }
            }
            var rects = range.getClientRects();
            var startRect = rects[0];
            var endRect = rects[rects.length - 1];
            if (startRect.top > endRect.top) {
                var temp = startRect;
                startRect = endRect;
                endRect = temp;
            }
            var x = startRect.left;
            if (lineSelection.end.offset < lineSelection.start.offset)
                x = endRect.right;
            var y = startRect.top;
            var width = +(startRect.left - endRect.right);
            if (width < 0)
                width = -width;
            var height = endRect.bottom - startRect.top;
            var wrapperRect = self.editorWrapper.getBoundingClientRect();
            self.customSelection.style.top = (y - wrapperRect.top) + 'px';
            self.customSelection.style.left = (x - wrapperRect.left) + 'px';
            self.customSelection.style.width = width + 'px';
            self.customSelection.style.height = height + 'px';
            self.customSelection.className = 'custom-selection custom-selection-box';
        }
        function extendRangeByOne(container, offset) {
            if (offset < container.textContent.length)
                return offset + 1;
            do {
                while (!container.nextSibling) {
                    container = container.parentElement;
                    if (container.nodeType == Node.ELEMENT_NODE && container.classList.contains('line')) {
                        return null;
                    }
                }
                container = container.nextSibling;
            } while (container.textContent.length == 0);
            do {
                if (container.nodeType != Node.TEXT_NODE)
                    container = container.firstChild;
                while (container.textContent.length == 0)
                    container = container.nextSibling;
            } while (container.nodeType != Node.TEXT_NODE);
            return container;
        }
    };
    SelectionObserver.prototype.handleDragStart = function (event) {
        if (!(event.target instanceof Node))
            return;
        for (var current = event.target; current && current != this.editor; current = current.parentElement) {
            if (!(current instanceof HTMLElement))
                continue;
            var selectionType = current.getAttribute('data-selection');
            if (selectionType == 'box') {
                event.preventDefault();
                event.stopPropagation();
                return false;
            }
        }
    };
    return SelectionObserver;
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