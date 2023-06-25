using System.ComponentModel;
using LegendaryExplorer.Misc.AppSettings;
using LegendaryExplorerCore;
using LegendaryExplorerCore.Compression;

namespace LegendaryExplorer.Misc
{
    public static class CoreLibSettingsBridge
    {
        /// <summary>
        /// Maps core-lib settings into LegendaryExplorer and sets up listeners for changes to the settings.
        /// Call this method only once.
        /// </summary>
        public static void MapSettingsIntoBridge()
        {
            LegendaryExplorerCoreLibSettings.Instance.ParseUnknownArrayTypesAsObject = Settings.Global_PropertyParsing_ParseUnknownArrayTypeAsObject;
            LegendaryExplorerCoreLibSettings.Instance.TLKDefaultLanguage = Settings.Global_TLK_Language;
            LegendaryExplorerCoreLibSettings.Instance.TLKGenderIsMale = Settings.Global_TLK_IsMale;
            LegendaryExplorerCoreLibSettings.Instance.ME1Directory = Settings.Global_ME1Directory;
            LegendaryExplorerCoreLibSettings.Instance.ME2Directory = Settings.Global_ME2Directory;
            LegendaryExplorerCoreLibSettings.Instance.ME3Directory = Settings.Global_ME3Directory;
            LegendaryExplorerCoreLibSettings.Instance.LEDirectory = Settings.Global_LEDirectory;
            LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory = Settings.Global_UDKCustomDirectory;

            Settings.StaticPropertyChanged += LEXSettingChanged;
            LegendaryExplorerCoreLibSettings.Instance.PropertyChanged += CoreLibSettingChanged;
        }

        private static void LEXSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Settings.Global_PropertyParsing_ParseUnknownArrayTypeAsObject):
                    LegendaryExplorerCoreLibSettings.Instance.ParseUnknownArrayTypesAsObject = Settings.Global_PropertyParsing_ParseUnknownArrayTypeAsObject;
                    break;
                case nameof(Settings.Global_TLK_Language):
                    LegendaryExplorerCoreLibSettings.Instance.TLKDefaultLanguage = Settings.Global_TLK_Language;
                    break;
                case nameof(Settings.Global_TLK_IsMale):
                    LegendaryExplorerCoreLibSettings.Instance.TLKGenderIsMale = Settings.Global_TLK_IsMale;
                    break;
                case nameof(Settings.Global_ME1Directory):
                    LegendaryExplorerCoreLibSettings.Instance.ME1Directory = Settings.Global_ME1Directory;
                    break;
                case nameof(Settings.Global_ME2Directory):
                    LegendaryExplorerCoreLibSettings.Instance.ME2Directory = Settings.Global_ME2Directory;
                    break;
                case nameof(Settings.Global_ME3Directory):
                    LegendaryExplorerCoreLibSettings.Instance.ME3Directory = Settings.Global_ME3Directory;
                    break;
                case nameof(Settings.Global_LEDirectory):
                    LegendaryExplorerCoreLibSettings.Instance.LEDirectory = Settings.Global_LEDirectory;
                    OodleHelper.EnsureOodleDll();
                    break;
                case nameof(Settings.Global_UDKCustomDirectory):
                    LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory = Settings.Global_UDKCustomDirectory;
                    OodleHelper.EnsureOodleDll();
                    break;
            }
        }

        private static void CoreLibSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(LegendaryExplorerCoreLibSettings.Instance.ParseUnknownArrayTypesAsObject):
                    Settings.Global_PropertyParsing_ParseUnknownArrayTypeAsObject = LegendaryExplorerCoreLibSettings.Instance.ParseUnknownArrayTypesAsObject;
                    break;
                case nameof(LegendaryExplorerCoreLibSettings.Instance.TLKDefaultLanguage):
                    Settings.Global_TLK_Language = LegendaryExplorerCoreLibSettings.Instance.TLKDefaultLanguage;
                    break;
                case nameof(LegendaryExplorerCoreLibSettings.Instance.TLKGenderIsMale):
                    Settings.Global_TLK_IsMale = LegendaryExplorerCoreLibSettings.Instance.TLKGenderIsMale;
                    break;
                case nameof(LegendaryExplorerCoreLibSettings.Instance.ME1Directory):
                    Settings.Global_ME1Directory = LegendaryExplorerCoreLibSettings.Instance.ME1Directory;
                    break;
                case nameof(LegendaryExplorerCoreLibSettings.Instance.ME2Directory):
                    Settings.Global_ME2Directory = LegendaryExplorerCoreLibSettings.Instance.ME2Directory;
                    break;
                case nameof(LegendaryExplorerCoreLibSettings.Instance.ME3Directory):
                    Settings.Global_ME3Directory = LegendaryExplorerCoreLibSettings.Instance.ME3Directory;
                    break;
                case nameof(LegendaryExplorerCoreLibSettings.Instance.LEDirectory):
                    Settings.Global_LEDirectory = LegendaryExplorerCoreLibSettings.Instance.LEDirectory;
                    OodleHelper.EnsureOodleDll();
                    break;
                case nameof(LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory):
                    Settings.Global_UDKCustomDirectory = LegendaryExplorerCoreLibSettings.Instance.UDKCustomDirectory;
                    break;
                default:
                    return;
            }
            Settings.Save();
        }
    }
}
