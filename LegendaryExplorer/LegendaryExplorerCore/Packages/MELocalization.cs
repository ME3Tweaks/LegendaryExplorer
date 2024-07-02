using System;
using System.IO;

namespace LegendaryExplorerCore.Packages
{
    /// <summary>
    /// Represents the localization of a package file
    /// </summary>
    /// <remarks><see cref="MELocalizationExtensions">Extension methods</see>. This does not work for ME1/LE1 as it uses two character non-int localizations.</remarks>
    public enum MELocalization
    {
        /// <summary>No localization</summary>
        None = 0,
        /// <summary>English</summary>
        INT,
        /// <summary>German</summary>
        DEU,
        /// <summary>Spanish</summary>
        ESN,
        /// <summary>French</summary>
        FRA,
        /// <summary>Italian</summary>
        ITA,
        /// <summary>Japanese</summary>
        JPN,
        /// <summary>Polish</summary>
        POL,
        /// <summary>Russian</summary>
        RUS
    }

    /// <summary>
    /// Extension methods for the <see cref="MELocalization"/> enum
    /// </summary>
    public static class MELocalizationExtensions
    {
        /// <summary>
        /// Returns the suffix used by a localization for "LOC_" file names
        /// </summary>
        /// <remarks>This should only be used for "LOC_" filenames, there are more options for TLK localizations. Does not handle PL suffix</remarks>
        /// <param name="localization">Input localization</param>
        /// <param name="game">Game of localization</param>
        /// <returns>"LOC_" suffix. Example: <c>DEU</c></returns>
        public static string ToLocaleString(this MELocalization localization, MEGame game)
        {
            if (game.IsGame1())
            {
                return localization switch
                {
                    MELocalization.DEU => "DE",
                    MELocalization.ESN => "ES",
                    MELocalization.FRA => "FR",
                    MELocalization.ITA => "IT",
                    MELocalization.POL => "PLPC", // This does not correctly account for PL
                    MELocalization.RUS => "RA",
                    MELocalization.JPN => "JA",
                    MELocalization.None => "",
                    _ => localization.ToString()
                };
            }
            return localization.ToString();
        }

        /// <summary>
        /// Strips the localized suffix off the input string. 
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string StripUnrealLocalization(this string str)
        {
            var localization = str.GetUnrealLocalization();
            if (localization == MELocalization.None) return str;

            // Store where the new string should be written to
            var filenamePos = str.LastIndexOf(Path.GetFileNameWithoutExtension(str), StringComparison.InvariantCultureIgnoreCase);

            // Store the extension, which may be empty.
            var extension = Path.GetExtension(str);

            string localizationName = Path.GetFileNameWithoutExtension(str);
            var locPosTemp = localizationName.LastIndexOf("_", StringComparison.InvariantCultureIgnoreCase); // This will not account for _LOC_ so we will trim that in a second pass.

            // This shouldn't happen, so I'm not going to check for index here if it's not found
            localizationName = localizationName.Substring(0, locPosTemp);

            if (localizationName.EndsWith(@"_LOC", StringComparison.InvariantCultureIgnoreCase))
            {
                // Remove '_LOC_'
                localizationName = localizationName.Substring(0, localizationName.Length - 4);
            }

            // Restore the extension
            localizationName += extension;

            if (filenamePos > 0)
            {
                // prepend the original string (e.g. it's a path)
                return str.Substring(0, filenamePos) + localizationName;
            }
            return localizationName;
        }

        public static string SetUnrealLocalization(this string str, MEGame game, MELocalization loc)
        {
            var ext = Path.GetExtension(str);
            var fileNoExtension = Path.GetFileNameWithoutExtension(str).StripUnrealLocalization();
            var preFilename = str.Substring(0, str.LastIndexOf(fileNoExtension));

            if (loc == MELocalization.None)
                return $"{preFilename}{fileNoExtension}{ext}";

            return $"{preFilename}{fileNoExtension}_{loc.ToLocaleString(game)}{ext}";
        }

        /// <summary>
        /// Attempts to determine the localization of the given string. Localizations end with either LOC_[LANG] or just _[LANG].
        /// </summary>
        /// <param name="str">The string to check against</param>
        /// <returns>The MELocalization enum that corresponds to the matching localization. If none match, <see cref="MELocalization.None"/> is returned</returns>
        public static MELocalization GetUnrealLocalization(this string str)
        {
            string localizationName = Path.GetFileNameWithoutExtension(str).ToUpper();
            //if (localizationName.Length > 8)
            //{
            var loc = localizationName.LastIndexOf("LOC_", StringComparison.OrdinalIgnoreCase);
            if (loc > 0 && loc >= localizationName.Length - 6) // technically this is 7 for ME2/ME3 (ME1 uses 2 letter)
            {
                localizationName = localizationName.Substring(loc);
            }
            else
            {
                loc = localizationName.LastIndexOf("_", StringComparison.OrdinalIgnoreCase);
                if (loc > 0 && localizationName.Length > loc + 1)
                {
                    // End of file might be RA, like Startup_RA, or salarian_ss_FR.pcc
                    localizationName = localizationName.Substring(loc + 1);
                }
            }
            //}

            // Combined basegame startup files don't use the _LOC_ extension.
            // ME1/LE1 files also don't always adhere to this...
            switch (localizationName)
            {
                case "DE":
                case "GE":
                case "DEU":
                case "LOC_DEU":
                case "LOC_DE":
                    return MELocalization.DEU;
                case "ES":
                case "ESN":
                case "LOC_ES":
                case "LOC_ESN":
                    return MELocalization.ESN;
                case "FR":
                case "FE":
                case "FRA":
                case "LOC_FRA":
                case "LOC_FR":
                    return MELocalization.FRA;
                case "INT":
                case "LOC_INT":
                    return MELocalization.INT;
                case "IT":
                case "IE":
                case "ITA":
                case "LOC_ITA":
                case "LOC_IT":
                    return MELocalization.ITA;
                case "JA":
                case "JPN":
                case "LOC_JPN":
                    return MELocalization.JPN;
                case "PL":
                case "POL":
                case "PLPC":
                case "LOC_POL":
                case "LOC_PLPC":
                case "LOC_PL":
                    return MELocalization.POL;
                case "RA":
                case "RU":
                case "RUS":
                case "LOC_RUS":
                case "LOC_RA":
                case "LOC_RU":
                    return MELocalization.RUS;
                default:
                    return MELocalization.None;
            }
        }
    }
}