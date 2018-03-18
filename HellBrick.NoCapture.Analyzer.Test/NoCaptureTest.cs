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
				.And.Subject.First().GetMessage().Should().Be( $"{methodName}( {parameterName} ) doesn't allow capturing lambdas. Captured variables: {String.Join( ",", capturedVariables )}." )
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
	}
}
