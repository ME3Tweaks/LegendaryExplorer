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
        /// Given a filename, returns the localization of it.
        /// </summary>
        /// <remarks>Should work on ME1/LE1 files</remarks>
        /// <returns>MELocalization of the file</returns>
        public static MELocalization GetFileLocalizationFromFilePath(this string filePath)
        {
            string localizationName = Path.GetFileNameWithoutExtension(filePath).ToUpper();
            if (localizationName.Length > 8)
            {
                var loc = localizationName.LastIndexOf("LOC_", StringComparison.OrdinalIgnoreCase);
                if (loc > 0)
                {
                    localizationName = localizationName.Substring(loc);
                }
            }
            switch (localizationName)
            {
                case "LOC_DEU":
                case "LOC_DE":
                    //case "LOC_GE": // German text, English audio
                    return MELocalization.DEU;
                case "LOC_ESN":
                    return MELocalization.ESN;
                case "LOC_FRA":
                case "LOC_FR":
                    //case "LOC_FE": French text, English Audio
                    return MELocalization.FRA;
                case "LOC_INT":
                    return MELocalization.INT;
                case "LOC_ITA":
                case "LOC_IT":
                    //case "LOC_IE": // LE1 Italian text, English Audio
                    return MELocalization.ITA;
                case "LOC_JPN":
                case "LOC_JA": //LE1 Japanese text, English Audio
                    return MELocalization.JPN;
                case "LOC_POL":
                case "LOC_PLPC":
                case "LOC_PL":
                    return MELocalization.POL;
                case "LOC_RUS":
                case "LOC_RA":
                    //case "LOC_RU": // LE1 Russian text Russian audio ?
                    return MELocalization.RUS;
                default:
                    return MELocalization.None;
            }
        }
    }
}