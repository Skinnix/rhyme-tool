using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.RhymeTool.Client.Services;

public readonly record struct SystemRequestResult(SystemRequestResultType Result)
{
	public bool IsOk => Result.HasFlag(SystemRequestResultType.OkFlag);
	public bool IsPending => Result.HasFlag(SystemRequestResultType.PendingFlag);
	public bool IsError => Result.HasFlag(SystemRequestResultType.ErrorFlag);
	public bool IsDenied => Result.HasFlag(SystemRequestResultType.Denied);

	public static implicit operator SystemRequestResult(SystemRequestResultType result) => new(result);
}

public readonly record struct SystemRequestResult<T>(SystemRequestResultType Result, T? Value = default)
{
	public bool IsOk => Result.HasFlag(SystemRequestResultType.OkFlag);
	public bool IsPending => Result.HasFlag(SystemRequestResultType.PendingFlag);
	public bool IsError => Result.HasFlag(SystemRequestResultType.ErrorFlag);
	public bool IsDenied => Result.HasFlag(SystemRequestResultType.Denied);

	public static implicit operator SystemRequestResult<T>(SystemRequestResultType result) => new(result);
}

[Flags]
public enum SystemRequestResultType
{
	None = 0,

	OkFlag = 1,
	PendingFlag = 2,
	NeededFlag = 8,
	UserInteractionFlag = 16,
	PrerequisiteFlag = 32,
	ErrorFlag = 1024,

	NotNeeded = OkFlag,
	Existing = OkFlag | NeededFlag,
	Confirmed = OkFlag | UserInteractionFlag,
	Granted = OkFlag | NeededFlag | UserInteractionFlag,

	Nonexisting = NeededFlag,
	Denied = NeededFlag | UserInteractionFlag,
	PrerequisiteFailed = PrerequisiteFlag,

	Requesting = NeededFlag | PendingFlag | UserInteractionFlag,

	Error = ErrorFlag,
}
