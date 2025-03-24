using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skinnix.Compoetry.Maui.Pages.Document;
using Skinnix.RhymeTool.Client;
using Skinnix.RhymeTool.Client.Services;
using Skinnix.RhymeTool.Client.Services.Files;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using UraniumUI.Pages;

namespace Skinnix.Compoetry.Maui.Pages.Files;

public partial class FileExplorerPage : UraniumContentPage
{
	public FileExplorerPageVM ViewModel => (FileExplorerPageVM)BindingContext;

	public FileExplorerPage()
	{
		BindingContext = App.Services.GetRequiredService<FileExplorerPageVM>();
		ViewModel.Page = this;
		_ = ViewModel.TryLoadRoot();

		ViewModel.PropertyChanged += ViewModel_PropertyChanged;

		InitializeComponent();
	}

	private async void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(FileExplorerPageVM.BreadcrumbString):
				//await breadcrumbScroll.ScrollToAsync(breadcrumbScroll.Content, ScrollToPosition.End, true);
				break;
		}
	}
}

public partial class FileExplorerPageVM(IDocumentFileService fileService, IDocumentService documentService) : ViewModelBase
{
	internal FileExplorerPage? Page { get; set; }

	[ObservableProperty] public partial bool IsLoading { get; private set; }
	[ObservableProperty] public partial bool IsWorkingDirectorySelected { get; private set; }
	[ObservableProperty] public partial bool NeedsPermission { get; private set; }
	[ObservableProperty] public partial bool IsPermissionPending { get; private set; }
	[ObservableProperty] public partial bool IsError { get; private set; }

	[ObservableProperty] public partial ItemList? Root { get; private set; }
	[ObservableProperty] public partial ItemList? CurrentItems { get; private set; }

	[ObservableProperty] public partial FormattedString BreadcrumbString { get; private set; } = new();

	public bool CanSelectFile => fileService.CanSelectFile;
	public bool CanSelectWorkingDirectory => fileService.CanSelectWorkingDirectory;

	public Item? SelectedItem
	{
		get => null;
		set
		{
			if (value is Directory directory)
				_ = LoadDirectory(directory);
			else if (value is File file)
				_ = LoadFile(file);
			else if (value is ParentItem parent)
				_ = GoUp();

			OnPropertyChanged(nameof(SelectedItem));
		}
	}

	public async Task TryLoadRoot(CancellationToken cancellation = default)
	{
		CurrentItems = null;

		IsError = false;
		NeedsPermission = false;
		IsPermissionPending = false;
		IsLoading = true;
		IsWorkingDirectorySelected = false;

		var result = await fileService.TryGetFileListAsync(cancellation);
		IsLoading = false;

		if (result.IsPending)
		{
			IsPermissionPending = true;
			return;
		}

		if (result.IsDenied)
		{
			NeedsPermission = true;
			return;
		}

		if (result.IsError || result.Value is null)
		{
			IsError = true;
			return;
		}

		IsWorkingDirectorySelected = true;
		Root = CurrentItems = new(result.Value.GetItemsAsync, null);
		RefreshBreadcrumbs();

		await CurrentItems.LoadAsync();
	}

	[RelayCommand]
	private Task Refresh()
		=> CurrentItems?.LoadAsync() ?? Task.CompletedTask;

	[RelayCommand]
	private Task LoadItem(Item item) => item switch
	{
		File file => LoadFile(file),
		Directory directory => LoadDirectory(directory),
		ParentItem _ => GoUp(),
		_ => Task.CompletedTask
	};

	private async Task LoadDirectory(Directory directory)
	{
		CurrentItems = directory.Items;
		RefreshBreadcrumbs();

		await CurrentItems.LoadAsync();
	}

	[RelayCommand(CanExecute = nameof(CanGoUp))]
	private Task GoUp()
	{
		if (CurrentItems?.Owner is null)
			return Task.CompletedTask;

		CurrentItems = CurrentItems.Owner.List;
		RefreshBreadcrumbs();

		return CurrentItems.EnsureLoaded();
	}

