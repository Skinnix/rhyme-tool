using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.ComponentModel;

public class ModifiableObservableCollection<T> : ObservableCollection<T>, IModifiable
{
	public event EventHandler<ModifiedEventArgs>? Modified;

	public ModifiableObservableCollection() { }

	public ModifiableObservableCollection(IEnumerable<T> collection)
		: base(collection)
	{
		RegisterRange(this);
	}

	protected ModifiableObservableCollection(IEnumerable<T> collection, bool registerItems)
		: base(collection)
	{
		if (registerItems)
			RegisterRange(this);
	}

	#region Register/Deregister
	protected virtual T Register(T child)
	{
		if (child is IModifiable modifiable)
		{
			modifiable.Modified -= OnChildModified;
			modifiable.Modified += OnChildModified;
		}
		else
		{
			if (child is INotifyPropertyChanged notifyPropertyChanged)
			{
				notifyPropertyChanged.PropertyChanged -= OnChildPropertyChanged;
				notifyPropertyChanged.PropertyChanged += OnChildPropertyChanged;
			}

			if (child is INotifyCollectionChanged notifyCollectionChanged)
			{
				notifyCollectionChanged.CollectionChanged -= OnChildCollectionChanged;
				notifyCollectionChanged.CollectionChanged += OnChildCollectionChanged;
			}
		}

		return child;
	}

	protected IEnumerable<T> RegisterRange(IEnumerable<T> children)
	{
		foreach (var child in children)
			Register(child);

		return children;
	}

	protected virtual T Deregister(T child)
	{
		if (child is IModifiable modifiable)
		{
			modifiable.Modified -= OnChildModified;
		}

		if (child is INotifyPropertyChanged notifyPropertyChanged)
		{
			notifyPropertyChanged.PropertyChanged -= OnChildPropertyChanged;
		}

		if (child is INotifyCollectionChanged notifyCollectionChanged)
		{
			notifyCollectionChanged.CollectionChanged -= OnChildCollectionChanged;
		}

		return child;
	}

	protected IEnumerable<T> DeregisterRange(IEnumerable<T> children)
	{
		foreach (var child in children)
			Deregister(child);

		return children;
	}

	private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		OnPropertyChanged(e.PropertyName);
	}

	private void OnChildCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		OnModified(new ModifiedEventArgs(this, sender, e));
	}

	private void OnChildModified(object? sender, EventArgs e)
	{
		OnModified(new ModifiedEventArgs(this, sender, e));
	}
	#endregion

	#region Events
	protected void OnPropertyChanged(string? propertyName) => OnPropertyChanged(new PropertyChangedEventArgs(propertyName));

	protected override void OnPropertyChanged(PropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		OnModified(new ModifiedEventArgs(this, this, e));
	}

	protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
	{
		base.OnCollectionChanged(e);
		OnModified(new ModifiedEventArgs(this, this, e));
	}

	protected virtual void OnModified(ModifiedEventArgs args)
	{
		Modified?.Invoke(this, args);
	}
	#endregion

	#region Overrides
	protected override void InsertItem(int index, T item)
	{
		Register(item);

		base.InsertItem(index, item);
	}

	protected override void SetItem(int index, T item)
	{
		if (index < Count)
			Deregister(this[index]);

		Register(item);

		base.SetItem(index, item);
	}

	protected override void RemoveItem(int index)
	{
		var item = this[index];

		base.RemoveItem(index);

		Deregister(item);
	}

	protected override void ClearItems()
	{
		DeregisterRange(this);

		base.ClearItems();
	}
	#endregion
}
