declare interface Destructible {
	destroy(): void;
}

declare interface AwaitingModification {

}

declare interface ObservedModification {
	mutations: MutationRecord[];
}

declare interface LineIdentifier {
	metaline: string;
	line: number | null;
}

declare interface TextIndex<T> {
	node: Node;
	offset: number;
	data: T;
}

declare interface NodeSelectionAnchor {
	node: Node,
	offset: number,
}

declare interface AnchorSelection<TAnchor> {
	start: TAnchor;
	end: TAnchor;
}

declare interface NodeSelection {
	start: NodeSelectionAnchor,
	end: NodeSelectionAnchor,
}

declare interface MetalineLineAnchor extends LineIdentifier {
	offset: number;
	lineNode?: Node;
}

declare interface LineNodeAnchor {
	offset: number;
	lineNode: Node;
}

declare interface ModificationData {
	inputType: string,
	data: string,
	editRange?: AnchorSelection<MetalineLineAnchor>,
}

declare interface MetalineEditResult {
	success: boolean,
	willRender: boolean,
	selection: AnchorSelection<MetalineLineAnchor> | null,
	failReason?: {
		label: string,
		code: string,
	},
}

declare type EditorCallback = (editor: ModificationEditor, data: ModificationData, selectionRange: Range | null, pauseRender: () => (() => void)) => void;
declare type ModificationHandler = (modification: ObservedModification) => void;

function registerModificationEditor(editor: HTMLElement, reference: BlazorDotNetReference, callbackName: string): ModificationEditor {
	let callback: EditorCallback = (editor, data) => {
		var selection = editor.getCurrentSelection();
		var eventData = {
			selection: selection,
			editRange: data.editRange,
			...data
		};
		setTimeout(() => {
			reference.invokeMethodAsync<MetalineEditResult>(callbackName, eventData).then(result => {
				editor.setCurrentSelection(result.selection);
			});
		}, 0);
	};
	return new ModificationEditor(editor, callback);
}

class Debouncer {
	private interval: ReturnType<typeof setInterval> | null;
	private handler: TimerHandler | null;

	constructor(private timeout: number) {

	}
	
	public debounce(handler: () => void, debounce: boolean) {
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
		
		this.handler = () => {
			console.log("debounce: invoke");
			this.interval = null;
			this.handler = null;
			handler();
		};
		setTimeout(this.handler, this.timeout);
		console.log("debounce: started");
	}
}

class ModificationEditor implements Destructible {
	static FIRST_LINE_ID = -1;
	static LAST_LINE_ID = -2;

	private observer: MutationObserver;
	private debouncer = new Debouncer(2);
	private actualCompositionUpdate = false;
	private afterModification: ModificationHandler | null;

	private revertModifications = true;
	private revertSelection: StaticRange | null | undefined;

	private isUncancelable = (event: InputEvent) => false;
	
	constructor(public editor: HTMLElement, private callback: EditorCallback) {
		this.editor.addEventListener('beforeinput', this.handleBeforeInput.bind(this));
		this.editor.addEventListener('input', this.handleInput.bind(this));
		this.editor.addEventListener('compositionstart', this.handleCompositionStart.bind(this));
		this.editor.addEventListener('compositionupdate', this.handleCompositionUpdate.bind(this));
		this.editor.addEventListener('compositionend', this.handleCompositionEnd.bind(this));

		if (/Chrome.*Mobile/.test(navigator.userAgent)) {
			this.isUncancelable = (event: InputEvent) => {
				if (event.inputType == 'deleteContent' || event.inputType == 'deleteContentBackward' || event.inputType == 'deleteContentForward') {
					return true;
				}

				return false;
			};
		}

		let self = this;
		this.observer = new MutationObserver(function (mutations) {
			//Element ignorieren?
			if (self.ignoreMutation(mutations, editor))
				return;

			//Rerender?
			if (self.isRenderDone(mutations)) {
				//Render fertig
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
				let handler = self.afterModification;
				self.afterModification = null;
				handler({
					mutations: mutations
				});
			} else if (!self.revertModifications) {
				console.log("allowed modification (render?)", mutations);
			}
		});
		this.startObserver();
	}

