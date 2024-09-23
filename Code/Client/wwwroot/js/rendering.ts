class ActionQueue<T = void> implements PromiseLike<T> {
	private queue: Promise<any> | null = null;
	private resolveRender: (() => void) | null = null;
	private awaitRenderPromise: Promise<void> | null = null;

	get isBusy(): boolean {
		return !!this.queue;
	}

	public then<TResult1 = T, TResult2 = never>(
			onfulfilled?: ((value: T) => TResult1 | PromiseLike<TResult1>) | undefined | null,
			onrejected?: ((reason: any) => TResult2 | PromiseLike<TResult2>) | undefined | null): PromiseLike<TResult1 | TResult2> {
		let self = this;
		let promise: Promise<any> = null;
		function checkRemove(next: Promise<any> | any): Promise<any> | void {
			if (next && next.then) {
				return next.then((afterNext: any) => {
					return checkRemove(afterNext);
				});
			}

			if (self.queue === promise) {
				self.queue = null;
				console.log("Queue is empty");
			} else {
				//console.log("Queue continues");
			}

			return next;
		};
		let handler = (value: T) => checkRemove(onfulfilled(value));

		if (this.queue) {
			this.queue = promise = this.queue.then(handler);
		} else {
			this.queue = promise = new Promise((resolve, reject) => {
				resolve(handler(undefined));
			});
		}

		return <PromiseLike<TResult1 | TResult2>>this;
	}

	public prepareForNextRender(): void {
		if (this.resolveRender)
			return;

		let self = this;
		this.awaitRenderPromise = new Promise((resolve, reject) => {
			self.resolveRender = () => {
				resolve();
				self.resolveRender = null;
				self.awaitRenderPromise = null;
			};
		});
	}

	public awaitRender(): PromiseLike<void> {
		if (!this.awaitRenderPromise)
			return;

		console.log("Awaiting next render");
		let promise = this.awaitRenderPromise;
		return promise;
	}

	public notifyRender(): void {
		console.log("Render called");
		if (!this.resolveRender)
			return;

		this.resolveRender();
	}
}