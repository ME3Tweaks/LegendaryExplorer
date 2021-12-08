using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.PlotDatabase.Serialization;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase.Databases
{
    /// <summary>
    /// Represents a table of plot elements for a single game created from the BioWare-provided plot databases
    /// </summary>
    public class BasegamePlotDatabase : PlotDatabaseBase
    {
        public override bool IsBioware => true;

        /// <summary>
        /// Initializes a new <see cref="BasegamePlotDatabase"/> and loads it with the BioWare plot data that ships with LEC
        /// </summary>
        /// <param name="game">Game to load data for</param>
        public BasegamePlotDatabase(MEGame game)
        {
            Game = game;
            LoadPlotsFromLEC(game);
        }

        private void LoadPlotsFromLEC(MEGame game)
        {
            Game = game;

            string json = LegendaryExplorerCoreUtilities.LoadStringFromCompressedResource("PlotDatabases.zip",
                LegendaryExplorerCoreLib.CustomPlotFileName(game));

            var pdb = JsonConvert.DeserializeObject<SerializedPlotDatabase>(json, _jsonSerializerSettings);
            if (pdb != null)
            {
                pdb.BuildTree();
                ImportPlots(pdb);
                Root = Organizational[1];
            }
        }
    }
}