	public destroy() {
		this.observer.disconnect();
		this.editor.removeEventListener('beforeinput', this.handleBeforeInput.bind(this));
		this.editor.removeEventListener('input', this.handleInput.bind(this));
		this.editor.removeEventListener('compositionstart', this.handleCompositionStart.bind(this));
		this.editor.removeEventListener('compositionupdate', this.handleCompositionUpdate.bind(this));
		this.editor.removeEventListener('compositionend', this.handleCompositionEnd.bind(this));
	}

	private startObserver() {
		this.observer.observe(this.editor, {
			childList: true,
			subtree: true,
			characterData: true,
			characterDataOldValue: true,
			attributes: true,
			attributeFilter: ['data-render-key', 'data-render-key-done']
		});
	}

	private isRenderDone(mutations: MutationRecord[]): boolean {
		for (let i = 0; i < mutations.length; i++) {
			let mutation = mutations[i];
			for (var j = 0; j < mutation.addedNodes.length; j++) {
				let node = mutation.addedNodes[j];
				if (node instanceof HTMLElement) {
					if (node.getAttribute('data-render-key-done'))
						return true;
				}
			}
		}

		return false;
	}

	private ignoreMutation(mutations: MutationRecord[], editor: HTMLElement) {
		for (let i = 0; i < mutations.length; i++) {
			let mutation = mutations[i];
			for (var current = mutation.target; current != editor; current = current.parentElement) {
				if (current instanceof HTMLElement) {
					if (current.classList.contains('metaline-controls') || current.classList.contains('line-controls')) {
						return true;
					}
				}
			}
		}

		return false;
	}

	private handleBeforeInput(event: InputEvent) {
		event.stopImmediatePropagation();

		let editRange: AnchorSelection<MetalineLineAnchor> | null = null;
		if (event.inputType == 'insertCompositionText') {
			//Findet gerade gar keine Komposition statt?
			if (!this.actualCompositionUpdate) {
				//Ungültiges Event
				return;
			}
			this.actualCompositionUpdate = false;

			//Ermittle den Bereich der Komposition
			let targetRange = event.getTargetRanges()[0];
			if (targetRange) {
				editRange = this.getLineSelection(targetRange);
			}
		}

		//Lese Daten
		let data = event.data;
		if (!event.data && event.dataTransfer) {
			data = event.dataTransfer.getData('text');
		}

		//Hole Auswahl
		console.log(event);
		let currentRange = getSelection().getRangeAt(0);

		//Wird ein Zeilenumbruch gelöscht?
		if (event.inputType == 'deleteContent' || event.inputType == 'deleteContentBackward' || event.inputType == 'deleteContentForward') {
			if (currentRange.endOffset == 0 && currentRange.startContainer !== currentRange.endContainer) {
				let rangeString = currentRange.toString();
				if (rangeString === '' || rangeString === "\n") {
					currentRange.collapse(event.inputType == 'deleteContentForward');
				}
			}
		}

		//Speichere die aktuelle Auswahl, um sie später wiederherstellen zu können
		this.revertSelection = new StaticRange(currentRange);

		//Kann das Event verhindert werden?
		if (event.cancelable && !this.isUncancelable(event)) {
			//Das Event wurde verhindert und die Bearbeitung kann durchgeführt werden
			event.preventDefault();
			this.callback(this, {
				inputType: event.inputType,
				editRange: editRange,
				data: data
			}, currentRange, this.pauseRender.bind(this));
		} else {
			//Das Event wurde nicht verhindert und muss zunächst rückgängig gemacht werden
			this.afterModification = (modification) => {
				//Die Änderung wurde rückgängig gemacht

				//Debounce bei Composition-Events
				let debounce = event.inputType == 'insertCompositionText';
				this.debouncer.debounce(() => {
					console.log("callback");

					//Lese aktuelle Selection
					let currentRange = getSelection().getRangeAt(0);

					this.callback(this, {
						inputType: event.inputType,
						editRange: editRange,
						data: data
					}, currentRange, this.pauseRender.bind(this));
				}, debounce);
			};
		}
	}

	private handleInput(event: InputEvent) {
		//Findet gerade gar keine Komposition statt?
		if (event.inputType == 'insertCompositionText') {
			if (!this.actualCompositionUpdate) {
				//Ungültiges Event
				return;
			}
		}

		if (!this.afterModification) {
			console.error("Unhandled input event!", event);
		}
	}

