using System.Collections.Immutable;

namespace HellBrick.NoCapture.Analyzer
{
	internal static partial class ImmutableArrayExtensions
	{
		public static T OnlyOrDefault<T>( this ImmutableArray<T> array )
			=> array.Length == 1
			? array[ 0 ]
			: default;
	}
}
