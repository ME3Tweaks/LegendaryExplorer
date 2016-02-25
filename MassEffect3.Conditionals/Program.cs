using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MassEffect3.Conditionals.IO;

namespace MassEffect3.Conditionals
{
	public static class Program
	{
		private static string GetExecutableName()
		{
			return Path.GetFileName(Assembly.GetExecutingAssembly().Location);
		}

		public static void Main(string[] args)
		{
			var initialArgs = new[]
			{
				@"C:\Users\Matthew\Desktop\Mass Effect 3\_Temp\DLC_EXP_Pack003\ConditionalsDLC_EXP_Pack003.cnd"
			};

			if (File.Exists(initialArgs[0]))
			{
				var cnd = BinaryConditionals.Load(initialArgs[0]);

				foreach (var entry in cnd.Entries)
				{
					Console.WriteLine("[conditional_{0}]", entry.Id);
					

					Console.WriteLine();
				}
			}

			Console.WriteLine("Press any key to continue...");
			Console.ReadKey();
		}
	}
}
