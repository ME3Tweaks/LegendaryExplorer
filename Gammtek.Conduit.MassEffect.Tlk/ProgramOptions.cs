using Gammtek.Conduit.CommandLine;

namespace Gammtek.Conduit.MassEffect.Tlk
{
	public class ProgramOptions
	{
		//[Value(1, Required = false)]
		public string Destination { get; set; }

		//[Value(0, Required = false)]
		public string Source { get; set; }

		/*[Option('m', "mode",
			DefaultValue = ProgramTlkMode.Unknown,
			HelpText = @"Output mode")]*/
		public ProgramTlkMode TlkMode { get; set; }
	}
}
