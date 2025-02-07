using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Skinnix.RhymeTool.Client.Native;
using Skinnix.RhymeTool.ComponentModel;

namespace Skinnix.RhymeTool.MauiBlazor.Services;

/*public abstract class TranslatingCollection<T, TSource> : ObservableCollection<T>
{
	private readonly ObservableCollection<TSource> source;

	protected TranslatingCollection(ObservableCollection<TSource> source)
	{
		this.source = source;

		source.CollectionChanged += OnSourceCollectionChanged;

		RefreshAll();
	}

	private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		switch (e.Action)
		{
			case NotifyCollectionChangedAction.Add:
				foreach (TSource item in e.NewItems!)
					InsertItem(e.NewStartingIndex, Translate(item));
				break;
			case NotifyCollectionChangedAction.Remove:
				var start = e.OldStartingIndex;
				var count = e.OldItems?.Count ?? 1;
				for (var i = 0; i < count; i++)
					RemoveItem(start);

				break;
			case NotifyCollectionChangedAction.Replace:

				RemoveItem(FindIndex((TSource)e.OldItems![0]));
				InsertItem(e.NewStartingIndex, Translate((TSource)e.NewItems![0]));
				break;
			case NotifyCollectionChangedAction.Move:
				MoveItem(FindIndex((TSource)e.OldItems![0]), e.NewStartingIndex);
				break;
			case NotifyCollectionChangedAction.Reset:
				RefreshAll();
				break;
		}
	}
}*/

public interface IMauiUiService : INativeControlService
{
	Page MainPage { get; set; }
	BlazorWebView WebView { get; set; }
}

internal class MauiUiService : DeepObservableBase, IMauiUiService
{
	private Page? mainPage;
	private BlazorWebView? webView;

	private INativeMenuCollection? menus;

	public bool SupportsNativeControls => true;

	public Page MainPage
	{
		get => mainPage ?? throw new InvalidOperationException("Die Benutzeroberfläche ist noch nicht initialisiert");
		set => mainPage = value;
	}

	public BlazorWebView WebView
	{
		get => webView ?? throw new InvalidOperationException("Die Benutzeroberfläche ist noch nicht initialisiert");
		set => webView = value;
	}

	public INativeMenuCollection Menus
		=> menus ??= mainPage is not null ? new ItemList(mainPage.MenuBarItems)
		: throw new InvalidOperationException("Die Benutzeroberfläche ist noch nicht initialisiert");

	private class ItemList(IList<MenuBarItem> inner) : INativeMenuCollection
	{
		public int Count => inner.Count;

		public INativeMenuCollection.IMenu this[int index] => (INativeMenuCollection.IMenu)inner[index];

		public INativeMenuCollection.IMenu AddMenu(string text)
		{
			var result = new Menu()
			{
				Text = text
			};
			inner.Add(result);
			return result;
		}

		public bool Remove(INativeMenuCollection.IMenu item)
		{
			if (item is not Menu menu)
				return false;
			return inner.Remove(menu);
		}

		public void Clear() => inner.Clear();

		public IEnumerator<INativeMenuCollection.IMenu> GetEnumerator() => inner.OfType<INativeMenuCollection.IMenu>().GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private class Collection(IList<IMenuElement> owner) : INativeMenuCollection.IItem.ICollection
	{
		public int Count => owner.Count;
		public INativeMenuCollection.IItem this[int index] => (INativeMenuCollection.IItem)owner[index];

		public INativeMenuCollection.IClickable AddButton(string text, ICommand? command = null)
		{
			var result = new FlyoutItem()
			{
				Text = text,
				Command = command
			};
			owner.Add(result);
			return result;
		}

		public INativeMenuCollection.ISubMenu AddSubMenu(string text)
		{
			var result = new SubMenuItem()
			{
				Text = text
			};
			owner.Add(result);
			return result;
		}

		public bool Remove(INativeMenuCollection.IItem item)
		{
			if (item is not MenuItem menuItem)
				return false;
			return owner.Remove(menuItem);
		}

		public void Clear() => owner.Clear();

		public IEnumerator<INativeMenuCollection.IItem> GetEnumerator() => owner.OfType<INativeMenuCollection.IItem>().GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	private class Menu : MenuBarItem, INativeMenuCollection.IMenu
	{
		public INativeMenuCollection.IItem.ICollection Items { get; }

		public Menu()
		{
			Items = new Collection(this);
		}
	}

	private class FlyoutItem : MenuFlyoutItem, INativeMenuCollection.IClickable;

	private class SubMenuItem : MenuFlyoutSubItem, INativeMenuCollection.ISubMenu
	{
		public INativeMenuCollection.IItem.ICollection Items { get; }

		public SubMenuItem()
		{
			Items = new Collection(this);
		}
	}
}
