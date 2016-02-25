using System;
using System.Collections.Generic;
using Gammtek.Conduit.IO;

namespace Gammtek.Conduit.MassEffect.ModBuilder
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			var pathFrom = @"c:\FolderA\FolderB\FolderC\";
			var pathTo = @"c:\FolderA\FolderD\FolderE\Program.exe";
			var pathRelative = @"..\..\FolderD\FolderE";

			var pathResult = ConduitPath.MakePathRelativeTo(pathTo, pathFrom, false);

			Console.WriteLine(pathRelative);
			Console.WriteLine(pathResult);
			Console.WriteLine(pathRelative.Equals(pathResult));

			var exp1 = @"$(Var1)";
			var expAdd = new Dictionary<string, string>()
			{
				{ "Var1", "$(Var2).$(Var3) => $(Var6)" },
				{ "Var2", "$(Var4)" },
				{ "Var3", "$(Var5)" },
				{ "Var4", "Var4" },
				{ "Var5", "Var5" },
				{ "Var6", "Var6" },
			};

			var expResult = Utilities.ExpandVariables(exp1, expAdd);

			Console.WriteLine(expResult);

			Console.ReadKey(true);
		}
	}
}
