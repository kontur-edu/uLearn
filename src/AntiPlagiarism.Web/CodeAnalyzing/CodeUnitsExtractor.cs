﻿using System.Collections.Generic;
using System.Linq;
using Ulearn.Common;

namespace AntiPlagiarism.Web.CodeAnalyzing
{
	public class CodeUnitsExtractor
	{
		private readonly CSharpCodeUnitsExtractor csharpCodeUnitsExtractor;
		private readonly TokensExtractor tokensExtractor;

		public CodeUnitsExtractor(CSharpCodeUnitsExtractor csharpCodeUnitsExtractor,
			TokensExtractor tokensExtractor)
		{
			this.csharpCodeUnitsExtractor = csharpCodeUnitsExtractor;
			this.tokensExtractor = tokensExtractor;
		}

		public List<CodeUnit> Extract(string program, Language language)
		{
			return language == Language.CSharp
				? csharpCodeUnitsExtractor.Extract(program)
				: ExtractNonCSharp(program, language);
		}

		private List<CodeUnit> ExtractNonCSharp(string program, Language language)
		{
			var tokens = tokensExtractor.GetFilteredTokensFromPygmentize(program, language);
			RenameTokenTypes(tokens);
			var codePath = new CodePath(Enumerable.Empty<CodePathPart>());
			var codeUnit = new CodeUnit(codePath, tokens);
			return new List<CodeUnit> { codeUnit };
		}

		private readonly string[] tokenTypesToAppendValue = { "Punctuation", "Keyword", "Operator", "Keyword.Reserved" };
		private void RenameTokenTypes(List<Token> tokens)
		{
			foreach (var token in tokens)
			{
				if (tokenTypesToAppendValue.Contains(token.Type))
					token.Type += $".{token.Value}";
			}
		}
	}
}