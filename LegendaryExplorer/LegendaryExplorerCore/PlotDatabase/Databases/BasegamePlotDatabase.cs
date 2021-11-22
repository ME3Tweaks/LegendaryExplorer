using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using Newtonsoft.Json;

namespace LegendaryExplorerCore.PlotDatabase
{
    public class BasegamePlotDatabase : PlotDatabaseBase
    {
        public override bool IsBioware => true;

        public void LoadPlotsFromLEC(MEGame game)
        {
            Game = game;
            var pdb = new SerializedPlotDatabase();

            string json = LegendaryExplorerCoreUtilities.LoadStringFromCompressedResource("PlotDatabases.zip",
                LegendaryExplorerCoreLib.CustomPlotFileName(game));

            pdb = JsonConvert.DeserializeObject<SerializedPlotDatabase>(json, _jsonSerializerSettings);
            pdb.BuildTree();
            ImportPlots(pdb);

            Root = Organizational[1];
        }

        public static BasegamePlotDatabase CreateBasegamePlotDatabase(MEGame game)
        {
            var db = new BasegamePlotDatabase();
            db.LoadPlotsFromLEC(game);
            return db;
        }
    }
}