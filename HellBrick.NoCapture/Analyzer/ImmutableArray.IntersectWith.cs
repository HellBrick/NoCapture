using System;
using System.Collections.Immutable;

namespace HellBrick.NoCapture.Analyzer
{
	internal static partial class ImmutableArrayExtensions
	{
		public static ImmutableArray<T> IntersectWith<T>( this ImmutableArray<T> array, ImmutableArray<T> otherArray )
		{
			return
				array.IsEmpty || otherArray.IsEmpty
				? ImmutableArray<T>.Empty
				: IntersectSlow();

			ImmutableArray<T> IntersectSlow()
			{
				ImmutableArray<T>.Builder builder = ImmutableArray.CreateBuilder<T>( Math.Min( array.Length, otherArray.Length ) );
				foreach ( T item in array )
				{
					if ( otherArray.Contains( item ) )
						builder.Add( item );
				}

				return
					builder.Count == builder.Capacity
					? builder.MoveToImmutable()
					: builder.ToImmutable();
			}
		}
	}
}
