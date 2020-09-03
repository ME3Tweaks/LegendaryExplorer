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
            }
        }

        private static void CoreLibSettingChanged(object sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(CoreLibSettings.Instance.ParseUnknownArrayTypesAsObject):
                    Properties.Settings.Default.PropertyParsingUnknownArrayAsObject = CoreLibSettings.Instance.ParseUnknownArrayTypesAsObject;
                    Properties.Settings.Default.Save();
                    break;
                case nameof(CoreLibSettings.Instance.TLKDefaultLanguage):
                    Properties.Settings.Default.TLKLanguage = CoreLibSettings.Instance.TLKDefaultLanguage;
                    Properties.Settings.Default.Save();
                    break;
                case nameof(CoreLibSettings.Instance.TLKGenderIsMale):
                    Properties.Settings.Default.TLKGender_IsMale = CoreLibSettings.Instance.TLKGenderIsMale;
                    Properties.Settings.Default.Save();
                    break;
            }
        }
    }
}
