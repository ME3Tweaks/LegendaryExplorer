using System.ComponentModel;

namespace LegendaryExplorerCore
{
    public class LegendaryExplorerCoreLibSettings : INotifyPropertyChanged
    {
        // In LegendaryExplorer (not this lib) the property changed event is listened to for changes and maps them into the settings system for LEX
        public static LegendaryExplorerCoreLibSettings Instance { get; set; }

        public LegendaryExplorerCoreLibSettings()
        {
            Instance = this;
            GameFilesystem.ME1Directory.ReloadDefaultGamePath();
            GameFilesystem.ME2Directory.ReloadDefaultGamePath();
            GameFilesystem.ME3Directory.ReloadDefaultGamePath();
            GameFilesystem.LEDirectory.LookupDefaultPath(); // LE Directory is root of 3 games plus the launcher 
        }
        public bool TLKGenderIsMale { get; set; }
        public string TLKDefaultLanguage { get; set; } = "INT"; // maybe should be enum?
        public bool ParseUnknownArrayTypesAsObject { get; set; }
        public string ME1Directory { get; set; }
        public string ME2Directory { get; set; }
        public string ME3Directory { get; set; }
        public string LEDirectory { get; set; }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
