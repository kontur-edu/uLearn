﻿using System;

namespace uLearn.Courses.BasicProgramming.Slides
{
	[Id("Ariphmetic_operations")]
	[Title("Арифметические операции и var")]
	public class S06_Operations
	{
		/*
		С прибавлением единицы все оказалось просто. Казалось бы прибавление к числу половинки должно быть не сложнее...

		Подумайте, как так получилось, что казалось бы корректная программа не работает.
		Исправьте программу так, чтобы она давала корректный ответ.
		*/

		[Exercise]
		[Hint("Вспомните, как именно работает var. Какой тип у меременной a?")]
		[ExpectedOutput("5.5")]
		static public void Main()
		{
			double a = 5;
			a += 0.5;
			Console.WriteLine(a);
			/*uncomment
			var a = 5;
			a += 0.5;
			Console.WriteLine(a)
			*/
		}


	}
}
