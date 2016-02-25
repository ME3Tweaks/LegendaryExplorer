using System;
using System.Linq;
using Gammtek.Conduit.CommandLine;

namespace MassEffect3.Coalesce
{
	public class ProgramOptions
	{
		/*[Option('m', "mode",
			DefaultValue = ProgramCoalesceMode.Unknown,
			HelpText = @"Output mode")]*/
		public ProgramCoalesceMode CoalesceMode { get; set; }

		//[Value(0, Required = true)]
		public string Source { get; set; }

		//[Value(1, Required = false)]
		public string Destination { get; set; }

		/*[Option("replace-whitespace",
			DefaultValue = false,
			HelpText = @"Replace whitespace characters")]
		public bool ReplaceWhitespace { get; set; }*/

		public static FluentCommandLineParser<ProgramOptions> Create(string[] args)
		{
			var parser = new FluentCommandLineParser<ProgramOptions>();

			parser.Setup(arg => arg.Source)
				.As('s', "source")
				.SetDefault(args.Any() ? args[0] : null);

			parser.Setup(arg => arg.Destination)
				.As('d', "destination")
				.SetDefault(args.Length >= 2 ? args[1] : null);

			parser.Setup(arg => arg.CoalesceMode)
				.As('m', "mode")
				.SetDefault(ProgramCoalesceMode.Unknown);

			parser.SetupHelp("?", "help")
				.Callback(text => Console.WriteLine(text));

			return parser;
		}
	}
}
