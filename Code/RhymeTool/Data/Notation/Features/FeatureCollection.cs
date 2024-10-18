using System.Collections;
using Skinnix.RhymeTool.ComponentModel;

namespace Skinnix.RhymeTool.Data.Notation.Features;

public class FeatureCollection : IReadOnlyCollection<IDocumentFeature>, IModifiable
{
	public event EventHandler<ModifiedEventArgs>? Modified;

	private readonly List<IDocumentFeature> features;

	public int Count => features.Count;

	public FeatureCollection()
	{
		features = new();
	}

	public FeatureCollection(IEnumerable<IDocumentFeature> features)
	{
		this.features = new(features);
	}

	public void Add(IDocumentFeature feature)
	{
		features.Add(feature);
		Modified?.Invoke(this, new(this));
	}

	public bool Remove(IDocumentFeature feature)
	{
		if (!features.Remove(feature))
			return false;

		Modified?.Invoke(this, new(this));
		return true;
	}

	public IEnumerator<IDocumentFeature> GetEnumerator() => features.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => features.GetEnumerator();
}
