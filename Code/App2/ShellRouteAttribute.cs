using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skinnix.Compoetry.Maui;

[AttributeUsage(AttributeTargets.Class)]
public class ShellRouteAttribute : Attribute
{
	public string Route { get; }

	public ShellRouteAttribute(string route)
	{
		Route = route;
	}
}
