using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data.Notation;
using Skinnix.RhymeTool.Data.Notation.Features;

namespace Skinnix.RhymeTool.Data;

public interface IStoredBase
{
	
}

public interface IStored<out T> : IStoredBase
{
	T Restore();
}

public interface IStored<out T, in TParams> : IStoredBase
{
	T Restore(TParams parameters);
}

public interface ISelfStored<TSelf, out T> : IStored<T>
	where TSelf : ISelfStored<TSelf, T>
{
	//bool Equals(TSelf other);
}

public interface ISelfStored<TSelf, out T, in TParams> : IStored<T, TParams>
	where TSelf : ISelfStored<TSelf, T, TParams>
{
	//bool Equals(TSelf other);
}
