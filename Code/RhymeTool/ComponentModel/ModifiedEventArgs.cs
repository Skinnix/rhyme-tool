using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.ComponentModel;

public class ModifiedEventArgs : EventArgs
{
	public IModifiable Source { get; }
	public object? OriginalSender { get; }
	public EventArgs? OriginalEventArgs { get; }

	public ModifiedEventArgs(IModifiable source)
	{
		Source = source;
	}

	public ModifiedEventArgs(IModifiable source, object? originalSender, EventArgs? originalEventArgs)
	{
		Source = source;
		OriginalSender = originalSender;
		OriginalEventArgs = originalEventArgs;
	}
}
