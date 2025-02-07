using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.ComponentModel;

namespace Skinnix.RhymeTool.Client.Native;

public interface INativeControlService : INotifyPropertyChanged
{
	bool SupportsNativeControls { get; }

	INativeMenuCollection Menus { get; }
}
