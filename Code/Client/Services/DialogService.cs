using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Skinnix.RhymeTool.Client.Services;

public interface IDialogService
{
	ValueTask ShowErrorAsync(string message, string? title);
	ValueTask<bool> ConfirmAsync(string message, string? title);
	ValueTask ShowToast(string message, string title = "Chords") => ShowToast(message, title, TimeSpan.FromSeconds(5));
	ValueTask ShowToast(string message, string title, TimeSpan delay);
}

class DialogService : IDialogService
{
	private readonly IJSRuntime js;

	public DialogService(IJSRuntime js)
	{
		this.js = js;
	}

	public ValueTask ShowErrorAsync(string message, string? title)
		=> js.InvokeVoidAsync("alert", message);

	public ValueTask<bool> ConfirmAsync(string message, string? title)
		=> js.InvokeAsync<bool>("confirm", message);

	public ValueTask ShowToast(string message, string title, TimeSpan delay)
		=> js.InvokeVoidAsync("showToast", message, title, delay.TotalMilliseconds);
}
