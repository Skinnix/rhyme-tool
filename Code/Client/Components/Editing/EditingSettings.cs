using System.ComponentModel;
using System.Runtime.CompilerServices;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Configuration;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.Display;

namespace Skinnix.RhymeTool.Client.Components.Rendering;

public class EditingSettings : DocumentSettings
{
	[Configurable(Name = "Leere Akkordzeilen anzeigen")]
	public bool ShowEmptyChordLines
	{
		get => Formatter.ShowEmptyAttachmentLines;
		set => Formatter = Formatter with
		{
			ShowEmptyAttachmentLines = value
		};
	}

	//[Configurable(Name = "Akkordzeilen erweitern")]
	//public bool ExtendChordLines
	//{
	//	get => Formatter.ExtendAttachmentLines;
	//	set => Formatter = Formatter with
	//	{
	//		ExtendAttachmentLines = value
	//	};
	//}
}
