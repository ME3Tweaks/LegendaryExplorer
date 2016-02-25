using Gammtek.Conduit.CommandLine;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	public class ProgramOptions
	{
		/*[Option('m', "mode",
			DefaultValue = ProgramCoalesceMode.Unknown,
			HelpText = @"Output mode")]*/
		public ProgramCoalesceMode CoalesceMode { get; set; }

		//[Value(0, Required = false)]
		public string Source { get; set; }

		//[Value(1, Required = false)]
		public string Destination { get; set; }
	}
}
