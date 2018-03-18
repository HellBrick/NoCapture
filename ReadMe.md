# What is this?

A Roslyn analyzer that allows you to enforce a no-capture policy for passing lambdas to your methods by decorating them with a special attribute.

# How to use it?

1. Install the [HellBrick.NoCapture](https://www.nuget.org/packages/HellBrick.NoCapture) package from NuGet

1. Decorate the methods or the parameters that you don't want the callers to pass capturing lambdas to with the `[NoCapture]` attribute:

```csharp
[NoCapture]
public static IEnumerable<TOut> Select<TIn, TOut, TArg>( this IEnumerable<TIn> sequence, TArg argument, Func<TIn, TArg, TOut> selector )
{
	foreach ( TIn item in sequence )
		yield return selector( item, argument );
}

public static IEnumerable<TOut> Select<TIn, TOut>( this IEnumerable<TIn> sequence, [NoCapture] Func<TIn, TOut> selector )
		=> sequence.Select( selector, ( item, innerSelector ) => innerSelector( item ) );

// ...

public class Caller
{
	private readonly int[] _numbers = { 64, 128, 256 };
	private readonly int _field = 42;

	public void CallSite( int argument )
	{
		// Error: Select( selector ) requires a non-capturing lambda. Captured variables: this.
		_numbers.Select( x => x + _field );

		// Error: Select( selector ) requires a non-capturing lambda. Captured variables: this, argument.
		_numbers.Select( "", ( x, _ ) => x + _field + argument );
	}
}
```

# Limitations

At the moment, only the lambdas defined inline in the argument list are checked for the violations of the `[NoCapture]` attribute.
For instance, the following call site, if added to the previous example, will trick the analyzer into a false negative:

```csharp
public void AnotherCallSite()
{
	// No error yet.
	Func<int, int> lambda = x => x + _field;
	_numbers.Select( lambda );
}
```
