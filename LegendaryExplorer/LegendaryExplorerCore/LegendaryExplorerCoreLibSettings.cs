using System.ComponentModel;
using LegendaryExplorerCore.DebugTools;

namespace LegendaryExplorerCore
{
    /// <summary>
    /// Class that contains settings specific to Legendary Explorer Core. When this class is initially accessed, the game directories will automatically populate themselves
    /// </summary>
    public class LegendaryExplorerCoreLibSettings : INotifyPropertyChanged
    {
        // In LegendaryExplorer (not this lib) the property changed event is listened to for changes and maps them into the settings system for LEX

        /// <summary>
        /// The singleton instance of the LegendaryExplorerCoreLibSettings object
        /// </summary>
        public static LegendaryExplorerCoreLibSettings Instance { get; }

        static LegendaryExplorerCoreLibSettings()
        {
            Instance = new LegendaryExplorerCoreLibSettings();
            GameFilesystem.ME1Directory.ReloadDefaultGamePath();
            LECLog.Information($"ME1 Default Path set to {GameFilesystem.LE1Directory.DefaultGamePath}");
            GameFilesystem.ME2Directory.ReloadDefaultGamePath();
            LECLog.Information($"ME2 Default Path set to {GameFilesystem.ME2Directory.DefaultGamePath}");
            GameFilesystem.ME3Directory.ReloadDefaultGamePath();
            LECLog.Information($"ME3 Default Path set to {GameFilesystem.ME3Directory.DefaultGamePath}");
            GameFilesystem.LEDirectory.LookupDefaultPath(); // LE Directory is root of 3 games plus the launcher 
            LECLog.Information($"LE1 Default Path set to {GameFilesystem.LE1Directory.DefaultGamePath}");
            LECLog.Information($"LE2 Default Path set to {GameFilesystem.LE2Directory.DefaultGamePath}");
            LECLog.Information($"LE3 Default Path set to {GameFilesystem.LE3Directory.DefaultGamePath}");

            GameFilesystem.UDKDirectory.ReloadDefaultGamePath();
            // Don't care about logging UDK path
        }

        /// <summary>
        /// If TLKs that load should be male or female version
        /// </summary>
        public bool TLKGenderIsMale { get; set; }

        /// <summary>
        /// The default TLK language to use
        /// </summary>
        public string TLKDefaultLanguage { get; set; } = "INT"; // maybe should be enum?

        /// <summary>
        /// If an array type can't be determined, it defaults to integers. Setting this to true makes them parse of objects instead
        /// </summary>
        public bool ParseUnknownArrayTypesAsObject { get; set; }

        /// <summary>
        /// Value that can be bound to to bridge location settings from an enclosing wrapper application
        /// </summary>
        public string ME1Directory { get; set; }
        /// <summary>
        /// Value that can be bound to to bridge location settings from an enclosing wrapper application
        /// </summary>
        public string ME2Directory { get; set; }
        /// <summary>
        /// Value that can be bound to to bridge location settings from an enclosing wrapper application
        /// </summary>
        public string ME3Directory { get; set; }
        /// <summary>
        /// Value that can be bound to to bridge location settings from an enclosing wrapper application
        /// </summary>
        public string LEDirectory { get; set; }
        /// <summary>
        /// Value that can be bound to to bridge location settings from an enclosing wrapper application
        /// </summary>
        public string UDKCustomDirectory { get; set; }

#pragma warning disable
        /// <summary>
        /// Can be subscribed to for applications to be notified when settings change for this class
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
