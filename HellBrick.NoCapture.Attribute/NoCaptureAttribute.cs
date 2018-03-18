using System;

namespace HellBrick
{
	[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method, Inherited = false, AllowMultiple = false )]
	public sealed class NoCaptureAttribute : Attribute
	{
	}
}
