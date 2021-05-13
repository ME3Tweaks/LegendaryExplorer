using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryExplorer.Misc.AppSettings
{
    /// <summary>
    /// Class that contains static-bindable settings.
    /// </summary>
    public static partial class Settings
    {
        // This file contains the main interaction code and is NOT pregenerated

        #region Static Property Changed

        private static bool Loaded = false;
        public static event PropertyChangedEventHandler StaticPropertyChanged;

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        private static bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
            if (Loaded)
            {
                LogSettingChanging(propertyName, value);
                Save();
            }
            return true;
        }

        private static void LogSettingChanging(string propertyName, object value)
        {
            if (Loaded)
                Debug.WriteLine($@"Setting changing: {propertyName} -> {value}");
        }

        #endregion

        // Do not add settings to this class. Add them to the SettingsBuilder.tt file
    }
}
