using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Skinnix.RhymeTool.ComponentModel;

namespace Skinnix.RhymeTool.Client.Native;

public interface INativeMenuCollection : IReadOnlyList<INativeMenuCollection.IMenu>
{
	IMenu AddMenu(string text);
	bool Remove(IMenu item);
	void Clear();

	public interface IItem
	{
		public interface ICollection : IReadOnlyList<IItem>
		{
			IClickable AddButton(string text, ICommand? command = null);
			ISubMenu AddSubMenu(string text);

			bool Remove(IItem item);
			void Clear();
		}
	}

	public interface IMenu : IItem
	{
		ICollection Items { get; }
	}

	public interface IClickable : IItem
	{
		string Text { get; set; }
		ICommand? Command { get; set; }
	}

	public interface ISubMenu : IClickable, IMenu;
}
