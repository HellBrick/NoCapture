using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace HellBrick.NoCapture.Analyzer
{
	internal static partial class SemanticModelExtensions
	{
		public static ISymbol GetSingleSymbol( this SemanticModel semanticModel, ExpressionSyntax expression )
		{
			SymbolInfo symbolInfo = semanticModel.GetSymbolInfo( expression );
			return symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.OnlyOrDefault();
		}
	}
}
