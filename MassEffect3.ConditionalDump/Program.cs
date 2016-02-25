using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Gammtek.Conduit.IO;
using MassEffect3.Options;

namespace MassEffect3.ConditionalDump
{
	internal class Program
	{
		private static string GetExecutableName()
		{
			return Path.GetFileName(Assembly.GetExecutingAssembly().Location);
		}

		public static void Main(string[] args)
		{
			var showHelp = false;

			var options = new OptionSet
			{
				{
					"h|help",
					"show this message and exit",
					v => showHelp = v != null
				},
			};

			var initialArgs = new[]
			{
				@"C:\Users\Matthew\Desktop\Mass Effect 3\_Temp\DLC_EXP_Pack003\ConditionalsDLC_EXP_Pack003.cnd"
			};

			args = initialArgs;
			List<string> extras;

			try
			{
				extras = options.Parse(args);
			}
			catch (OptionException e)
			{
				Console.Write("{0}: ", GetExecutableName());
				Console.WriteLine(e.Message);
				Console.WriteLine("Try `{0} --help' for more information.", GetExecutableName());
				return;
			}

			if (extras.Count < 1 || extras.Count > 2 ||
				showHelp)
			{
				Console.WriteLine("Usage: {0} [OPTIONS]+ input_cnd [output_file]", GetExecutableName());
				Console.WriteLine();
				Console.WriteLine("Options:");
				options.WriteOptionDescriptions(Console.Out);
				return;
			}

			var inputPath = extras[0];
			var outputPath = extras.Count > 1 ? extras[1] : null;

			var cnd = new ConditionalsFile();
			using (var input = File.OpenRead(inputPath))
			{
				cnd.Deserialize(input);
			}

			if (outputPath == null)
			{
				DumpConditionals(Console.Out, cnd);
			}
			else
			{
				using (var output = File.Create(outputPath))
				{
					var writer = new StreamWriter(output);
					DumpConditionals(writer, cnd);
					writer.Flush();
				}
			}
		}

		private static void DumpConditionals(TextWriter writer, ConditionalsFile cnd)
		{
			foreach (var id in cnd.Ids.OrderBy(id => id))
			{
				writer.WriteLine("[conditional_{0}]", id);

				var buffer = cnd.GetConditional(id);

				var c1 = ByteBufferReaderTest.DumpConditionalBool(new ByteBufferReader(buffer, 0, cnd.ByteOrder));

				//var c2 = DataReaderTest.DumpConditionalBool(new DataReader(new MemoryStream(buffer, 0, buffer.Length), cnd.ByteOrder));

				writer.WriteLine("{0}", c1);

				//writer.WriteLine("{0}", c2);

				writer.WriteLine();
			}

			Console.ReadKey();
		}
	}
}
