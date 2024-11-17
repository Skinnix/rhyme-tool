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
            for (; (topParent === null || topParent === void 0 ? void 0 : topParent.parentElement) != element; topParent = topParent === null || topParent === void 0 ? void 0 : topParent.parentElement)
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
    if (!resolveAfterRender || !promiseAfterRender) {
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
                if (!selection)
                    throw new Error("Keine Auswahl vorhanden.");
                var eventData = __assign({ selection: selection, editRange: data.editRange, justSelected: selectionObserver.triggerJustSelected() }, data);
                wrapper.classList.add('refreshing');
                selectionObserver.pauseObservation();
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
                var selection = null;
                if (result.selection && data.inputType != 'deleteByDrag') {
                    selection = editor.setCurrentSelection(result.selection);
                }
                else {
                    selection = getSelection();
                    if ((selection === null || selection === void 0 ? void 0 : selection.rangeCount) == 1) {
                        var currentSelectionRange = selection.getRangeAt(0);
                        currentSelectionRange.setStart(selectionRange.startContainer, selectionRange.startOffset);
                        currentSelectionRange.setEnd(selectionRange.endContainer, selectionRange.endOffset);
                    }
                    else if (selection) {
                        selection.removeAllRanges();
                        selection.addRange(selectionRange);
                    }
                }
                selectionObserver.refreshSelection();
                for (var focusElement = selection === null || selection === void 0 ? void 0 : selection.focusNode; focusElement && !('scrollIntoView' in focusElement); focusElement = focusElement.parentElement) { }
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
        if (this.interval && this.handler) {
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
            for (var current = mutation.target; current && current != editor; current = current.parentElement) {
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
        var _a;
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
        var currentRange = (_a = getSelection()) === null || _a === void 0 ? void 0 : _a.getRangeAt(0);
        if (!currentRange)
            return;
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
                    var _a;
                    console.log("callback");
                    var currentRange = (_a = getSelection()) === null || _a === void 0 ? void 0 : _a.getRangeAt(0);
                    if (!currentRange)
                        throw new Error("Keine Auswahl m�glich");
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
        var _a;
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
                            (_a = node.parentElement) === null || _a === void 0 ? void 0 : _a.removeChild(node);
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
        var _a, _b, _c, _d;
        console.log("Restore selection", selection);
        var currentSelection = getSelection();
        if (!currentSelection)
            throw new Error("Keine Auswahl m�glich");
        if (selection) {
            var currentRange = void 0;
            if ((currentSelection === null || currentSelection === void 0 ? void 0 : currentSelection.rangeCount) == 1) {
                currentRange = currentSelection.getRangeAt(0);
            }
            else {
                currentSelection.removeAllRanges();
                currentRange = document.createRange();
                currentSelection.addRange(currentRange);
            }
            if (selection.startOffset <= ((_b = (_a = selection.startContainer.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0))
                currentRange.setStart(selection.startContainer, selection.startOffset);
            else
                currentRange.setStartAfter(selection.startContainer);
            if (selection.endOffset <= ((_d = (_c = selection.endContainer.textContent) === null || _c === void 0 ? void 0 : _c.length) !== null && _d !== void 0 ? _d : 0))
                currentRange.setEnd(selection.endContainer, selection.endOffset);
            else
                currentRange.setEndAfter(selection.endContainer);
        }
        else {
            currentSelection === null || currentSelection === void 0 ? void 0 : currentSelection.removeAllRanges();
        }
    };
    ModificationEditor.prototype.getCurrentSelection = function (documentSelection) {
        var _a;
        if (documentSelection === undefined)
            documentSelection = getSelection() || undefined;
        if (!documentSelection || documentSelection.rangeCount == 0)
            return null;
        var range = (_a = getSelection()) === null || _a === void 0 ? void 0 : _a.getRangeAt(0);
        if (!range)
            return null;
        if (!this.editor.contains(range.startContainer) || !this.editor.contains(range.endContainer))
            return null;
        return this.getLineSelection(range);
    };
    ModificationEditor.prototype.setCurrentSelection = function (lineSelection) {
        var documentSelection = getSelection();
        if (!documentSelection)
            throw new Error("Keine Auswahl m�glich");
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
                : endLine ? null : this.editor.querySelector(".metaline[data-metaline=\"".concat(metalineLineSelection.end.metaline, "\"]"));
            if (!startLine) {
                if (!startMetaline || metalineLineSelection.start.line === null)
                    throw new Error("Zeile nicht gefunden");
                startLine = findLine(startMetaline, metalineLineSelection.start.line);
                if (!startLine)
                    throw new Error("Zeile nicht gefunden");
            }
            if (!endLine) {
                if (!endMetaline || metalineLineSelection.end.line === null)
                    throw new Error("Zeile nicht gefunden");
                endLine = metalineLineSelection.end.metaline == metalineLineSelection.start.metaline && metalineLineSelection.end.line == metalineLineSelection.start.line ? startLine
                    : findLine(endMetaline, metalineLineSelection.end.line);
                if (!endLine)
                    throw new Error("Zeile nicht gefunden");
            }
        }
        var start = SelectionHelper.getNodeAndOffset(startLine, lineSelection.start.offset);
        range.setStart(start.node, start.offset);
        var end = startLine === endLine && lineSelection.end.offset == lineSelection.start.offset ? start
            : SelectionHelper.getNodeAndOffset(endLine, lineSelection.end.offset);
        range.setEnd(end.node, end.offset);
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
        if (!start)
            return null;
        var end = range.endContainer !== range.startContainer ? this.getTextIndex(range.endContainer, this.getLineInfo, range.endOffset) : {
            offset: start.offset + range.endOffset - range.startOffset,
            data: start.data,
            node: start.node,
        };
        if (!end)
            return null;
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
        if (!data || !current)
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
            var _a, _b;
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
                var offsetAfter = currentOffset + ((_b = (_a = child.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0);
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
            var _a, _b;
            if (!(node instanceof HTMLElement)) {
                if (!node.textContent)
                    throw new Error("Kein Textknoten gefunden");
                return {
                    node: node,
                    offset: node.textContent.length - offset,
                };
            }
            var currentEndOffset = 0;
            for (var i = node.childNodes.length - 1; i >= 0; i--) {
                var child = node.childNodes[i];
                if (child.nodeType == Node.COMMENT_NODE)
                    continue;
                var offsetBefore = currentEndOffset + ((_b = (_a = child.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0);
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
        var _a, _b;
        if (!(node instanceof HTMLElement))
            return null;
        var nodeElement = node;
        var lineId = null;
        if (node.classList.contains('line')) {
            lineId = parseInt((_a = nodeElement.getAttribute('data-line')) !== null && _a !== void 0 ? _a : '');
            nodeElement = (_b = nodeElement.parentElement) === null || _b === void 0 ? void 0 : _b.parentElement;
        }
        else if (nodeElement.classList.contains('metaline-lines')) {
            nodeElement = nodeElement.parentElement;
        }
        else if (!nodeElement.classList.contains('metaline')) {
            return null;
        }
        var metalineId = nodeElement === null || nodeElement === void 0 ? void 0 : nodeElement.getAttribute('data-metaline');
        if (metalineId == null || lineId == null)
            return null;
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
        if (!this.editorWrapper)
            throw new Error("Editor hat kein Elternelement");
        this.customSelection = this.editorWrapper.querySelector('.custom-selection');
        if (!this.customSelection)
            throw new Error("Custom-Selection-Element nicht gefunden");
        document.addEventListener('selectionchange', this.handleSelectionChange.bind(this));
        this.editor.addEventListener('dragstart', this.handleDragStart.bind(this));
    }
    SelectionObserver.prototype.destroy = function () {
        document.removeEventListener('selectionchange', this.handleSelectionChange.bind(this));
        this.editor.removeEventListener('dragstart', this.handleDragStart.bind(this));
    };
    SelectionObserver.prototype.triggerJustSelected = function () {
        if (this.justSelected) {
            this.justSelected = false;
            return true;
        }
        return false;
    };
    SelectionObserver.prototype.refreshSelection = function () {
        this.isPaused = false;
        requestAnimationFrame(this.processSelectionChange.bind(this));
    };
    SelectionObserver.prototype.pauseObservation = function () {
        this.isPaused = true;
    };
    SelectionObserver.prototype.handleSelectionChange = function () {
        var _this = this;
        if (this.isPaused) {
            requestAnimationFrame((function () {
                _this.isPaused = false;
            }).bind(this));
            return;
        }
        this.processSelectionChange();
    };
    SelectionObserver.prototype.processSelectionChange = function () {
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
        if (SelectionObserver.needsLineFix && this.fixSelection(documentSelection, range))
            return;
        var lineSelection = this.modificationEditor.getLineSelection(range, true);
        if (!lineSelection || lineSelection.start.metaline != lineSelection.end.metaline)
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
    SelectionObserver.prototype.fixSelection = function (documentSelection, range) {
        var _a, _b, _c;
        var modified = false;
        var collapsed = range.collapsed;
        var newStart = null;
        if (!(range.startContainer instanceof Text)) {
            if (range.startContainer.childNodes.length > range.startOffset) {
                var node = range.startContainer.childNodes[range.startOffset];
                while (node.firstChild)
                    node = node.firstChild;
                newStart = { node: node, offset: 0 };
                modified = true;
            }
        }
        if (!collapsed) {
            if (!(range.endContainer instanceof Text)) {
                if (range.endContainer.childNodes.length > range.endOffset) {
                    var node = range.endContainer.childNodes[range.startOffset];
                    while (node.lastChild)
                        node = node.lastChild;
                    range.setEnd(node, (_b = (_a = node.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0);
                    modified = true;
                }
            }
            if (newStart)
                range.setStart(newStart.node, newStart.offset);
        }
        else if (newStart) {
            range.setStart(newStart.node, newStart.offset);
            range.setEnd(newStart.node, newStart.offset);
        }
        var startContainer = range.endContainer;
        var startNode = startContainer instanceof HTMLElement ? startContainer : startContainer.parentElement;
        if (startNode === null || startNode === void 0 ? void 0 : startNode.classList.contains('suffix')) {
            if (((_c = startNode.previousSibling) === null || _c === void 0 ? void 0 : _c.classList.contains('suffix')) === false) {
                startNode = startNode.previousSibling;
                var node = SelectionHelper.getNodeAndOffset(startNode, -1);
                range.setEnd(node.node, node.offset);
                if (collapsed)
                    range.setStart(node.node, node.offset);
                modified = true;
            }
            else {
                range.setEnd(startContainer, 0);
                if (collapsed)
                    range.setStart(startContainer, 0);
                modified = true;
            }
        }
        if (modified)
            console.log('fixed');
        return modified;
    };
    SelectionObserver.prototype.resetCustomSelections = function () {
        this.customSelection.className = 'custom-selection';
        this.justSelected = true;
    };
    SelectionObserver.prototype.adjustBoxSelection = function (documentSelection, range, lineSelection) {
        if (!this.supportsMultipleRanges)
            return emulateBoxSelection(this, documentSelection, range, lineSelection);
        if (lineSelection.start.lineNode && lineSelection.end.lineNode && lineSelection.end.lineNode.compareDocumentPosition(lineSelection.start.lineNode) & Node.DOCUMENT_POSITION_FOLLOWING) {
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
            emulateBoxSelection(this, documentSelection, range, lineSelection);
        }
        function emulateBoxSelection(self, documentSelection, range, lineSelection) {
            var startCell = range.startContainer;
            var endCell = range.endContainer;
            if (!startCell || !endCell || lineSelection.start.line == null || lineSelection.end.line == null)
                return self.resetCustomSelections();
            console.log(lineSelection.start, lineSelection.end);
            console.log(range);
            if (lineSelection.start.line > lineSelection.end.line
                || (lineSelection.start.line == lineSelection.end.line && lineSelection.start.offset > lineSelection.end.offset)) {
                startCell = endCell;
                endCell = range.startContainer;
            }
            if (!(startCell instanceof HTMLElement))
                startCell = startCell === null || startCell === void 0 ? void 0 : startCell.parentNode;
            if (!(endCell instanceof HTMLElement))
                endCell = endCell === null || endCell === void 0 ? void 0 : endCell.parentNode;
            var selectionBox;
            if (range.collapsed) {
                if (range.startOffset != 0)
                    startCell = startCell === null || startCell === void 0 ? void 0 : startCell.nextSibling;
                if (!(startCell instanceof HTMLElement))
                    return self.resetCustomSelections();
                selectionBox = startCell.getBoundingClientRect();
            }
            else {
                if (lineSelection.start.offset == lineSelection.end.offset) {
                    endCell = endCell === null || endCell === void 0 ? void 0 : endCell.nextSibling;
                }
                if (lineSelection.end.offset >= lineSelection.start.offset) {
                    if (range.startOffset != 0)
                        startCell = startCell === null || startCell === void 0 ? void 0 : startCell.nextSibling;
                    if (range.endOffset == 0)
                        endCell = endCell === null || endCell === void 0 ? void 0 : endCell.previousSibling;
                }
                else {
                    if (range.startOffset == 0)
                        startCell = startCell === null || startCell === void 0 ? void 0 : startCell.previousSibling;
                    if (range.endOffset != 0)
                        endCell = endCell === null || endCell === void 0 ? void 0 : endCell.nextSibling;
                }
                if (!(startCell instanceof HTMLElement) || !(endCell instanceof HTMLElement))
                    return self.resetCustomSelections();
                var startBox = startCell.getBoundingClientRect();
                var endBox = endCell.getBoundingClientRect();
                selectionBox = SelectionObserver.getBoundingBox(startBox, endBox);
            }
            setBoxPosition(self, selectionBox);
            function setBoxPosition(self, box) {
                var editorRect = self.editor.getBoundingClientRect();
                var left = box.left - editorRect.left;
                var top = box.top - editorRect.top;
                var width = box.width;
                var height = box.height;
                var newPosition = top.toFixed(0) + ';' + left.toFixed(0) + ';' + documentSelection.toString().length;
                var newPositionFull = newPosition + ';' + width.toFixed(0) + ';' + height.toFixed(0);
                var currentPosition = self.customSelection['data-position'];
                if (currentPosition != newPositionFull) {
                    self.customSelection.style.top = top + 'px';
                    self.customSelection.style.left = left + 'px';
                    self.customSelection.style.width = width + 'px';
                    self.customSelection.style.height = height + 'px';
                    self.customSelection['data-position'] = newPositionFull;
                    if (!(currentPosition === null || currentPosition === void 0 ? void 0 : currentPosition.startsWith(newPosition)))
                        self.justSelected = true;
                }
                if (self.customSelection.className != 'custom-selection custom-selection-box') {
                    self.customSelection.className = 'custom-selection custom-selection-box';
                    self.justSelected = true;
                }
            }
        }
    };
    SelectionObserver.getBoundingBox = function () {
        var args = [];
        for (var _i = 0; _i < arguments.length; _i++) {
            args[_i] = arguments[_i];
        }
        var left = Number.MAX_VALUE;
        var top = Number.MAX_VALUE;
        var right = 0;
        var bottom = 0;
        for (var _a = 0, args_1 = args; _a < args_1.length; _a++) {
            var rect = args_1[_a];
            if (rect.left < left)
                left = rect.left;
            if (rect.right > right)
                right = rect.right;
            if (rect.top < top)
                top = rect.top;
            if (rect.bottom > bottom)
                bottom = rect.bottom;
        }
        return new DOMRect(left, top, right - left, bottom - top);
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
    SelectionObserver.needsLineFix = /Firefox/.test(navigator.userAgent);
    return SelectionObserver;
}());
var SelectionHelper = (function () {
    function SelectionHelper() {
    }
    SelectionHelper.getNodeAndOffset = function (node, offset) {
        var _a, _b, _c, _d, _e, _f;
        if (offset < 0)
            return SelectionHelper.getNodeAndOffsetFromEnd(node, offset);
        var iterator = document.createNodeIterator(node, NodeFilter.SHOW_TEXT);
        var currentOffset = 0;
        var current = iterator.nextNode();
        while (current && currentOffset + ((_b = (_a = current.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0) < offset) {
            currentOffset += (_d = (_c = current.textContent) === null || _c === void 0 ? void 0 : _c.length) !== null && _d !== void 0 ? _d : 0;
            var next = iterator.nextNode();
            if (!next)
                return {
                    node: current,
                    offset: ((_f = (_e = current.textContent) === null || _e === void 0 ? void 0 : _e.length) !== null && _f !== void 0 ? _f : 0),
                };
            current = next;
        }
        return {
            node: current !== null && current !== void 0 ? current : node,
            offset: offset - currentOffset,
        };
    };
    SelectionHelper.getNodeAndOffsetFromEnd = function (node, offset) {
        var _a, _b, _c, _d;
        var iterator = document.createNodeIterator(node, NodeFilter.SHOW_TEXT);
        var allNodes = [];
        var current = iterator.nextNode();
        if (!current)
            return {
                node: node,
                offset: 0,
            };
        while (current) {
            allNodes.push(current);
            current = iterator.nextNode();
        }
        offset = -offset - 1;
        var currentOffset = 0;
        for (var i = allNodes.length - 1; i >= 0; i--) {
            current = allNodes[i];
            if (currentOffset + ((_b = (_a = current.textContent) === null || _a === void 0 ? void 0 : _a.length) !== null && _b !== void 0 ? _b : 0) >= offset)
                break;
            currentOffset += (_d = (_c = current.textContent) === null || _c === void 0 ? void 0 : _c.length) !== null && _d !== void 0 ? _d : 0;
        }
        current !== null && current !== void 0 ? current : (current = node);
        if (!current.textContent)
            throw new Error("Kein Textknoten gefunden");
        return {
            node: current,
            offset: current.textContent.length - (offset - currentOffset),
        };
    };
    return SelectionHelper;
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
        var handler = function (value) { return checkRemove(onfulfilled ? onfulfilled(value) : undefined); };
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