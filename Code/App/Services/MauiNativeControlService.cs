//using System;
//using System.Collections;
//using System.Collections.Generic;
//using System.Collections.ObjectModel;
//using System.Collections.Specialized;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using System.Windows.Input;
//using Microsoft.AspNetCore.Components.WebView.Maui;
//using Skinnix.RhymeTool.Client.Native;
//using Skinnix.RhymeTool.ComponentModel;

//namespace Skinnix.RhymeTool.MauiBlazor.Services;

///*public abstract class TranslatingCollection<T, TSource> : ObservableCollection<T>
//{
//	private readonly ObservableCollection<TSource> source;

//	protected TranslatingCollection(ObservableCollection<TSource> source)
//	{
//		this.source = source;

//		source.CollectionChanged += OnSourceCollectionChanged;

//		RefreshAll();
//	}

//	private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
//	{
//		switch (e.Action)
//		{
//			case NotifyCollectionChangedAction.Add:
//				foreach (TSource item in e.NewItems!)
//					InsertItem(e.NewStartingIndex, Translate(item));
//				break;
//			case NotifyCollectionChangedAction.Remove:
//				var start = e.OldStartingIndex;
//				var count = e.OldItems?.Count ?? 1;
//				for (var i = 0; i < count; i++)
//					RemoveItem(start);

//				break;
//			case NotifyCollectionChangedAction.Replace:

//				RemoveItem(FindIndex((TSource)e.OldItems![0]));
//				InsertItem(e.NewStartingIndex, Translate((TSource)e.NewItems![0]));
//				break;
//			case NotifyCollectionChangedAction.Move:
//				MoveItem(FindIndex((TSource)e.OldItems![0]), e.NewStartingIndex);
//				break;
//			case NotifyCollectionChangedAction.Reset:
//				RefreshAll();
//				break;
//		}
//	}
//}*/

//public interface IMauiUiService : INativeControlService
//{
//	Page MainPage { get; set; }
//	BlazorWebView WebView { get; set; }
//}

//internal class MauiUiService : DeepObservableBase, IMauiUiService
//{
//	private INativeMenuBar? menuBar;

//	public bool SupportsNativeControls => true;

//	public INativeMenuBar? MenuBar
//	{
//		get => menuBar;
//		set => Set(ref menuBar, value);
//	}

//	private Page? mainPage;
//	private BlazorWebView? webView;

//	public Page MainPage
//	{
//		get => mainPage ?? throw new InvalidOperationException("Die Benutzeroberfläche ist noch nicht initialisiert");
//		set => mainPage = value;
//	}

//	public BlazorWebView WebView
//	{
//		get => webView ?? throw new InvalidOperationException("Die Benutzeroberfläche ist noch nicht initialisiert");
//		set => webView = value;
//	}

//	public INativeMenuBar CreateMenuBar()
//		=> MenuBar = new NativeMenuBar();

//	private class NativeMenuBar : DeepObservableBase, INativeMenuBar
//	{
//		public INativeMenuBar.ICollection Items { get; }

//		public NativeMenuBar()
//		{
//			Items = new ItemList();
//		}
//	}

//	private class ItemList : INativeMenuBar.ICollection
//	{
//		public ObservableCollection<MenuBarItem> Collection { get; }

//		public int Count => Collection.Count;

//		public INativeMenuBar.IMenu this[int index] => (INativeMenuBar.IMenu)Collection[index];

//		public INativeMenuBar.IMenu AddMenu(string text)
//		{
//			var result = new Menu()
//			{
//				Text = text
//			};
//			Collection.Add(result);
//			return result;
//		}

//		public bool Remove(INativeMenuBar.IMenu item)
//		{
//			if (item is not Menu menu)
//				return false;
//			return Collection.Remove(menu);
//		}

//		public void Clear() => Collection.Clear();

//		public IEnumerator<INativeMenuBar.IMenu> GetEnumerator() => Collection.OfType<INativeMenuBar.IMenu>().GetEnumerator();
//		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//	}

//	private class Collection(IList<IMenuElement> owner) : INativeMenuBar.IItem.ICollection
//	{
//		public int Count => owner.Count;
//		public INativeMenuBar.IItem this[int index] => (INativeMenuBar.IItem)owner[index];

//		public INativeMenuBar.IClickable AddButton(string text, ICommand? command = null)
//		{
//			var result = new FlyoutItem()
//			{
//				Text = text,
//				Command = command
//			};
//			owner.Add(result);
//			return result;
//		}

//		public INativeMenuBar.ISubMenu AddSubMenu(string text)
//		{
//			var result = new SubMenuItem()
//			{
//				Text = text
//			};
//			owner.Add(result);
//			return result;
//		}

//		public bool Remove(INativeMenuBar.IItem item)
//		{
//			if (item is not MenuItem menuItem)
//				return false;
//			return owner.Remove(menuItem);
//		}

//		public void Clear() => owner.Clear();

//		public IEnumerator<INativeMenuBar.IItem> GetEnumerator() => owner.OfType<INativeMenuBar.IItem>().GetEnumerator();
//		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//	}

//	private class Menu : MenuBarItem, INativeMenuBar.IMenu
//	{
//		public INativeMenuBar.IItem.ICollection Items { get; }

//		public Menu()
//		{
//			Items = new Collection(this);
//		}
//	}

//	private class FlyoutItem : MenuFlyoutItem, INativeMenuBar.IClickable;

//	private class SubMenuItem : MenuFlyoutSubItem, INativeMenuBar.ISubMenu
//	{
//		public INativeMenuBar.IItem.ICollection Items { get; }

//		public SubMenuItem()
//		{
//			Items = new Collection(this);
//		}
//	}
//}
