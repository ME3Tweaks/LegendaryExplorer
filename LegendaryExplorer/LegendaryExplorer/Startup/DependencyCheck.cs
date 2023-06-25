using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using LegendaryExplorer.Misc;
using Microsoft.AppCenter.Analytics;
using Microsoft.Win32;

namespace LegendaryExplorer.Startup
{
    internal class DependencyCheck
    {
        public static void CheckDependencies(Window window)
        {
            // VC++ 2013 x64
            // Registry system seems too cumbersome for this version of vc++
            var vcDll = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "msvcp120.dll");
            if (!File.Exists(vcDll))
            {
                // Dependency not met
                MessageBox.Show(window,
                    "The Visual C++ 2013 x64 Distributable is not installed, which is required for some features to work. Clicking OK will open the download page for this dependency.",
                    "Missing dependency", MessageBoxButton.OK, MessageBoxImage.Information);
                HyperlinkExtensions.OpenURL("https://aka.ms/highdpimfc2013x64enu");
                // We could have it download and run as it doesn't prompt for admin until it you press install.
                // We could also have it automatically attempt install but it'd prompt for admin.
                Analytics.TrackEvent("Opened VC++ download page");
            }
        }
    }
}
