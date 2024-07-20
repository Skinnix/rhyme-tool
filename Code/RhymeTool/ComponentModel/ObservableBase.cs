using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Skinnix.RhymeTool.ComponentModel;

public abstract class ObservableBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;

	#region Set
	protected T Set<T>(ref T field, T value, bool force = true, [CallerMemberName] string? propertyName = null)
	{
		if (!force && Equals(field, value))
			return value;

		field = value;
		RaisePropertyChanged(propertyName);
		return value;
	}
	#endregion

	protected void RaisePropertyChanged(string? propertyName) => RaisePropertyChanged(new PropertyChangedEventArgs(propertyName));

	protected virtual void RaisePropertyChanged(PropertyChangedEventArgs e)
	{
		PropertyChanged?.Invoke(this, e);
	}
}
