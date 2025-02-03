using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Data.Notation.Features;

public interface IDocumentFeature
{
	bool Overrides(IDocumentFeature other);

	Stored Store();

	public interface Stored : IStored<IDocumentFeature, SheetDocument>
	{
		Stored OptimizeWith(Stored feature, out bool isEqual);
		Stored OptimizeWith(IReadOnlyCollection<Stored> others);
	}
}
