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
                BasegamePlotFileName(game));

            var pdb = JsonConvert.DeserializeObject<SerializedPlotDatabase>(json, _jsonSerializerSettings);
            if (pdb != null)
            {
                pdb.BuildTree();
                ImportPlots(pdb);
                Root = Organizational[1];
            }
        }

        /// <summary>
        /// Gets the filename of the BioWare plot database .json file that is shipped with the LEC library
        /// </summary>
        /// <param name="game">Game to get filename for</param>
        /// <returns>Name of a .json file</returns>
        public static string BasegamePlotFileName(MEGame game) => game switch
        {
            MEGame.ME3 => "le3.json",
            MEGame.ME2 => "le2.json",
            MEGame.ME1 => "le1.json",
            MEGame.LE3 => "le3.json",
            MEGame.LE2 => "le2.json",
            MEGame.LE1 => "le1.json",
            _ => "le3.json"
        };
    }
}