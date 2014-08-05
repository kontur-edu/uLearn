using System;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace uLearn.CSharp
{
	public static class SlideParser
	{
		public static Slide ParseSlide(string filename, SlideInfo slideInfo, Func<string, string> getInclude)
		{
			SyntaxTree tree = CSharpSyntaxTree.ParseFile(filename);
			return ParseSyntaxTree(tree, slideInfo, "Using System; Using System.Linq;", getInclude);
		}

		public static Slide ParseCode(string sourceCode, SlideInfo slideInfo, string prelude, Func<string, string> getInclude)
		{
			SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
			return ParseSyntaxTree(tree, slideInfo, prelude, getInclude);
		}

		private static Slide ParseSyntaxTree(SyntaxTree tree, SlideInfo slideInfo, string prelude,
			Func<string, string> getInclude)
		{
			var blocksBuilder = new SlideBuilder(getInclude);
			var exerciseBuilder = new ExerciseBuilder();
			blocksBuilder.Visit(tree.GetRoot());
			var sourceForTestingRoot = exerciseBuilder.Visit(tree.GetRoot());
			if (!exerciseBuilder.IsExercise)
				return new Slide(blocksBuilder.Blocks, slideInfo, blocksBuilder.Title, blocksBuilder.Id);
			return new ExerciseSlide(blocksBuilder.Blocks, exerciseBuilder.ExerciseInitialCode, exerciseBuilder.ExpectedOutput, exerciseBuilder.Hints,
				new SolutionBuilder(sourceForTestingRoot, prelude, exerciseBuilder.Validators), slideInfo, blocksBuilder.Title, blocksBuilder.Id);
		}
	}
}