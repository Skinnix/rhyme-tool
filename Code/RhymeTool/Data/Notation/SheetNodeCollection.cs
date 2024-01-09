//using System.Collections;
//using System.Collections.ObjectModel;

//namespace Skinnix.RhymeTool.Data.Notation;

//public class SheetNodeCollection : IList<SheetNode>
//{
//    public event EventHandler<EventArgs>? CollectionChanged;
//    public event EventHandler<SheetNodeChangedEventArgs>? NodeChanged;

//    private readonly ObservableCollection<SheetNode> nodes = new();

//    public SheetNodeCollection()
//    {
//        nodes.CollectionChanged += (_, e) => CollectionChanged?.Invoke(this, e);
//    }

//    #region List Members
//    public bool IsReadOnly => false;
//    public int Count => nodes.Count;

//    public SheetNode this[int index]
//    {
//        get => nodes[index];
//        set
//        {
//            Deregister(nodes[index]);
//            nodes[index] = value;
//            Register(value);
//        }
//    }

//    public bool Contains(SheetNode item) => nodes.Contains(item);
//    public int IndexOf(SheetNode item) => nodes.IndexOf(item);
//    public void CopyTo(SheetNode[] array, int arrayIndex) => nodes.CopyTo(array, arrayIndex);

//    public void Add(SheetNode item) => nodes.Add(item);
//    public void Insert(int index, SheetNode item)
//    {
//        if (index == Count)
//            nodes.Add(item);
//        else
//            nodes.Insert(index, item);
//    }
//    public bool Remove(SheetNode item) => nodes.Remove(item);
//    public void RemoveAt(int index) => nodes.RemoveAt(index);
//    public void Clear() => nodes.Clear();

//    public IEnumerator<SheetNode> GetEnumerator() => nodes.GetEnumerator();
//    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//    #endregion

//    #region Changes
//    private void Register(SheetNode node)
//    {
//        node.Changed -= OnNodeChanged;
//        node.Changed += OnNodeChanged;
//    }

//    private void Deregister(SheetNode node)
//    {
//        node.Changed -= OnNodeChanged;
//    }

//    private void OnNodeChanged(object? sender, SheetNodeChangedEventArgs e)
//        => InvokeNodeChanged(e);

//    private void InvokeNodeChanged(SheetNodeChangedEventArgs e)
//        => NodeChanged?.Invoke(this, e);
//    #endregion

//    public SheetNode? Find(Guid id)
//    {
//        foreach (var node in nodes)
//            if (node.Id == id)
//                return node;

//        return null;
//    }

//    public SheetNode? FindRecursive(Guid id)
//    {
//        foreach (var node in nodes)
//            if (node.Id == id)
//                return node;

//        foreach (var node in nodes.OfType<CompositeSheetNode>())
//        {
//            var found = node.Nodes.FindRecursive(id);
//            if (found != null)
//                return found;
//        }

//        return null;
//    }

//    public SheetNode? RemoveRecursive(Guid id)
//    {
//        foreach (var node in nodes)
//        {
//            if (node.Id == id)
//            {
//                Remove(node);
//                return node;
//            }
//        }

//        foreach (var node in nodes.OfType<CompositeSheetNode>())
//        {
//            var removed = node.Nodes.RemoveRecursive(id);
//            if (removed != null)
//                return removed;
//        }

//        return null;
//    }
//}
