using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.ComponentModel;

public abstract class DeepObservableBase : ObservableBase, INotifyPropertyChanged, IModifiable
{
	public event EventHandler<ModifiedEventArgs>? Modified;

	#region Register/Deregister
	protected T Register<T>(T child)
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

	protected IEnumerable<T> RegisterRange<T>(IEnumerable<T> children)
	{
		foreach (var child in children)
			Register(child);

		return children;
	}

	private T Deregister<T>(T child)
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

	protected IEnumerable<T> DeregisterRange<T>(IEnumerable<T> children)
	{
		foreach (var child in children)
			Deregister(child);

		return children;
	}

	private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		RaisePropertyChanged(e.PropertyName);
	}

	private void OnChildCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RaiseModified(new ModifiedEventArgs(this, sender, e));
	}

	private void OnChildModified(object? sender, EventArgs e)
	{
		RaiseModified(new ModifiedEventArgs(this, sender, e));
	}
	#endregion

	protected override void RaisePropertyChanged(PropertyChangedEventArgs e)
	{
		base.RaisePropertyChanged(e);
		RaiseModified(new ModifiedEventArgs(this, this, e));
	}

	protected virtual void RaiseModified(ModifiedEventArgs args)
	{
		Modified?.Invoke(this, args);
	}
}
