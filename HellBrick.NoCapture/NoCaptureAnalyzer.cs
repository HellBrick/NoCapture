using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace HellBrick.NoCapture.Analyzer
{
	[DiagnosticAnalyzer( LanguageNames.CSharp )]
	public class NoCaptureAnalyzer : DiagnosticAnalyzer
	{
		public const string DiagnosticId = "HBNoCapture";
		private const string _noCaptureAttributeName = nameof( NoCaptureAttribute );

		private static readonly DiagnosticDescriptor _descriptor
			= new DiagnosticDescriptor
			(
				DiagnosticId,
				"Capturing is not allowed",
				"{0}( {1} ) doesn't allow capturing lambdas. Captured variables: {2}.",
				"Performance",
				DiagnosticSeverity.Error,
				isEnabledByDefault: true
			);

		private static readonly ImmutableArray<SyntaxKind> _targetNodeTypes
			= ImmutableArray.Create
			(
				SyntaxKind.SimpleLambdaExpression,
				SyntaxKind.ParenthesizedLambdaExpression
			);

		public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create( _descriptor );

		public override void Initialize( AnalysisContext context )
		{
			context.ConfigureGeneratedCodeAnalysis( GeneratedCodeAnalysisFlags.None );
			context.RegisterSyntaxNodeAction( c => ExamineNode( c ), _targetNodeTypes );

			void ExamineNode( SyntaxNodeAnalysisContext nodeContext )
			{
				if ( TryGetSymbols( nodeContext.Node, nodeContext.SemanticModel, out IMethodSymbol methodSymbol, out IParameterSymbol parameterSymbol ) )
				{
					if ( HasNoCaptureAttribute( methodSymbol ) || HasNoCaptureAttribute( parameterSymbol ) )
					{
						DataFlowAnalysis dataFlow = nodeContext.SemanticModel.AnalyzeDataFlow( nodeContext.Node );
						if ( !dataFlow.Captured.IsEmpty )
						{
							Diagnostic diagnostic
								= Diagnostic.Create
								(
									_descriptor,
									nodeContext.Node.GetLocation(),
									methodSymbol.Name,
									parameterSymbol.Name,
									String.Join( ", ", dataFlow.Captured.Select( s => s.Name ) )
								);

							nodeContext.ReportDiagnostic( diagnostic );
						}
					}

					bool HasNoCaptureAttribute( ISymbol symbol )
						=> symbol.GetAttributes().Any( a => a.AttributeClass.Name == _noCaptureAttributeName );
				}
			}

			bool TryGetSymbols( SyntaxNode node, SemanticModel semanticModel, out IMethodSymbol method, out IParameterSymbol parameter )
			{
				if
				(
					node.Parent is ArgumentSyntax argument
					&& argument.Parent is ArgumentListSyntax argumentList
					&& argumentList.Parent is InvocationExpressionSyntax invocation
					&& semanticModel.GetSingleSymbol( invocation.Expression ) is IMethodSymbol methodSymbol
				)
				{
					method = methodSymbol;
					parameter
						= argument.NameColon is NameColonSyntax nameColon
						? methodSymbol.Parameters.First( p => p.Name == nameColon.Name.Identifier.Text )
						: methodSymbol.Parameters[ argumentList.Arguments.IndexOf( argument ) ];

					return true;
				}
				else
				{
					method = null;
					parameter = null;
					return false;
				}
			}
		}
	}
}
