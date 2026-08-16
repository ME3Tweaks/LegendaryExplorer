using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.PathfindingNetworkEditor.Editor;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using System;
using System.IO;
using System.Linq;

namespace LegendaryExplorer.Tools.PathfindingNetworkEditor.Models
{
    /// <summary>
    /// Handles the storage/loading of level packages
    /// </summary>
    public class LevelMultiPackageHandler : NotifyPropertyChangedBase
    {
        /// <summary>
        /// Gets the game associated with the first open level, if available.
        /// </summary>
        public MEGame? Game => OpenLevelsList.FirstOrDefault()?.Package.Game;

        /// <summary>
        /// Gets the first open level's name, or a default message if no levels are loaded.
        /// </summary>
        public string MasterLevelName => OpenLevelsList.FirstOrDefault()?.LevelName ?? "No level loaded";

        /// <summary>
        /// The list of available levels.
        /// </summary>
        public ObservableCollectionExtended<LevelData> OpenLevelsList { get; set; } = new();

        public void OpenLevelMaster(string path, Action<string> loadingFileCallback = null)
        {
            OpenLevelsList.ClearEx();

            loadingFileCallback?.Invoke(Path.GetFileNameWithoutExtension(path));
            var levelMaster = (MEPackage)MEPackageHandler.OpenMEPackage(path);
            OpenLevelsList.Add(new LevelData(levelMaster));

            var levelFiles = MELoadedFiles.GetFilesLoadedInGame(levelMaster.Game);
            foreach (var p in levelMaster.AdditionalPackagesToCook)
            {
                if (levelFiles.TryGetValue(p + ".pcc", out var level))
                {
                    loadingFileCallback?.Invoke(Path.GetFileNameWithoutExtension(path));
                    var subLevel = MEPackageHandler.OpenMEPackage(level);
                    OpenLevelsList.Add(new LevelData(subLevel));
                }
            }

            // Notify game has changed.
            OnPropertyChanged(nameof(Game));
            OnPropertyChanged(nameof(MasterLevelName));
        }
    }
}