	private async Task LoadFile(File file)
	{
		var content = await file.GetContentAsync();
		await OpenFileContent(content);
	}

	[RelayCommand]
	private Task SetHistory(ItemList list)
	{
		CurrentItems = list;
		RefreshBreadcrumbs();
		return CurrentItems.EnsureLoaded();
	}

	private void RefreshBreadcrumbs()
	{
		var newBreadcrumbs = new FormattedString();
		if (CurrentItems is null)
		{
			BreadcrumbString = newBreadcrumbs;
			return;
		}

		if (CurrentItems?.Owner is null)
		{
			newBreadcrumbs.Spans.Add(new()
			{
				Text = "\uF07C",
				FontFamily = "FontAwesomeSolid",
				FontAttributes = FontAttributes.Bold,
			});

			//Damit die Textausrichtung gleich bleibt:
			newBreadcrumbs.Spans.Add(new()
			{
				Text = "/",
				TextColor = Colors.Transparent,
			});
			BreadcrumbString = newBreadcrumbs;
			return;
		}

		var history = new List<ItemList>();
		for (var parent = CurrentItems.Owner.List; parent is not null; parent = parent.Owner?.List)
			history.Insert(0, parent);

		var root = history[0];
		var span = new Span()
		{
			Text = "\uF07C",
			FontFamily = "FontAwesomeSolid",
			FontAttributes = FontAttributes.Bold,
		};
		var primaryColor = Application.Current?.Resources["Primary"] as Color;
		if (primaryColor is not null)
		{
			span.TextColor = primaryColor;
		}
		span.GestureRecognizers.Add(new TapGestureRecognizer()
		{
			Command = new RelayCommand(() => SetHistory(root)),
		});
		newBreadcrumbs.Spans.Add(span);

		foreach (var parent in history.Skip(1))
		{
			newBreadcrumbs.Spans.Add(new()
			{
				Text = " / ",
			});

			span = new()
			{
				Text = parent.Owner?.Name ?? string.Empty,
				FontAttributes = FontAttributes.Bold,
				TextDecorations = TextDecorations.Underline,
			};
			if (primaryColor is not null)
			{
				span.TextColor = primaryColor;
			}

			span.GestureRecognizers.Add(new TapGestureRecognizer()
			{
				Command = new RelayCommand(() => SetHistory(parent)),
			});
			newBreadcrumbs.Spans.Add(span);
		}

		newBreadcrumbs.Spans.Add(new()
		{
			Text = " / ",
		});

		span = new()
		{
			Text = CurrentItems.Owner.Name,
			FontAttributes = FontAttributes.Bold,
		};
		newBreadcrumbs.Spans.Add(span);

		BreadcrumbString = newBreadcrumbs;
		return;
	}

	private bool CanGoUp()
		=> CurrentItems?.Owner is not null;

	[RelayCommand(CanExecute = nameof(CanSelectFile))]
	private async Task SelectFile()
	{
		if (!fileService.CanSelectFile)
			return;

		var result = await fileService.TrySelectFileAsync();
		if (!result.IsOk)
		{
			await (Page?.DisplayAlert("Fehler", "Fehler beim Auswählen der Datei", "OK") ?? Task.CompletedTask);
			return;
		}

		if (result.Value is null)
			return;

		await OpenFileContent(result.Value);
	}

	private async Task OpenFileContent(IFileContent content)
	{
		if (!content.CanRead)
		{
			await (Page?.DisplayAlert("Fehler", "Die Datei kann nicht gelesen werden", "OK") ?? Task.CompletedTask);
			return;
		}

		var document = await documentService.LoadFile(content);
		await RendererPage.ShowDocument(document);
	}

	[RelayCommand(CanExecute = nameof(CanSelectWorkingDirectory))]
	private async Task SelectWorkingDirectory()
	{
		var result = await fileService.TrySelectWorkingDirectoryAsync();
		if (result.IsPending)
			return;

		if (!result.IsOk)
		{
			await (Page?.DisplayAlert("Fehler", "Fehler beim Auswählen des Ordners", "OK") ?? Task.CompletedTask);
			return;
		}

		if (result.Value is null)
			return;

		IsWorkingDirectorySelected = true;
		await TryLoadRoot();
	}
}

