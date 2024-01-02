using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Skinnix.RhymeTool.Data;

namespace Skinnix.RhymeTool;

public static class EnumExtensions
{
	public static string GetDisplayName<TEnum>(this TEnum value)
		where TEnum : struct, Enum
	{
		MemberInfo[] memberInfo = typeof(TEnum).GetMember(value.ToString());
		if (memberInfo != null && memberInfo.Length > 0)
		{
			var member = memberInfo[0];

			return member.GetCustomAttribute<EnumNameAttribute>(false)?.Name
				?? member.GetCustomAttribute<DisplayAttribute>(false)?.Name
				?? value.ToString();
		}

		return value.ToString();
	}

	public static int TryRead<T>(ReadOnlySpan<char> s, out T value)
		where T : struct, Enum
		=> EnumNameAttribute.TryRead(s, out value);
}
