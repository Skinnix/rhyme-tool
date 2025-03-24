using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Skinnix.Compoetry.Maui.Views;
using Skinnix.RhymeTool.Client;
using Skinnix.RhymeTool.Client.Services.Files;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.Compoetry.Maui.Pages;
using Skinnix.Compoetry.Maui.Pages.Document;
using Microsoft.Maui.LifecycleEvents;
using Skinnix.Compoetry.Maui.IO;
using Skinnix.RhymeTool.Client.Services.Preferences;
using Skinnix.Compoetry.Maui.Pages.Files;
using System.Diagnostics;
using UraniumUI;
using UraniumUI.Options;
using UraniumUI.Material.Controls;
using System.Globalization;
using Skinnix.RhymeTool.Data;
using InputKit.Shared.Controls;
using System.Reflection;
using UraniumUI.Extensions;
using UraniumUI.Resources;
using System.ComponentModel;
using Skinnix.RhymeTool.Configuration;

namespace Skinnix.Compoetry.Maui;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.UseUraniumUI()
			.UseUraniumUIMaterial()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				fonts.AddFont("Font-Awesome-6-Free-Regular-400.otf", "FontAwesome");
				fonts.AddFont("Font-Awesome-6-Free-Solid-900.otf", "FontAwesomeSolid");
				fonts.AddFontAwesomeIconFonts();
			});

		builder.Services.AddMauiBlazorWebView();

		//RhymeTool
		builder.Services.AddRhymeToolClient();

		//HTTP-Client
		builder.Services.AddScoped(_ => new HttpClient());

		builder.Services.AddSingleton<IMauiUiService, MauiUiService>();
		builder.Services.AddTransient<IDebugDataService, MauiDebugDataService>();

		//Service-Überschreibungen
		builder.Services.AddSingleton<IDocumentFileService, MauiDocumentFileService>();
		builder.Services.AddTransient<IPreferencesService, MauiPreferencesService>();

		//UraniumUI
		builder.Services.Configure<AutoFormViewOptions>(options =>
		{
			options.PropertyNameFactory = property =>
			{
				return property.GetCustomAttribute<DescriptionAttribute>()?.Description
					?? property.GetCustomAttribute<ConfigurableAttribute>()?.Name
					?? property.Name;
			};
			options.EditorMapping[typeof(Enum)] = EditorForEnum;
		});

#if DEBUG
		builder.Logging.AddDebug();
		builder.Services.AddBlazorWebViewDeveloperTools();
#endif

		#region ViewModels
		builder.Services.AddTransient<AppWindowVM>();
		builder.Services.AddTransient<MainPageVM>();
		builder.Services.AddTransient<PreferencesPageVM>();

		builder.Services.AddTransient<FileExplorerPageVM>();

		builder.Services.AddTransient<RendererPageVM>();
		builder.Services.AddTransient<EditorPageVM>();
		#endregion

		builder.ConfigureMauiHandlers(handlers =>
		{
#if WINDOWS
			handlers.AddHandler<InnerFlyoutPage, Skinnix.Compoetry.Maui.Platforms.Windows.InnerFlyoutPageHandler>();
			handlers.AddHandler<OuterFlyoutPage, Skinnix.Compoetry.Maui.Platforms.Windows.OuterFlyoutPageHandler>();
#endif
		});

		return builder.Build();
	}

	private static View EditorForEnum(PropertyInfo property, Func<PropertyInfo, string> propertyNameFactory, object source)
	{
		var editor = new PickerField();

		var rawValues = Enum.GetValues(property.PropertyType.AsNonNullable());
		var values = new ReadableEnumConverter.Entry[rawValues.Length];
		for (var i = 0; i < rawValues.Length; i++)
			values[i] = ReadableEnumConverter.Convert(rawValues.GetValue(i));

		if (values.Length <= 5)
		{
			return CreateSelectionViewForValues(values, property, propertyNameFactory, source);
		}

		editor.ItemsSource = values;
		editor.SetBinding(PickerField.SelectedItemProperty, new Binding(property.Name, source: source, converter: new ReadableEnumConverter()));
		editor.Title = propertyNameFactory(property);
		editor.AllowClear = property.PropertyType.IsNullable();
		return editor;
	}

	private static View CreateSelectionViewForValues(Array values, PropertyInfo property, Func<PropertyInfo, string> propertyNameFactory, object source)
	{
		var shouldUseSingleColumn = values.Length > 3;
		var editor = new SelectionView
		{
			Color = ColorResource.GetColor("Primary", "PrimaryDark"),
			ColumnSpacing = -2,
			RowSpacing = shouldUseSingleColumn ? 5 : -2,
			SelectionType = shouldUseSingleColumn ? InputKit.Shared.SelectionType.RadioButton : InputKit.Shared.SelectionType.Button,
			ColumnNumber = shouldUseSingleColumn ? 1 : values.Length,
			ItemsSource = values
		};

		editor.SetBinding(SelectionView.SelectedItemProperty, new Binding(property.Name, source: source, converter: new ReadableEnumConverter()));

		return new VerticalStackLayout
		{
			Spacing = 6,
			Children = {
				new Label { Text = propertyNameFactory(property) },
				editor
			}
		};
	}

#if DEBUG
	private class MauiDebugDataService : IDebugDataService
	{
		public Task<IFileContent> GetDebugFileAsync()
			=> Task.FromResult<IFileContent>(new DebugFileContent("test-sas.txt", () => FileSystem.OpenAppPackageFileAsync("test-sas.txt")));

		public sealed record DebugFileContent(string NameWithExtension, Func<Task<Stream>> GetStream) : IFileContent
		{
			public string? Id => null;
			public string Name => Path.GetFileNameWithoutExtension(NameWithExtension);

			public bool CanRead => true;
			public bool CanWrite => false;

			public Task WriteAsync(Func<Stream, Task> write, CancellationToken cancellation = default) => throw new NotSupportedException();

			public Task<Stream> ReadAsync(CancellationToken cancellation = default)
				=> GetStream();
		}
	}
#endif

	private class ReadableEnumConverter : IValueConverter
	{
		object? IValueConverter.Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Convert(value);
		public static Entry Convert(object? value)
		{
			var name = value is null ? string.Empty : EnumNameAttribute.GetDisplayName(value.GetType(), value);
			return new Entry(value, name);
		}

		object? IValueConverter.ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
			=> value is Entry entry ? ConvertBack(entry) : null;
		public static object? ConvertBack(Entry entry)
		{
			return entry.Value;
		}

		public sealed class Entry(object? value, string name)
		{
			public object? Value { get; } = value;

			public override string ToString() => name;

			public override bool Equals(object? obj)
				=> obj is Entry entry ? Equals(entry.Value, Value)
				: Equals(obj, Value);

			public override int GetHashCode()
				=> Value?.GetHashCode() ?? 0;
		}
	}
}

public static class MauiExtensions
{
	public static IServiceCollection AddWithRoute<TPage, TViewModel>(this IServiceCollection services)
		where TPage : NavigableElement, IHasShellRoute
		where TViewModel : ViewModelBase
		=> services.AddTransientWithShellRoute<TPage, TViewModel>(TPage.Route);
}
