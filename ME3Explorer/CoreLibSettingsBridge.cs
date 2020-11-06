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
            CoreLibSettings.Instance.ParseUnknownArrayTypesAsObject = Properties.Settings.Default.PropertyParsingUnknownArrayAsObject;
            CoreLibSettings.Instance.TLKDefaultLanguage = Properties.Settings.Default.TLKLanguage;
            CoreLibSettings.Instance.TLKGenderIsMale = Properties.Settings.Default.TLKGender_IsMale;
            CoreLibSettings.Instance.ME1Directory = Properties.Settings.Default.ME1Directory;
            CoreLibSettings.Instance.ME2Directory = Properties.Settings.Default.ME2Directory;
            CoreLibSettings.Instance.ME3Directory = Properties.Settings.Default.ME3Directory;

            Properties.Settings.Default.PropertyChanged += ME3ExpSettingChanged;
            CoreLibSettings.Instance.PropertyChanged += CoreLibSettingChanged;
        }

        private static void ME3ExpSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Properties.Settings.Default.PropertyParsingUnknownArrayAsObject):
                    CoreLibSettings.Instance.ParseUnknownArrayTypesAsObject = Properties.Settings.Default.PropertyParsingUnknownArrayAsObject;
                    break;
                case nameof(Properties.Settings.Default.TLKLanguage):
                    CoreLibSettings.Instance.TLKDefaultLanguage = Properties.Settings.Default.TLKLanguage;
                    break;
                case nameof(Properties.Settings.Default.TLKGender_IsMale):
                    CoreLibSettings.Instance.TLKGenderIsMale = Properties.Settings.Default.TLKGender_IsMale;
                    break;
                case nameof(Properties.Settings.Default.ME1Directory):
                    CoreLibSettings.Instance.ME1Directory = Properties.Settings.Default.ME1Directory;
                    break;
                case nameof(Properties.Settings.Default.ME2Directory):
                    CoreLibSettings.Instance.ME2Directory = Properties.Settings.Default.ME2Directory;
                    break;
                case nameof(Properties.Settings.Default.ME3Directory):
                    CoreLibSettings.Instance.ME3Directory = Properties.Settings.Default.ME3Directory;
                    break;
            }
        }

        private static void CoreLibSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CoreLibSettings.Instance.ParseUnknownArrayTypesAsObject):
                    Properties.Settings.Default.PropertyParsingUnknownArrayAsObject = CoreLibSettings.Instance.ParseUnknownArrayTypesAsObject;
                    break;
                case nameof(CoreLibSettings.Instance.TLKDefaultLanguage):
                    Properties.Settings.Default.TLKLanguage = CoreLibSettings.Instance.TLKDefaultLanguage;
                    break;
                case nameof(CoreLibSettings.Instance.TLKGenderIsMale):
                    Properties.Settings.Default.TLKGender_IsMale = CoreLibSettings.Instance.TLKGenderIsMale;
                    break;
                case nameof(CoreLibSettings.Instance.ME1Directory):
                    Properties.Settings.Default.ME1Directory = CoreLibSettings.Instance.ME1Directory;
                    break;
                case nameof(CoreLibSettings.Instance.ME2Directory):
                    Properties.Settings.Default.ME2Directory = CoreLibSettings.Instance.ME2Directory;
                    break;
                case nameof(CoreLibSettings.Instance.ME3Directory):
                    Properties.Settings.Default.ME3Directory = CoreLibSettings.Instance.ME3Directory;
                    break;
                default:
                    return;
            }
            Properties.Settings.Default.Save();
        }
    }
}
