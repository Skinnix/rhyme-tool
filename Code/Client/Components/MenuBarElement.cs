using Microsoft.AspNetCore.Components;

namespace Skinnix.RhymeTool.Client.Components
{
	public class MenuBarElement : ComponentBase, IDisposable
	{
		[CascadingParameter] public MenuBarContext? Context { get; set; }

		[Parameter] public string Group { get; set; } = string.Empty;
		[Parameter] public RenderFragment? ChildContent { get; set; }

		protected override void OnParametersSet()
		{
			base.OnParametersSet();

			if (Context != null)
				Context.AddElement(this, Group);
		}

		public virtual void Dispose()
		{
			ChildContent = null;
			Context?.Refresh(Group);
		}
	}
}
