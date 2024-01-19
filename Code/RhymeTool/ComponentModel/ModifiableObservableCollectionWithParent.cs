namespace Skinnix.RhymeTool.ComponentModel;

public class ModifiableObservableCollectionWithParent<T, TParent> : ModifiableObservableCollection<T>
	where TParent : notnull
{
	public TParent? Parent { get; }

	public ModifiableObservableCollectionWithParent(TParent parent)
	{
		Parent = parent;
	}

	public ModifiableObservableCollectionWithParent(TParent parent, IEnumerable<T> collection)
		: base(collection, false)
	{
		Parent = parent;

		RegisterRange(this);
	}

	protected override T Register(T child)
	{
		base.Register(child);

		if (child is IHasCollectionParent<TParent> hasCollectionParent)
			hasCollectionParent.SetParent(Parent);

		return child;
	}

	protected override T Deregister(T child)
	{
		if (child is IHasCollectionParent<TParent> hasCollectionParent)
			hasCollectionParent.SetParent(Parent);

		return base.Deregister(child);
	}
}