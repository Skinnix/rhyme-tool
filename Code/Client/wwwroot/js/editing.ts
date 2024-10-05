declare interface Destructible {
	destroy(): void;
}

declare interface AwaitingModification {

}

declare interface ObservedModification {
	mutations: MutationRecord[];
}

declare interface LineIdentifier {
	metaline: string,
	line: number | null,
}

declare interface TextIndex<T> {
	offset: number;
	data: T;
}

declare interface NodeSelectionAnchor {
	node: Node,
	offset: number,
}

declare interface NodeSelection {
	start: NodeSelectionAnchor,
	end: NodeSelectionAnchor,
}

declare interface LineSelectionAnchor extends LineIdentifier {
	offset: number,
}

declare interface LineSelection {
	start: LineSelectionAnchor,
	end: LineSelectionAnchor,
}

declare interface ModificationData {
	inputType: string,
	data: string,
	editRange?: LineSelection,
}

declare interface MetalineEditResult {
	success: boolean,
	willRender: boolean,
	selection: LineSelection | null,
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
	
	constructor(private editor: HTMLElement, private callback: EditorCallback) {
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

			if (self.revertModifications) {
				self.revertModificationAndRestoreSelection({
					mutations: mutations
				}, self.revertSelection);
			} else {
				//Render fertig?
				if (self.isRenderDone(mutations)) {
					console.log("render done");
					if (self.revertModifications) {
						console.error('revert conflict');
					}
					self.revertModifications = true;
				}
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

		let editRange: LineSelection | null = null;
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
				data: event.data
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
						data: event.data
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

	public getCurrentSelection(): LineSelection | null {
		let range = getSelection()?.getRangeAt(0);
		if (!range)
			return null;

		return this.getLineSelection(range);
	}

	public setCurrentSelection(selection: LineSelection | null): Selection {
		let documentSelection = getSelection();
		if (!selection) {
			if (documentSelection.rangeCount != 0)
				documentSelection.removeAllRanges();

			return documentSelection;
		}

		let startMetaline = this.editor.querySelector(`.metaline[data-metaline="${selection.start.metaline}"]`);
		let endMetaline = selection.end.metaline == selection.start.metaline ? startMetaline
			: this.editor.querySelector(`.metaline[data-metaline="${selection.end.metaline}"]`);
		let startLine = findLine(startMetaline, selection.start.line);
		let endLine = selection.end.metaline == selection.start.metaline && selection.end.line == selection.start.line ? startMetaline
			: findLine(endMetaline, selection.end.line);

		let start = this.getNode(startLine, selection.start.offset);
		let end = selection.end.metaline == selection.start.metaline && selection.end.line == selection.start.line && selection.end.offset == selection.start.offset ? start
			: this.getNode(endLine, selection.end.offset);

		let range: Range;
		if (documentSelection.rangeCount) {
			range = documentSelection.getRangeAt(0);
			range.setStart(start.node, start.offset);
			range.setEnd(end.node, end.offset);
		} else {
			range = document.createRange();
			range.setStart(start.node, start.offset);
			range.setEnd(end.node, end.offset);
			documentSelection.addRange(range);
		}

		this.revertSelection = new StaticRange(range);
		return documentSelection;

		function findLine(metaline: Element, line: number): Element {
			if (line === ModificationEditor.FIRST_LINE_ID) {
				return metaline.querySelector('.line[data-line]');
			} else if (line === ModificationEditor.LAST_LINE_ID) {
				let lines = metaline.querySelectorAll('.line[data-line]');
				return lines[lines.length - 1];
			}

			return metaline.querySelector(`.line[data-line="${selection.start.line}"]`);
		}
	}

	private getLineSelection(range: AbstractRange): LineSelection {
		//Finde Offset des Startknotens
		let start = this.getTextIndex(range.startContainer, this.getLineInfo, range.startOffset);

		//Finde Offset des Endknotens
		let end = range.endContainer !== range.startContainer ? this.getTextIndex(range.endContainer, this.getLineInfo, range.endOffset) : {
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
		}
	}

	private getTextIndex<T>(node: Node, stopCondition: (node: Node) => T | null, offset?: number): TextIndex<T> {
		if (!offset)
			offset = 0;

		let data = stopCondition(node);
		for (let current = node; !data; current = current.parentElement, data = stopCondition(current)) {
			//Addiere die Länge aller Siblings
			for (var sibling = current.previousSibling; sibling; sibling = sibling.previousSibling) {
				if (sibling.nodeType == Node.COMMENT_NODE)
					continue;

				offset += sibling.textContent?.length || 0;
			}
		}

		return {
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
		if (nodeElement.classList.contains('line')) {
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