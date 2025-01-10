class SelectionObserver implements Destructible {
	private static needsLineFix = /Firefox/.test(navigator.userAgent);
	private supportsMultipleRanges = false;
	private customSelection: HTMLElement;
	private editor: HTMLElement;
	private editorWrapper: HTMLElement;

	private isPaused: boolean;
	private justSelected: boolean;

	constructor(private modificationEditor: ModificationEditor) {
		this.editor = modificationEditor.editor;
		this.editorWrapper = this.editor.parentElement!;
		if (!this.editorWrapper)
			throw new Error("Editor hat kein Elternelement");
		this.customSelection = this.editorWrapper.querySelector('.custom-selection')!;
		if (!this.customSelection)
			throw new Error("Custom-Selection-Element nicht gefunden");

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
		requestAnimationFrame(this.processSelectionChange.bind(this));
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

		//Aktualisiere Editor
		this.modificationEditor.updateFromElement(false, documentSelection);

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

		if (SelectionObserver.needsLineFix && this.fixSelection(documentSelection, range))
			return;

		//Finde die Metazeilen/Zeilen
		const lineSelection = this.modificationEditor.getLineSelection(range, true);

		//Keine oder mehr als eine Metazeile?
		if (!lineSelection || lineSelection.start.metaline != lineSelection.end.metaline)
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

	private fixSelection(documentSelection: Selection, range: Range): Boolean {
		let modified = false;
		let collapsed = range.collapsed;
		let newStart: { node: Node, offset: number } | null = null;
		if (!(range.startContainer instanceof Text)) {
			if (range.startContainer.childNodes.length > range.startOffset) {
				let node = range.startContainer.childNodes[range.startOffset];
				while (node.firstChild)
					node = node.firstChild;

				newStart = { node, offset: 0 };
				modified = true;
			}
		}

		if (!collapsed) {
			if (!(range.endContainer instanceof Text)) {
				if (range.endContainer.childNodes.length > range.endOffset) {
					let node = range.endContainer.childNodes[range.startOffset];
					while (node.lastChild)
						node = node.lastChild;

					range.setEnd(node, node.textContent?.length ?? 0);
					modified = true;
				}
			}

			if (newStart)
				range.setStart(newStart.node, newStart.offset);
		} else if (newStart) {
			range.setStart(newStart.node, newStart.offset);
			range.setEnd(newStart.node, newStart.offset);
		}

		let startContainer = range.endContainer;
		let startNode = startContainer instanceof HTMLElement ? startContainer : startContainer.parentElement;
		if (startNode?.classList.contains('suffix')) {
			//Das passiert nur bei doppelt auswählbaren Knoten (in Firefox)
			if ((startNode.previousSibling as HTMLElement)?.classList.contains('suffix') === false) {
				startNode = <HTMLElement>startNode.previousSibling;
				let node = SelectionHelper.getNodeAndOffset(startNode, -1);
				range.setEnd(node.node, node.offset);
				if (collapsed)
					range.setStart(node.node, node.offset);
				modified = true;
			} else {
				range.setEnd(startContainer, 0);
				if (collapsed)
					range.setStart(startContainer, 0);
				modified = true;
			}
		}

		if (modified)
			console.log('fixed');
		return modified;
	}

	private resetCustomSelections() {
		//Blende die Box aus
		this.customSelection.className = 'custom-selection';
		this.justSelected = true;
	}

	private adjustBoxSelection(documentSelection: Selection, range: Range, lineSelection: AnchorSelection<MetalineLineAnchor>) {
		//Nicht unterstützt?
		if (!this.supportsMultipleRanges)
			return emulateBoxSelection(this, documentSelection, range, lineSelection);

		//Invertiere ggf. die Auswahl
		if (lineSelection.start.lineNode && lineSelection.end.lineNode && lineSelection.end.lineNode.compareDocumentPosition(lineSelection.start.lineNode) & Node.DOCUMENT_POSITION_FOLLOWING) {
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
			emulateBoxSelection(this, documentSelection, range, lineSelection);
		}

		function emulateBoxSelection(self: SelectionObserver, documentSelection: Selection, range: Range, lineSelection: AnchorSelection<MetalineLineAnchor>) {
			let startCell: Node | null | undefined = range.startContainer;
			let endCell: Node | null | undefined = range.endContainer;
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
				startCell = startCell?.parentNode;
			if (!(endCell instanceof HTMLElement))
				endCell = endCell?.parentNode;

			let selectionBox: DOMRect;
			if (range.collapsed) {
				//Keine Auswahl, verwende die linke Kante der Startzelle
				if (range.startOffset != 0)
					startCell = startCell?.nextSibling;

				if (!(startCell instanceof HTMLElement))
					return self.resetCustomSelections();

				selectionBox = startCell.getBoundingClientRect();
			} else {
				//Vertikal?
				if (lineSelection.start.offset == lineSelection.end.offset) {
					endCell = endCell?.nextSibling;
				}

				//Geht die Box nach rechts?
				if (lineSelection.end.offset >= lineSelection.start.offset) {
					//Verwende die linke Kante der Startzelle
					if (range.startOffset != 0)
						startCell = startCell?.nextSibling;

					//Verwende die rechte Kante der Endzelle
					if (range.endOffset == 0)
						endCell = endCell?.previousSibling;
				} else {
					//Verwende die rechte Kante der Startzelle
					if (range.startOffset == 0)
						startCell = startCell?.previousSibling;

					//Verwende die linke Kante der Endzelle
					if (range.endOffset != 0)
						endCell = endCell?.nextSibling;
				}

				if (!(startCell instanceof HTMLElement) || !(endCell instanceof HTMLElement))
					return self.resetCustomSelections();

				//Berechne die Bounding Box
				let startBox = startCell.getBoundingClientRect();
				let endBox = endCell.getBoundingClientRect();
				selectionBox = SelectionObserver.getBoundingBox(startBox, endBox);
			}

			//Setze die Position
			setBoxPosition(self, selectionBox);

			function setBoxPosition(self: SelectionObserver, box: DOMRect) {
				//Positioniere die Box
				let editorRect = self.editor.getBoundingClientRect();
				let left = box.left - editorRect.left;
				let top = box.top - editorRect.top;
				let width = box.width;
				let height = box.height;
				let newPosition = top.toFixed(0) + ';' + left.toFixed(0) + ';' + documentSelection.toString().length;
				let newPositionFull = newPosition + ';' + width.toFixed(0) + ';' + height.toFixed(0);
				let currentPosition = (<any>self.customSelection)['data-position'];
				if (currentPosition != newPositionFull) {
					self.customSelection.style.top = top + 'px';
					self.customSelection.style.left = left + 'px';
					self.customSelection.style.width = width + 'px';
					self.customSelection.style.height = height + 'px';
					(<any>self.customSelection)['data-position'] = newPositionFull;

					if (!currentPosition?.startsWith(newPosition))
						self.justSelected = true;
				}

				//Mache die Box sichtbar
				if (self.customSelection.className != 'custom-selection custom-selection-box') {
					self.customSelection.className = 'custom-selection custom-selection-box';
					self.justSelected = true;
				}
			}
		}
	}

	private static getBoundingBox(...args: DOMRect[]): DOMRect {
		let left = Number.MAX_VALUE;
		let top = Number.MAX_VALUE;
		let right = 0;
		let bottom = 0;
		for (let rect of args) {
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
	}

	private handleDragStart(event: DragEvent) {
		if (!(event.target instanceof Node))
			return;

		for (let current: Node | null = event.target; current && current != this.editor; current = current.parentElement) {
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
