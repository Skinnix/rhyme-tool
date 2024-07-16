using Microsoft.AspNetCore.Components;
using Skinnix.RhymeTool.Configuration;

namespace Skinnix.RhymeTool.Client.Components.Configuration;

public abstract class PropertyEditorBase : ComponentBase
{
	[Parameter] public IConfigurableProperty? Property { get; set; }
	[Parameter] public string? EditorId { get; set; }
}

public abstract class PropertyEditorBase<TValue> : PropertyEditorBase
{
	protected TValue Value
	{
		get => (TValue)Property!.Value!;
		set => Property!.Value = value;
	}
}
