using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.ComponentModel;

public abstract class DeepObservableCollectionBase : DeepObservableBase, INotifyCollectionChanged
{
	public event NotifyCollectionChangedEventHandler? CollectionChanged;

	protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
	{
		CollectionChanged?.Invoke(this, e);	
		RaiseModified(new ModifiedEventArgs(this, this, e));
	}
}
