using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.AspNetCore.Components.WebView.Maui;

namespace Skinnix.Compoetry.Maui;

public interface IMauiUiService : INotifyPropertyChanged
{
	BlazorWebView? LoadedBlazorWebView { get; set; }
	RootComponent? RootComponent { get; set; }
}

internal partial class MauiUiService : ObservableObject, IMauiUiService, IDisposable
{
	[ObservableProperty] public partial BlazorWebView? LoadedBlazorWebView { get; set; }
	[ObservableProperty] public partial RootComponent? RootComponent { get; set; }

	public void Dispose()
	{
		LoadedBlazorWebView?.DisconnectHandlers();
	}
}
