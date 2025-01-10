declare class BlazorDotNetReference {
	invokeMethod<T = any>(method: string, ...args: any[]): T;
	invokeMethodAsync<T = any>(method: string, ...args: any[]): Promise<T>;
}

declare const bootstrap: any;
