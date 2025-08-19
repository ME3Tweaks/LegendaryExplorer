using CommandLine;

namespace LegendaryExplorerUnrealDoc
{
    /// <summary>
    /// Command line arguments.
    /// </summary>
    internal class CLIOptions
    {
        [Option('i', "inputfolder", Required = true, HelpText = "Sets the input folder; should contain folders for each game.")]
        public string InputFolder { get; set; }

        [Option('o', "outputfolder", Required = true, HelpText = "Sets the html output folder.")]
        public string OutputFolder { get; set; }

        [Option('b', "binaryoutputfolder", Required = true, HelpText = "Sets the binary output folder.")]
        public string BinaryOutputFolder { get; set; }
    }
}
