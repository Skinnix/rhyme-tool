using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Skinnix.RhymeTool.Client.Services;

public interface IDialogService
{
	Task ShowErrorAsync(string message, string? title);
	Task<bool> ConfirmAsync(string message, string? title);
}

class DialogService : IDialogService
{
	private readonly IJSRuntime js;

	public DialogService(IJSRuntime js)
	{
		this.js = js;
	}

	public Task ShowErrorAsync(string message, string? title)
		=> js.InvokeVoidAsync("alert", message).AsTask();

	public Task<bool> ConfirmAsync(string message, string? title)
		=> js.InvokeAsync<bool>("confirm", message).AsTask();
}
