using System;
using System.IO;
using System.Reflection;

namespace LegendaryExplorer.Misc
{
    public static class AppVersion
    {
        /// <summary>
        /// Displayed version in the UI. About page will be more detailed.
        /// </summary>
        public static string DisplayedVersion
        {
            get
            {
                Version assemblyVersion = Assembly.GetEntryAssembly().GetName().Version;
                string version = $"{assemblyVersion.Major}.{assemblyVersion.Minor}";
                if (assemblyVersion.Build != 0)
                {
                    version += "." + assemblyVersion.Build;
                }
                
#if DEBUG
                version += " DEBUG";
#elif NIGHTLY
                //This is what will be placed in release. Comment this out when building for a stable!
                version += " NIGHTLY"; //ENSURE THIS IS CHANGED FOR MAJOR RELEASES AND RELEASE CANDIDATES
#elif RELEASE
                // UPDATE THIS FOR RELEASE
                //version += " RC";
#endif
                return $"{version} {App.BuildDateTime.ToShortDateString()}";
            }
        }

        /// <summary>
        /// Full displayed version in the UI for about page
        /// </summary>
        public static string FullDisplayedVersion
        {
            get
            {
                Version ver = Assembly.GetExecutingAssembly().GetName().Version;
                return "v" + ver.Major + "." + ver.Minor + "." + ver.Build + "." + ver.Revision;
            }
        }
    }
}
