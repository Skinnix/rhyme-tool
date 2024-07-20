using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace Skinnix.RhymeTool.Client.Services;

public interface IDialogService
{
	Task ShowError(string message, string? title);
}

class DialogService : IDialogService
{
	private readonly IJSRuntime js;

	public DialogService(IJSRuntime js)
	{
		this.js = js;
	}

	public Task ShowError(string message, string? title)
	{
		return js.InvokeVoidAsync("alert", message).AsTask();
	}
}
