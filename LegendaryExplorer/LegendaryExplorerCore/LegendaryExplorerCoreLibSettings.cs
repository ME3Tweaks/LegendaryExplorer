﻿using System.ComponentModel;

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
            GameFilesystem.ME2Directory.ReloadDefaultGamePath();
            GameFilesystem.ME3Directory.ReloadDefaultGamePath();
            GameFilesystem.LEDirectory.LookupDefaultPath(); // LE Directory is root of 3 games plus the launcher 
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
        /// Value that can be binded to to bridge location settings from an enclosing wrapper application
        /// </summary>
        public string ME1Directory { get; set; }
        /// <summary>
        /// Value that can be binded to to bridge location settings from an enclosing wrapper application
        /// </summary>
        public string ME2Directory { get; set; }
        /// <summary>
        /// Value that can be binded to to bridge location settings from an enclosing wrapper application
        /// </summary>
        public string ME3Directory { get; set; }
        /// <summary>
        /// Value that can be binded to to bridge location settings from an enclosing wrapper application
        /// </summary>
        public string LEDirectory { get; set; }

#pragma warning disable
        /// <summary>
        /// Can be subscribed to for applications to be notified when settings change for this class
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
