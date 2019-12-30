using System;
using System.Linq;
using FluentAssertions;
using HellBrick.Diagnostics.Assertions;
using Xunit;

namespace HellBrick.NoCapture.Analyzer.Test
{
	internal static partial class NoCaptureAnalyzerVerifierExtensions
	{
		public static void ShouldHaveDiagnostic<TSource, TSourceCollectionFactory>
		(
			this AnalyzerVerifier<NoCaptureAnalyzer, TSource, TSourceCollectionFactory> verifier,
			string methodName,
			string parameterName,
			params string[] capturedVariables
		)
			where TSourceCollectionFactory : struct, ISourceCollectionFactory<TSource>
			=> verifier
			.ShouldHaveDiagnostics
			(
				diags
				=> diags.Should().HaveCount( 1 )
				.And.Subject.First().GetMessage().Should().Be( $"{methodName}( {parameterName} ) requires a non-capturing lambda. Captured variables: {String.Join( ",", capturedVariables )}." )
			);
	}

	public class NoCaptureTest
	{
		private readonly AnalyzerVerifier<NoCaptureAnalyzer> _verifier = AnalyzerVerifier.UseAnalyzer<NoCaptureAnalyzer>();

		[Fact]
		public void CapturingInvocationOfMethodWithoutNoCaptureIsIgnored()
			=> _verifier
			.Source
			(
@"
using System;
class C
{
	private readonly int _field = 42;

	void CallSite() => Invoke( () => _field );
	T Invoke<T>( Func<T> func ) => func();
}
"
			)
			.ShouldHaveNoDiagnostics();

		[Fact]
		public void CapturelessInvocationOfNoCaptureMethodIsIgnored()
			=> _verifier
			.Source
			(
@"
using System;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	void CallSite() => Invoke( () => 42 );
	[NoCapture] T Invoke<T>( Func<T> func ) => func();
}
"
			)
			.ShouldHaveNoDiagnostics();

		[Fact]
		public void CapturingInvocationOfNoCaptureMethodIsReported()
			=> _verifier
			.Source
			(
@"
using System;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	private readonly int _field = 42;

	void CallSite() => Invoke( () => _field );
	[NoCapture] T Invoke<T>( Func<T> func ) => func();
}
"
			)
			.ShouldHaveDiagnostic( "Invoke", "func", "this" );

		[Fact]
		public void CorrectParameterNameIsReportedIfNamedArgumentsHaveIncorrectOrder()
			=> _verifier
			.Source
			(
@"
using System;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	private readonly int _field = 42;

	void CallSite() => Invoke( func: s => s + _field, seed: 64 );
	[NoCapture] T Invoke<T>( int seed, Func<int, T> func ) => func( seed );
}
"
			)
			.ShouldHaveDiagnostic( "Invoke", "func", "this" );

		[Fact]
		public void SlightlyIncorrectCapturingInvocationOfNoCaptureMethodIsReported()
			=> _verifier
			.Source
			(
@"
using System;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	private readonly int _field = 42;

	// Lambda doesn't follow the correct signature, but the invoked method still can be determined as non-capturing.
	void CallSite() => Invoke( 64, () => _field );
	[NoCapture] T Invoke<T>( seed, Func<int, T> func ) => func( seed );
}
"
			)
			.ShouldHaveDiagnostic( "Invoke", "func", "this" );

		[Fact]
		public void CapturingArgumentPassedAsNoCaptureParameterIsReported()
			=> _verifier
			.Source
			(
@"
using System;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	private readonly int _field = 42;

	void CallSite() => Invoke( () => _field, () => _field + 2 );
	T Invoke<T>( Func<T> normalFunc, [NoCapture] Func<T> noCaptureFunc ) => normalFunc() + noCaptureFunc();
}
"
			)
			.ShouldHaveDiagnostic( "Invoke", "noCaptureFunc", "this" );

		[Fact]
		public void CapturingArgumentPassedAsNoCaptureParameterInsideLocalMethodIsReported()
			=> _verifier
			.Source
			(
@"
using System;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	private readonly int? _field = 64;

	int CallSite( int outerArg )
	{
		return
			field is int notNullField
			? notNullField.Select( x => x + outerArg )
			: 42;
	}
}

static class X
{
	public static T Select<T>( this T value, [NoCapture] Func<T, T> selector ) => selector( value );
}
"
			)
			.ShouldHaveDiagnostic( "Select", "selector", "outerArg" );

		[Fact]
		public void NoFalsePositiveForNonCapturingLambdaInsideLocalMethod()
			=> _verifier
			.Source
			(
@"
using System;
using System.Collections.Generic;
using System.Linq;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	private readonly int[] _numbers = { 42, 64, 128 };

	public IEnumerable<int> EnumerateNumbers()
	{
		return
			_numbers == null
			? Enumerable.Empty<int>()
			: EnumerateWithoutValidation();

		IEnumerable<int> EnumerateWithoutValidation()
			=> _numbers
			.WhereNoCapture( x => x == 42 );
	}
}

