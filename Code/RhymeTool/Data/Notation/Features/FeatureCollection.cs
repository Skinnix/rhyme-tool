using System.Collections;
using Skinnix.RhymeTool.ComponentModel;
using Skinnix.RhymeTool.Data.Editing;

namespace Skinnix.RhymeTool.Data.Notation.Features;

public class FeatureCollection : IReadOnlyCollection<IDocumentFeature>, IModifiable
{
	public event EventHandler<ModifiedEventArgs>? Modified;

	private List<IDocumentFeature> features;

	public SheetDocument Document { get; }

	public int Count => features.Count;

	public FeatureCollection(SheetDocument document)
	{
		Document = document;
		features = new();
	}

	public FeatureCollection(SheetDocument document, IEnumerable<IDocumentFeature> features)
	{
		Document = document;
		this.features = new(features);
	}

	public void Add(IDocumentFeature feature)
	{
		features.Add(feature);
		RaiseModified(new(this));
	}

	private void RaiseModified(ModifiedEventArgs args)
		=> Modified?.Invoke(this, args);

	public bool Remove(IDocumentFeature feature)
	{
		if (!features.Remove(feature))
			return false;

		RaiseModified(new(this));
		return true;
	}

	public IEnumerator<IDocumentFeature> GetEnumerator() => features.GetEnumerator();
	IEnumerator IEnumerable.GetEnumerator() => features.GetEnumerator();

	public Stored Store() => new(this);

	public readonly record struct Stored : IStored<FeatureCollection, SheetDocument>
	{
		private readonly IDocumentFeature.Stored[]? features;

		public Stored(FeatureCollection collection)
		{
			if (collection.Count == 0)
			{
				features = null;
			}
			else
			{
				features = new IDocumentFeature.Stored[collection.Count];
				foreach ((var i, var feature) in collection.Index())
					features[i] = feature.Store();

				features = ArrayCache.Cache(features);
			}
		}

		private Stored(IDocumentFeature.Stored[]? features)
		{
			this.features = features;
		}

		public FeatureCollection Restore(SheetDocument document)
			=> new(document, features?.Select(f => f.Restore(document)) ?? []);

		internal void Apply(FeatureCollection collection)
		{
			var newFeatures = new List<IDocumentFeature>(features?.Length ?? 0);
			if (features is not null)
				foreach (var feature in features)
					newFeatures.Add(feature.Restore(collection.Document));

			collection.features.Clear();
			collection.features = newFeatures;

			collection.RaiseModified(new(collection));
		}

		/*public Stored OptimizeWith(Stored other)
		{
			if (Equals(other))
				return other;

			var newFeatures = new IDocumentFeature.Stored[features?.Length ?? 0];
			var isEqual = features?.Length == other.features?.Length;
			if (features is not null)
			{

			}
			for (var i = 0; i < features.Length; i++)
			{
				if (i >= other.features.Length)
				{
					newFeatures[i] = features[i];
					isEqual = false;
					continue;
				}

				newFeatures[i] = features[i].OptimizeWith(other.features[i], out var featureEqual);
				if (!featureEqual)
				{
					newFeatures[i] = newFeatures[i].OptimizeWith(other.features);
					isEqual = false;
				}
			}

			if (isEqual)
				return other;

			return new(newFeatures);
		}*/
	}
}