public abstract partial class Item(ItemList list) : ObservableObject
{
	public ItemList List { get; } = list;

	public abstract string Name { get; }
	public abstract DateTime? LastModified { get; }
	public abstract long? Size { get; }

	public abstract string Icon { get; }

	public string? SizeString
	{
		get
		{
			var size = Size;
			if (size is null)
				return null;

			var suffixes = new[] { "B", "KB", "MB", "GB", "TB" };
			var currentSize = size.Value;
			foreach (var suffix in suffixes)
			{
				if (currentSize < 1024)
					return $"{currentSize} {suffix}";

				currentSize >>= 10;
			}

			return $"{currentSize} PB";
		}
	}

	public string? LastModifiedString
	{
		get
		{
			var lastModified = LastModified;
			if (lastModified is null)
				return null;

			if (lastModified.Value.Date == DateTime.Now.Date)
				return lastModified.Value.ToString("t");

			return lastModified.Value.ToString("d");
		}
	}

	public static Item Create(IFileListItem item, ItemList list) => item switch
	{
		IFileListDirectory directory => new Directory(list, directory),
		IFileListFile file => new File(list, file),
		_ => throw new NotSupportedException("Unbekannter Dateityp")
	};
}

public partial class Directory : Item
{
	public override string Name { get; }
	public override DateTime? LastModified => null;
	public override long? Size => null;
	public override string Icon => "\uF07B";

	public ItemList Items { get; }

	public Directory(ItemList list, IFileListDirectory inner)
		: base(list)
	{
		this.Name = inner.Name;
		this.Items = new(inner.GetItemsAsync, this);
	}
}

public partial class File(ItemList list, IFileListFile inner) : Item(list)
{
	public override string Name => inner.Name;
	public override DateTime? LastModified => inner.LastModified;
	public override long? Size => inner.Size;
	public override string Icon => "\uF15B";

	public Task<IFileContent> GetContentAsync(CancellationToken cancellation = default)
		=> inner.GetContentAsync(cancellation);
}

public partial class ParentItem(ItemList list) : Item(list)
{
	public override string Name => "..";
	public override DateTime? LastModified => null;
	public override long? Size => null;
	public override string Icon => "\uF3BF";
}

public partial class ItemList : IReadOnlyList<Item>, INotifyPropertyChanged, INotifyCollectionChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	private readonly Func<CancellationToken, Task<IReadOnlyList<IFileListItem>>> load;
	private readonly ItemList? parentList;

	private List<Item>? items;

	public Directory? Owner { get; }

	public int Count => items?.Count ?? 0;
	public Item this[int index] => items?[index] ?? throw new InvalidOperationException("Die Liste ist nicht geladen");

	public bool IsLoaded => items is not null;

	public ItemList(Func<CancellationToken, Task<IReadOnlyList<IFileListItem>>> load, Directory? owner)
	{
		this.load = load;
		this.Owner = owner;
	}

	public static ItemList CreateLoading(Func<CancellationToken, Task<IReadOnlyList<IFileListItem>>> load, Directory? owner, CancellationToken cancellation = default)
	{
		var list = new ItemList(load, owner);
		_ = list.LoadAsync(cancellation);
		return list;
	}

	public async Task LoadAsync(CancellationToken cancellation = default)
	{
		var newItems = await load(cancellation);
		var countBefore = Count;
		var itemEnumeration = newItems.Select(i => Item.Create(i, this));
		if (parentList is not null)
			itemEnumeration = itemEnumeration.Prepend(new ParentItem(parentList));
		items = new(itemEnumeration);

		if (countBefore != Count)
			PropertyChanged?.Invoke(this, new(nameof(Count)));

		CollectionChanged?.Invoke(this, new(NotifyCollectionChangedAction.Reset));
	}

	public Task EnsureLoaded(CancellationToken cancellation = default)
		=> IsLoaded ? Task.CompletedTask : LoadAsync(cancellation);

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	public IEnumerator<Item> GetEnumerator() => (items ?? []).GetEnumerator();
}