static class X
{
		public static IEnumerable<T> WhereNoCapture<T>( this IEnumerable<T> sequence, [NoCapture] Func<T, bool> predicate )
			=> sequence.Where( predicate );
}
"
			)
			.ShouldHaveNoDiagnostics();

		[Fact]
		public void CapturingArgumentPassedAsParamsItemWithoutNoCaptureIsIgnored()
			=> _verifier
			.Source
			(
@"
using System;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	private readonly int _field = 42;

	void CallSite() => Invoke( 64, x => x + 2, x => _field + x );
	T Invoke<T>( T initialValue, params Func<T, T>[] transforms ) => initialValue;
}
"
			)
			.ShouldHaveNoDiagnostics();

		[Fact]
		public void CapturingArgumentPassedAsNoCaptureParamsItemIsReported()
			=> _verifier
			.Source
			(
@"
using System;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	private readonly int _field = 42;

	void CallSite() => Invoke( 64, x => x + 2, x => _field + x );
	T Invoke<T>( T initialValue, [NoCapture] params Func<T, T>[] transforms ) => initialValue;
}
"
			)
			.ShouldHaveDiagnostic( "Invoke", "transforms", "this" );

		[Fact]
		public void NonCapturingArgumentPassedAsNoCaptureButContainingNestedCapturingLambdaIsIgnored()
			=> _verifier
			.Source
			(
@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	public void CallSite() => Invoke( ( list, v ) => list.Select( x => x + v ) );
	public void Invoke( [NoCapture] Expression<Func<List<int>, int, IEnumerable<int>>> f ) { }
}
"
			)
			.ShouldHaveNoDiagnostics();

		[Fact]
		public void CapturingArgumentPassedAsNoCaptureButContainingNestedCapturingLambdaIsReported()
			=> _verifier
			.Source
			(
@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	private readonly int _x = 42;
	public void CallSite() => Invoke( ( list, v ) => list.Aggregate( _x, ( agg, x ) => agg + x + v ) );
	public void Invoke( [NoCapture] Expression<Func<List<int>, int, int>> f ) { }
}
"
			)
			.ShouldHaveDiagnostic( "Invoke", "f", "this" );

		[Fact]
		public void CapturingArgumentPassedAsNoCaptureButContainingNestedCapturingLambdaWhichCapturesTheSameThingCapturedInItsParentIsReported()
			=> _verifier
			.Source
			(
@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

class C
{
	private readonly int _x = 42;
	public void CallSite() => Invoke( ( list, v ) => list.Aggregate( _x, ( agg, x ) => _x + agg + x + v ) );
	public void Invoke( [NoCapture] Expression<Func<List<int>, int, int>> f ) { }
}
"
			)
			.ShouldHaveDiagnostic( "Invoke", "f", "this" );

		[Fact]
		public void NestedCapturingFromTheOuterArgumentIsIgnored()
			=> _verifier
			.Source
			(
@"
using System;
using System.Collections.Generic;

static class InvokeExtension
{
	public static int Invoke( this List<int> list, Func<int, int> inner ) => inner( 42 );
}

class C
{
	private readonly int _x = 42;
	public void CallSite() => InvokeOuter( ( list, v ) => list.Invoke( _ => v ) );
	public void InvokeOuter( Func<List<int>, int, int> outer ) { }
}
"
			)
			.ShouldHaveNoDiagnostics();

		[Fact]
		public void NestedCapturingFromTheOuterArgumentPassedAsNoCaptureIsReported()
			=> _verifier
			.Source
			(
@"
using System;
using System.Collections.Generic;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

static class InvokeExtension
{
	public static int Invoke( this List<int> list, [NoCapture] Func<int, int> inner ) => inner( 42 );
}

class C
{
	private readonly int _x = 42;
	public void CallSite() => InvokeOuter( ( list, v ) => list.Invoke( _ => v ) );
	public void InvokeOuter( Func<List<int>, int, int> outer ) { }
}
"
			)
			.ShouldHaveDiagnostic( "Invoke", "inner", "v" );

		[Fact]
		public void NestedCapturingFromTheOutsideWorldArgumentPassedAsNoCaptureIsReported()
			=> _verifier
			.Source
			(
@"
using System;
using System.Collections.Generic;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

static class InvokeExtension
{
	public static int Invoke( this List<int> list, [NoCapture] Func<int, int> inner ) => inner( 42 );
}

class C
{
	private readonly int _x = 42;
	public void CallSite() => InvokeOuter( ( list, v ) => list.Invoke( _ => _x ) );
	public void InvokeOuter( Func<List<int>, int, int> outer ) { }
}
"
			)
			.ShouldHaveDiagnostic( "Invoke", "inner", "this" );

		[Fact]
		public void ArgumentPassedAsNoCaptureContainingArgumentCapturingFromTheOutsideWorldArgumentIsReported()
			=> _verifier
			.Source
			(
@"
using System;
using System.Collections.Generic;

[AttributeUsage( AttributeTargets.Parameter | AttributeTargets.Method )]
class NoCaptureAttribute : Attribute
{
}

static class InvokeExtension
{
	public static int Invoke( this List<int> list, Func<int, int> inner ) => inner( 42 );
}

class C
{
	private readonly int _x = 42;
	public void CallSite() => InvokeOuter( ( list, v ) => list.Invoke( _ => _x ) );
	public void InvokeOuter( [NoCapture] Func<List<int>, int, int> outer ) { }
}
"
			)
			// outer is to be reported because closure for the inner one is created from closure for the outer one
			.ShouldHaveDiagnostic( "InvokeOuter", "outer", "this" );
	}
}
