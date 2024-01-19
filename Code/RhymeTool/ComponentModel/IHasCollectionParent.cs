using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.ComponentModel;

public interface IHasCollectionParent<TParent>
	where TParent : notnull
{
	TParent? Parent { get; }
	void SetParent(TParent? parent);
}