	private handleCompositionStart(event: CompositionEvent) {
		console.log('compositionstart', event);
	}

	private handleCompositionUpdate(event: CompositionEvent) {
		this.actualCompositionUpdate = true;
		console.log('compositionupdate', event);
	}

	private handleCompositionEnd(event: CompositionEvent) {
		console.log('compositionend', event);
	}

	private pauseRender(): (() => void) {
		this.revertModifications = false;
		return () => {
			this.revertModifications = true;
		};
	}

	private revertModificationAndRestoreSelection(modification: ObservedModification | null, selection: AbstractRange | null | undefined) {
		//Rückgängigmachen der Änderung
		if (modification) {
			this.revertModification(modification);
			this.observer.takeRecords();
		}
		
		//Wiederherstellen der Auswahl
		if (selection !== undefined) {
			this.restoreSelection(selection);
		}
	};

	private revertModification(modification: ObservedModification) {
		console.log("Revert modification", modification);
		console.log(JSON.stringify(modification, null, 2))
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
						} else {
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
				} else if (mutation.type == 'characterData') {
					mutation.target.nodeValue = mutation.oldValue;
				}

				mutations.splice(m, 1);
				m--;
			}
		}
	}

	private restoreSelection(selection: AbstractRange | null) {
		console.log("Restore selection", selection);
		let currentSelection = getSelection();
		if (selection) {
			if (currentSelection.rangeCount == 1) {
				let currentRange = currentSelection.getRangeAt(0);
				currentRange.setStart(selection.startContainer, selection.startOffset);
				currentRange.setEnd(selection.endContainer, selection.endOffset);
			} else {
				currentSelection.removeAllRanges();
				let currentRange = document.createRange();
				currentRange.setStart(selection.startContainer, selection.startOffset);
				currentRange.setEnd(selection.endContainer, selection.endOffset);
				currentSelection.addRange(currentRange);
			}
		} else {
			currentSelection.removeAllRanges();
		}
	}

	public getCurrentSelection(documentSelection?: Selection | undefined): AnchorSelection<MetalineLineAnchor> | null {
		if (documentSelection === undefined)
			documentSelection = getSelection();

		if (!documentSelection || documentSelection.rangeCount == 0)
			return null;
		let range = getSelection().getRangeAt(0);
		if (!range)
			return null;

		if (!this.editor.contains(range.startContainer) || !this.editor.contains(range.endContainer))
			return null;

		return this.getLineSelection(range);
	}

	public setCurrentSelection(lineSelection: AnchorSelection<MetalineLineAnchor> | null): Selection {
		//Nichts auswählen?
		let documentSelection = getSelection();
		if (!lineSelection) {
			if (documentSelection.rangeCount != 0)
				documentSelection.removeAllRanges();

			this.revertSelection = null;
			return documentSelection;
		}

		//Hole/erzeuge Range
		let range: Range;
		if (documentSelection.rangeCount) {
			range = documentSelection.getRangeAt(0);
		} else {
			range = document.createRange();
			documentSelection.addRange(range);
		}

		//Setze Range
		this.setLineSelectionRange(documentSelection, range, lineSelection);

		//Speichere die Auswahl, um sie später wiederherstellen zu können
		this.revertSelection = new StaticRange(range);
		return documentSelection;
	}

	public setLineSelectionRange(documentSelection: Selection, range: Range, lineSelection: AnchorSelection<MetalineLineAnchor> | AnchorSelection<LineNodeAnchor>) {
		let startLine = lineSelection.start.lineNode;
		let endLine = lineSelection.end.lineNode;

		console.log('selecting', lineSelection);

		if (!(startLine instanceof HTMLElement && endLine instanceof HTMLElement)) {
			let metalineLineSelection = <AnchorSelection<MetalineLineAnchor>>lineSelection;

			let startMetaline = startLine ? null : this.editor.querySelector(`.metaline[data-metaline="${metalineLineSelection.start.metaline}"]`);
			let endMetaline = metalineLineSelection.end.metaline == metalineLineSelection.start.metaline ? startMetaline
				: endLine ? null
					: this.editor.querySelector(`.metaline[data-metaline="${metalineLineSelection.end.metaline}"]`);
			startLine ??= findLine(startMetaline, metalineLineSelection.start.line);
			endLine ??= metalineLineSelection.end.metaline == metalineLineSelection.start.metaline && metalineLineSelection.end.line == metalineLineSelection.start.line ? startLine
				: findLine(endMetaline, metalineLineSelection.end.line);
		}

		//Setze die Range
		let start = SelectionHelper.getNodeAndOffset(startLine, lineSelection.start.offset);
		range.setStart(start.node, start.offset);
		let end = startLine === endLine && lineSelection.end.offset == lineSelection.start.offset ? start
			: SelectionHelper.getNodeAndOffset(endLine, lineSelection.end.offset);
		range.setEnd(end.node, end.offset);

		//if (lineSelection.start.offset < 0 || lineSelection.start.offset >= startLine.textContent.length) {
		//	/*let current = startLine;
		//	while (current.lastChild)
		//		current = current.lastChild;
		//	range.setStart(current, current.textContent.length);
		//	range.setEnd(current, current.textContent.length);*/
		//	range.setStartAfter(getLastTextNode(startLine));
		//	range.setEnd(range.startContainer, range.startOffset);

		//	for (let i = -1; i > lineSelection.start.offset; i--)
		//		documentSelection.modify('move', 'backward', 'character');
		//} else {
		//	range.setStartBefore(getFirstTextNode(startLine));
		//	range.setEnd(range.startContainer, range.startOffset);

		//	for (var i = 0; i < lineSelection.start.offset; i++)
		//		documentSelection.modify('move', 'forward', 'character');
		//}

		//if (startLine === endLine) {
		//	if (lineSelection.start.offset == lineSelection.end.offset) {
		//		return;
		//	} else if (lineSelection.end.offset >= 0) {
		//		for (var i = lineSelection.start.offset; i < lineSelection.end.offset; i++)
		//			documentSelection.modify('extend', 'forward', 'character');
		//		return;
		//	}
		//}

		//if (lineSelection.end.offset < 0 || lineSelection.end.offset >= endLine.textContent.length) {
		//	/*let current = endLine;
		//	while (current.lastChild)
		//		current = current.lastChild;
		//	range.setEnd(current, current.textContent.length);*/
		//	range.setEndAfter(getLastTextNode(endLine));

		//	for (let i = -1; i > lineSelection.end.offset; i--)
		//		documentSelection.modify('extend', 'backward', 'character');
		//} else {
		//	range.setEndBefore(getFirstTextNode(endLine));

		//	for (var i = 0; i < lineSelection.end.offset; i++)
		//		documentSelection.modify('extend', 'forward', 'character');
		//}


		//function getFirstTextNode(node: Node): Text | null {
		//	let iterator = document.createNodeIterator(node, NodeFilter.SHOW_TEXT);
		//	return <Text>iterator.nextNode();
		//}

		//function getLastTextNode(node: Node): Text | null {
		//	let iterator = document.createNodeIterator(node, NodeFilter.SHOW_TEXT);
		//	let last = <Text>iterator.nextNode();
		//	let next: Node | null;
		//	while (next = iterator.nextNode())
		//		last = <Text>next;
		//	return last;
		//}



		//let start = this.getNode(startLine, lineSelection.start.offset);
		//let end = startLine === endLine && lineSelection.end.offset == lineSelection.start.offset ? start
		//	: this.getNode(endLine, lineSelection.end.offset);

		//if (start.node instanceof Text && start.offset > start.node.length)
		//	range.setStart(start.node, start.node.length);
		//else if (start.node instanceof HTMLElement && start.offset > start.node.childElementCount)
		//	range.setStart(start.node, start.node.childElementCount);
		//else
		//	range.setStart(start.node, start.offset);

		//if (end.node instanceof Text && end.offset > end.node.length)
		//	range.setEnd(end.node, end.node.length);
		//else if (end.node instanceof HTMLElement && end.offset > end.node.childElementCount)
		//	range.setEnd(end.node, end.node.childElementCount);
		//else
		//	range.setEnd(end.node, end.offset);

		function findLine(metaline: Element, line: number): Element {
			if (line === ModificationEditor.FIRST_LINE_ID) {
				return metaline.querySelector('.line[data-line]');
			} else if (line === ModificationEditor.LAST_LINE_ID) {
				let lines = metaline.querySelectorAll('.line[data-line]');
				return lines[lines.length - 1];
			}

			return metaline.querySelector(`.line[data-line="${line}"]`);
		}
	}

	public getLineSelection(range: AbstractRange, includeNode?: boolean): AnchorSelection<MetalineLineAnchor> {
		//Finde Offset des Startknotens
		let start = this.getTextIndex(range.startContainer, this.getLineInfo, range.startOffset);

		//Finde Offset des Endknotens
		let end = range.endContainer !== range.startContainer ? this.getTextIndex(range.endContainer, this.getLineInfo, range.endOffset) : {
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
		}
	}

	private getTextIndex<T>(node: Node, stopCondition: (node: Node) => T | null, offset?: number): TextIndex<T> | null {
		if (!offset)
			offset = 0;

		let data = stopCondition(node);
		let current: Node;
		for (current = node; current && !data; current = current.parentElement, data = stopCondition(current)) {
			//Addiere die Länge aller Siblings
			for (var sibling = current.previousSibling; sibling; sibling = sibling.previousSibling) {
				if (sibling.nodeType == Node.COMMENT_NODE)
					continue;

				offset += sibling.textContent?.length || 0;
			}
		}

		if (!data)
			return null;

		return {
			node: current,
			offset: offset,
			data: data
		};
	}

	private getNode(node: Node, offset: number): NodeSelectionAnchor {
		if (offset < 0)
			return getFromEnd(node, -offset - 1);
		else
			return getFromStart(node, offset);

		function getFromStart(node: Node, offset: number) {
			if (!(node instanceof HTMLElement))
				return {
					node: node,
					offset: offset,
				};

			let currentOffset = 0;
			for (let i = 0; i < node.childNodes.length; i++) {
				let child = node.childNodes[i];
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

		function getFromEnd(node: Node, offset: number) {
			if (!(node instanceof HTMLElement))
				return {
					node: node,
					offset: node.textContent.length - offset,
				};

			let currentEndOffset = 0;
			for (let i = node.childNodes.length - 1; i >= 0; i--) {
				let child = node.childNodes[i];
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
	}

	private getLineInfo(node: Node): LineIdentifier | null {
		if (!(node instanceof HTMLElement))
			return null;
		let nodeElement = <HTMLElement>node;

		let lineId: number | null = null;
		if (node.classList.contains('line')) {
			lineId = parseInt(nodeElement.getAttribute('data-line'));
			nodeElement = nodeElement.parentElement.parentElement;
		} else if (nodeElement.classList.contains('metaline-lines')) {
			nodeElement = nodeElement.parentElement;
		} else if (!nodeElement.classList.contains('metaline')) {
			return null;
		}

		let metalineId = nodeElement.getAttribute('data-metaline');
		return {
			metaline: metalineId,
			line: lineId
		};
	}
}

class SelectionObserver implements Destructible {
	private supportsMultipleRanges = false; // /Firefox/.test(navigator.userAgent);
	private customSelection: HTMLElement;
	private editor: HTMLElement;
	private editorWrapper: HTMLElement;

	private isPaused: boolean;
	private justSelected: boolean;

	constructor(private modificationEditor: ModificationEditor) {
		this.editor = modificationEditor.editor;
		this.editorWrapper = this.editor.parentElement;
		this.customSelection = this.editorWrapper.querySelector('.custom-selection');

		document.addEventListener('selectionchange', this.handleSelectionChange.bind(this));
		this.editor.addEventListener('dragstart', this.handleDragStart.bind(this));
	}

	destroy(): void {
		document.removeEventListener('selectionchange', this.handleSelectionChange.bind(this));
		this.editor.removeEventListener('dragstart', this.handleDragStart.bind(this));
	}

	public triggerJustSelected() {
		if (this.justSelected) {
			this.justSelected = false;
			return true;
		}

		return false;
	}

	public refreshSelection() {
		this.isPaused = false;
		this.processSelectionChange();
	}

	public pauseObservation() {
		this.isPaused = true;
	}

	private handleSelectionChange() {
		if (this.isPaused) {
			requestAnimationFrame((() => {
				this.isPaused = false;
			}).bind(this));
			return;
		}
		
		this.processSelectionChange();
	}

	private processSelectionChange() {
		const documentSelection = getSelection();
		if (!documentSelection || documentSelection.rangeCount == 0)
			return this.resetCustomSelections();

		//Mehrere Ranges?
		let range = documentSelection.getRangeAt(0);
		if (documentSelection.rangeCount > 1) {
			let firstRange = range;
			let lastRange = documentSelection.getRangeAt(documentSelection.rangeCount - 1);
			if (lastRange.startContainer.compareDocumentPosition(firstRange.startContainer) & Node.DOCUMENT_POSITION_FOLLOWING) {
				firstRange = lastRange;
				lastRange = range;
			}

			range = document.createRange();
			if (firstRange.endContainer.compareDocumentPosition(firstRange.startContainer) & Node.DOCUMENT_POSITION_FOLLOWING) {
				range.setStart(lastRange.endContainer, lastRange.endOffset);
				range.setEnd(firstRange.startContainer, firstRange.startOffset);
			} else {
				range.setStart(firstRange.startContainer, firstRange.startOffset);
				range.setEnd(lastRange.endContainer, lastRange.endOffset);
			}
		}

		//Ist die Auswahl nicht im Editor?
		if (!this.editor.contains(range.startContainer) || !this.editor.contains(range.endContainer))
			return this.resetCustomSelections();
			
		//Finde die Metazeilen/Zeilen
		const lineSelection = this.modificationEditor.getLineSelection(range, true);

		//Mehr als eine Metazeile?
		if (lineSelection.start.metaline != lineSelection.end.metaline)
			return this.resetCustomSelections();

		//Metazeile mit Box-Auswahl?
		const metaline = lineSelection.start.lineNode?.parentElement?.parentElement;
		if (!metaline)
			return this.resetCustomSelections();
		const selectionType = metaline.getAttribute('data-selection');
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
	}

	private resetCustomSelections() {
		//Blende die Box aus
		this.customSelection.className = 'custom-selection';
		this.justSelected = true;
	}

	private adjustBoxSelection(documentSelection: Selection, range: Range, lineSelection: AnchorSelection<MetalineLineAnchor>) {
		//Nicht unterstützt?
		if (!this.supportsMultipleRanges)
			return emulateBoxSelection(this, documentSelection, range);

		//Invertiere ggf. die Auswahl
		if (lineSelection.end.lineNode.compareDocumentPosition(lineSelection.start.lineNode) & Node.DOCUMENT_POSITION_FOLLOWING) {
			lineSelection = {
				start: lineSelection.end,
				end: lineSelection.start,
			};
		}

		//Ermittle die Zeilen
		const startLine = lineSelection.start.lineNode;
		const endLine = lineSelection.end.lineNode;
		if (!(startLine instanceof HTMLElement) || !(endLine instanceof HTMLElement))
			return this.resetCustomSelections();
		let lines = [startLine];
		for (let current = startLine.nextSibling; current != endLine; current = current.nextSibling) {
			if (!(current instanceof HTMLElement) || !current.classList.contains('line'))
				continue;

			lines.push(current);
		}
		lines.push(endLine);

		//Ermittle die Offsets
		const startOffset = lineSelection.start.offset;
		const endOffset = lineSelection.end.offset;

		//Erzeuge die Ranges
		documentSelection.removeAllRanges();
		for (let i = 0; i < lines.length; i++) {
			//Erzeuge die Range
			let line = lines[i];
			let lineRange = document.createRange();

			//Füge die Range hinzu
			documentSelection.addRange(lineRange);

			//Setze die Range
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

		//Hat das nicht funktioniert?
		if (documentSelection.rangeCount != lines.length) {
			this.supportsMultipleRanges = false;
			emulateBoxSelection(this, documentSelection, range);
		}

		function emulateBoxSelection(self: SelectionObserver, documentSelection: Selection, range: Range) {
			//Startzelle
			let startCell = range.startContainer;
			let behindStartCell = false;
			if (startCell.nodeType == Node.TEXT_NODE) {
				if (range.startOffset >= startCell.textContent.length) {
					startCell = startCell.parentElement;
					if (startCell.nextSibling) {
						startCell = startCell.nextSibling;
					} else {
						behindStartCell = true;
					}
				} else {
					startCell = startCell.parentElement;
				}
			}

			//Endzelle
			let beforeEndCell = false;
			let insideEndCell = false;
			let endCell = range.endContainer;
			if (endCell.nodeType != Node.TEXT_NODE) {
				beforeEndCell = true;
			} else {
				if (range.endOffset < range.endContainer.textContent.length)
					insideEndCell = true;
				endCell = endCell.parentElement;
			}

			//Hole die Rechtecke
			let startRect = (<Element>startCell).getBoundingClientRect();
			if (behindStartCell)
				startRect = new DOMRect(startRect.right, startRect.top, 0, startRect.height);
			let endRect = (<Element>endCell).getBoundingClientRect();
			if (beforeEndCell)
				endRect = new DOMRect(endRect.left, endRect.top, 0, endRect.height);

			//Nichts ausgewählt?
			if (range.collapsed) {
				endCell = startCell;
				endRect = startRect;
			}

			//Finde die Grenzen
			let x, width: number;
			if (endRect.left >= startRect.left) {
				x = startRect.left;
				width = endRect.right - x;
			} else {
				if (!insideEndCell) {
					if ((<any>endCell.nextSibling)?.getBoundingClientRect) {
						endCell = endCell.nextSibling;
						endRect = (<Element>endCell).getBoundingClientRect();
						endRect = new DOMRect(endRect.left, endRect.top, 0, endRect.height);
					} else {
						endRect = new DOMRect(endRect.right, endRect.top, 0, endRect.height);
					}
				}

				if (!behindStartCell) {
					if ((<any>startCell.previousSibling)?.getBoundingClientRect) {
						startCell = startCell.previousSibling;
						startRect = (<Element>startCell).getBoundingClientRect();
						startRect = new DOMRect(startRect.right, startRect.top, 0, startRect.height);
					} else {
						startRect = new DOMRect(startRect.left, startRect.top, 0, startRect.height);
					}
				}

				x = endRect.left;
				width = startRect.right - x;
			}

			let y = startRect.top;
			let height = endRect.bottom - y;

			//Positioniere die Box
			let wrapperRect = self.editorWrapper.getBoundingClientRect();
			let top = y - wrapperRect.top;
			let left = x - wrapperRect.left;
			let newPosition = top.toFixed(2) + ';' + left.toFixed(2) + ';' + width.toFixed(2) + ';' + height.toFixed(2);
			if ((<any>self.customSelection)['data-position'] != newPosition) {
				self.customSelection.style.top = top + 'px';
				self.customSelection.style.left = left + 'px';
				self.customSelection.style.width = width + 'px';
				self.customSelection.style.height = height + 'px';
				(<any>self.customSelection)['data-position'] = newPosition;
				self.justSelected = true;
			}
			
			//Mache die Box sichtbar
			if (self.customSelection.className != 'custom-selection custom-selection-box') {
				self.customSelection.className = 'custom-selection custom-selection-box';
				self.justSelected = true;
			}
		}
    }

	private handleDragStart(event: DragEvent) {
		if (!(event.target instanceof Node))
			return;

		for (let current = event.target; current && current != this.editor; current = current.parentElement) {
			if (!(current instanceof HTMLElement))
				continue;

			let selectionType = current.getAttribute('data-selection');
			if (selectionType == 'box') {
				event.preventDefault();
				event.stopPropagation();
				return false;
			}
		}
	}
}

class SelectionHelper {
	static getNodeAndOffset(node: Node, offset: number) {
		if (offset < 0)
			return SelectionHelper.getNodeAndOffsetFromEnd(node, offset);

		let iterator = document.createNodeIterator(node, NodeFilter.SHOW_TEXT);
		let currentOffset = 0;
		let current = iterator.nextNode();
		while (current && currentOffset + current.textContent.length < offset) {
			currentOffset += current.textContent.length;
			let next = iterator.nextNode();
			if (!next)
				return {
					node: current,
					offset: current.textContent.length,
				};
			current = next;
		}

		return {
			node: current ?? node,
			offset: offset - currentOffset,
		};
	}

	private static getNodeAndOffsetFromEnd(node: Node, offset: number) {
		let iterator = document.createNodeIterator(node, NodeFilter.SHOW_TEXT);
		let allNodes = [];
		let current = iterator.nextNode();
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
		let currentOffset = 0;
		for (let i = allNodes.length - 1; i >= 0; i--) {
			current = allNodes[i];
			if (currentOffset + current.textContent.length >= offset)
				break;

			currentOffset += current.textContent.length;
		}

		return {
			node: current,
			offset: current.textContent.length - (offset - currentOffset),
		};
	}
}
