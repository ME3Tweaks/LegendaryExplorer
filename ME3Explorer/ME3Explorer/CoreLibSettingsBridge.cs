using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3ExplorerCore;

namespace ME3Explorer
{
    public static class CoreLibSettingsBridge
    {
        /// <summary>
        /// Maps core-lib settings into ME3Explorer and sets up listeners for changes to the settings.
        /// Call this method only once.
        /// </summary>
        public static void MapSettingsIntoBridge()
        {
            ME3ExplorerCoreLibSettings.Instance.ParseUnknownArrayTypesAsObject = Properties.Settings.Default.PropertyParsingUnknownArrayAsObject;
            ME3ExplorerCoreLibSettings.Instance.TLKDefaultLanguage = Properties.Settings.Default.TLKLanguage;
            ME3ExplorerCoreLibSettings.Instance.TLKGenderIsMale = Properties.Settings.Default.TLKGender_IsMale;
            ME3ExplorerCoreLibSettings.Instance.ME1Directory = Properties.Settings.Default.ME1Directory;
            ME3ExplorerCoreLibSettings.Instance.ME2Directory = Properties.Settings.Default.ME2Directory;
            ME3ExplorerCoreLibSettings.Instance.ME3Directory = Properties.Settings.Default.ME3Directory;

            Properties.Settings.Default.PropertyChanged += ME3ExpSettingChanged;
            ME3ExplorerCoreLibSettings.Instance.PropertyChanged += CoreLibSettingChanged;
        }

        private static void ME3ExpSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Properties.Settings.Default.PropertyParsingUnknownArrayAsObject):
                    ME3ExplorerCoreLibSettings.Instance.ParseUnknownArrayTypesAsObject = Properties.Settings.Default.PropertyParsingUnknownArrayAsObject;
                    break;
                case nameof(Properties.Settings.Default.TLKLanguage):
                    ME3ExplorerCoreLibSettings.Instance.TLKDefaultLanguage = Properties.Settings.Default.TLKLanguage;
                    break;
                case nameof(Properties.Settings.Default.TLKGender_IsMale):
                    ME3ExplorerCoreLibSettings.Instance.TLKGenderIsMale = Properties.Settings.Default.TLKGender_IsMale;
                    break;
                case nameof(Properties.Settings.Default.ME1Directory):
                    ME3ExplorerCoreLibSettings.Instance.ME1Directory = Properties.Settings.Default.ME1Directory;
                    break;
                case nameof(Properties.Settings.Default.ME2Directory):
                    ME3ExplorerCoreLibSettings.Instance.ME2Directory = Properties.Settings.Default.ME2Directory;
                    break;
                case nameof(Properties.Settings.Default.ME3Directory):
                    ME3ExplorerCoreLibSettings.Instance.ME3Directory = Properties.Settings.Default.ME3Directory;
                    break;
            }
        }

        private static void CoreLibSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(ME3ExplorerCoreLibSettings.Instance.ParseUnknownArrayTypesAsObject):
                    Properties.Settings.Default.PropertyParsingUnknownArrayAsObject = ME3ExplorerCoreLibSettings.Instance.ParseUnknownArrayTypesAsObject;
                    break;
                case nameof(ME3ExplorerCoreLibSettings.Instance.TLKDefaultLanguage):
                    Properties.Settings.Default.TLKLanguage = ME3ExplorerCoreLibSettings.Instance.TLKDefaultLanguage;
                    break;
                case nameof(ME3ExplorerCoreLibSettings.Instance.TLKGenderIsMale):
                    Properties.Settings.Default.TLKGender_IsMale = ME3ExplorerCoreLibSettings.Instance.TLKGenderIsMale;
                    break;
                case nameof(ME3ExplorerCoreLibSettings.Instance.ME1Directory):
                    Properties.Settings.Default.ME1Directory = ME3ExplorerCoreLibSettings.Instance.ME1Directory;
                    break;
                case nameof(ME3ExplorerCoreLibSettings.Instance.ME2Directory):
                    Properties.Settings.Default.ME2Directory = ME3ExplorerCoreLibSettings.Instance.ME2Directory;
                    break;
                case nameof(ME3ExplorerCoreLibSettings.Instance.ME3Directory):
                    Properties.Settings.Default.ME3Directory = ME3ExplorerCoreLibSettings.Instance.ME3Directory;
                    break;
                default:
                    return;
            }
            Properties.Settings.Default.Save();
        }
    }
}
