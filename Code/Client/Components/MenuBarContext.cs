using System.Collections.Concurrent;
using System.Xml.Linq;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Skinnix.RhymeTool.Client.Components
{
	public class MenuBarContext
	{
		private static readonly RenderFragment emptyFragment = _ => { };

		private readonly ConcurrentDictionary<string, Group> groups = new();

		public RenderFragment GetContent(string group = "")
			=> groups.TryGetValue(group, out var groupInstance)
			? groupInstance.Content
			: emptyFragment;

		public void AddElement(MenuBarElement element, string group = "")
		{
			var groupInstance = groups.GetOrAdd(group, new Group());
			groupInstance.RegisterElement(element);
		}

		public void Refresh(string group = "")
		{
			var groupInstance = groups.GetOrAdd(group, new Group());
			groupInstance.Refresh();
		}

		public void RegisterRefresh(object target, Action refresh, string group = "")
		{
			var groupInstance = groups.GetOrAdd(group, new Group());
			groupInstance.RegisterRefresh(target, refresh);
		}

		private class Group
		{
			private readonly LinkedList<WeakReference<MenuBarElement>> elements = new();
			private readonly LinkedList<KeyValuePair<WeakReference, Action>> refreshes = new();

			public RenderFragment Content => Draw;

			public void RegisterElement(MenuBarElement element)
			{
				if (CleanupElements(element))
					return;

				elements.AddLast(new WeakReference<MenuBarElement>(element));
				CleanupRefreshes(invoke: true);
			}

			public void Refresh()
			{
				CleanupRefreshes(invoke: true);
			}

			public void RegisterRefresh(object target, Action refresh)
			{
				if (CleanupRefreshes(searchTarget: target))
					return;

				refreshes.AddLast(new KeyValuePair<WeakReference, Action>(new(target), refresh));
			}

			private void Draw(RenderTreeBuilder builder)
			{
				foreach (var element in elements)
					if (element.TryGetTarget(out var target))
						target.ChildContent?.Invoke(builder);
			}

			private bool CleanupElements(MenuBarElement? searchTarget = null)
			{
				var found = false;
				for (var current = elements.First; current != null; current = current?.Next)
				{
					if (!current.Value.TryGetTarget(out var target))
					{
						var next = current.Next;
						elements.Remove(current);
						current = next;
						continue;
					}

					if (target == searchTarget)
						found = true;
				}

				return found;
			}

			private bool CleanupRefreshes(bool invoke = false, object? searchTarget = null)
			{
				var found = false;
				for (var current = refreshes.First; current != null; current = current?.Next)
				{
					var target = current.Value.Key.Target;
					if (target == null)
					{
						var next = current.Next;
						refreshes.Remove(current);
						current = next;
						continue;
					}

					if (target == searchTarget)
						found = true;

					if (invoke)
						current.Value.Value();
				}

				return found;
			}
		}
	}
}