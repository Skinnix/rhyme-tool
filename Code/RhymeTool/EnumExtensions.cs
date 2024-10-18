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
	public static IEnumerable<string> GetFlagsDisplayName<TEnum>(this TEnum value)
		where TEnum : struct, Enum
		=> value.GetUniqueFlags().Select(GetDisplayName);

	public static string GetDisplayName<TEnum>(this TEnum value)
		where TEnum : struct, Enum
	{
		MemberInfo[] memberInfo = typeof(TEnum).GetMember(value.ToString());
		if (memberInfo != null && memberInfo.Length > 0)
		{
			var member = memberInfo[0];

			return member.GetCustomAttribute<EnumNameAttribute>(false)?.PreferredName
				?? member.GetCustomAttribute<DisplayAttribute>(false)?.Name
				?? value.ToString();
		}

		return value.ToString();
	}

	public static int TryRead<T>(ReadOnlySpan<char> s, out T value)
		where T : struct, Enum
		=> EnumNameAttribute.TryRead(s, out value);

	public static IEnumerable<TEnum> GetUniqueFlags<TEnum>(this TEnum flags)
		where TEnum : struct, Enum
	{
		ulong flag = 1;
		foreach (var value in Enum.GetValues(flags.GetType()).Cast<Enum>())
		{
			ulong bits = Convert.ToUInt64(value);
			while (flag < bits)
			{
				flag <<= 1;
			}

			if (flag == bits && flags.HasFlag(value))
			{
				yield return (TEnum)value;
			}
		}
	}
}
