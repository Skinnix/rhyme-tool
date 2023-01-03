using System.Collections;

namespace Skinnix.RhymeTool.Client.Data.Structure
{
	public class RhymeNodeCollection : IList<RhymeNode>
	{
		public event EventHandler<RhymeNodeChangedEventArgs>? NodeChanged;

		private readonly List<RhymeNode> nodes = new();

		#region List Members
		public bool IsReadOnly => false;
		public int Count => nodes.Count;

		public RhymeNode this[int index]
		{
			get => nodes[index];
			set
			{
				Deregister(nodes[index]);
				nodes[index] = value;
				Register(value);
			}
		}

		public bool Contains(RhymeNode item) => nodes.Contains(item);
		public int IndexOf(RhymeNode item) => nodes.IndexOf(item);
		public void CopyTo(RhymeNode[] array, int arrayIndex) => nodes.CopyTo(array, arrayIndex);

		public void Add(RhymeNode item) => nodes.Add(item);
		public void Insert(int index, RhymeNode item) => nodes.Insert(index, item);
		public bool Remove(RhymeNode item) => nodes.Remove(item);
		public void RemoveAt(int index) => nodes.RemoveAt(index);
		public void Clear() => nodes.Clear();

		public IEnumerator<RhymeNode> GetEnumerator() => nodes.GetEnumerator();
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
		#endregion

		private void Register(RhymeNode node)
		{
			node.Changed -= OnNodeChanged;
			node.Changed += OnNodeChanged;
		}

		private void Deregister(RhymeNode node)
		{
			node.Changed -= OnNodeChanged;
		}

		private void OnNodeChanged(object? sender, RhymeNodeChangedEventArgs e)
			=> InvokeNodeChanged(e);

		private void InvokeNodeChanged(RhymeNodeChangedEventArgs e)
			=> NodeChanged?.Invoke(this, e);
	}
}
