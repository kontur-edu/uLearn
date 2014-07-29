﻿using System;

namespace uLearn.Courses.BasicProgramming.Slides
{
	[Id("Strings")]
	[Title("Строки")]
	class S04_Strings
	{
		/*
		Допишите код вместо ```...``` так, чтобы он выводил на экран вторую половину строки ```text```.
		*/
		[Exercise]
		[ExpectedOutput("CSharp!")]
		[Hint("Можете ли вы собрать решение из известных вам уже функций работы со строками?")]
		static public void Main()
		{
			string text = "I love CSharp!";
			string secondHalf = text.Substring(text.Length / 2);
			Console.WriteLine(secondHalf);
			/*uncomment
			string text = "I love CSharp!";
			string secondHalf = ...;
			Console.WriteLine(half);
			*/
		}
	}
}
