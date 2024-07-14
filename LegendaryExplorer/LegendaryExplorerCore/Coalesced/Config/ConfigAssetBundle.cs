using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Localization;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;

namespace LegendaryExplorerCore.Coalesced.Config
{
    /// <summary>
    /// Handler for a bundle of config assets
    /// </summary>
    [DebuggerDisplay(@"ConfigAssetBundle for {DebugFileName}")]
    public class ConfigAssetBundle
    {
        /// <summary>
        /// The assets the make up DLC's config files
        /// </summary>
        private CaseInsensitiveDictionary<CoalesceAsset> Assets = new();

        /// <summary>
        /// Game this bundle is for
        /// </summary>
        public MEGame Game { get; private set; }

        /// <summary>
        /// If this bundle has pending changes that have not yet been committed
        /// </summary>
        public bool HasChanges { get; set; }

        private string DLCFolderName;
        private string CookedDir;

        /// <summary>
        /// Generates a ConfigAssetBundle from the specified single-file stream (.bin)
        /// </summary>
        /// <param name="game"></param>
        /// <param name="stream"></param>
        /// <param name="filename"></param>
        /// <exception cref="Exception"></exception>
        private ConfigAssetBundle(MEGame game, Stream stream, string filename = null)
        {
#if DEBUG
            DebugFileName = filename;
#endif
            Game = game;
            if (Game is MEGame.LE1 or MEGame.LE2)
            {
                Assets = CoalescedConverter.DecompileLE1LE2ToAssets(stream, filename ?? @"Coalesced_INT.bin", stripExtensions: true);
            }
            else if (Game == MEGame.LE3)
            {
                Assets = CoalescedConverter.DecompileGame3ToAssets(stream, filename ?? @"Coalesced.bin", stripExtensions: true);
            }
            else
            {
                throw new Exception(LECLocalizationShim.GetString(LECLocalizationShim.string_interp_XDoesNotSupportGameY, nameof(ConfigAssetBundle), game));
            }
        }
#if DEBUG
        public string DebugFileName { get; set; }
#endif

        /// <summary>
        /// Generate a ConfigAssetBundle from the specified single packed file stream (.bin)
        /// </summary>
        /// <param name="game"></param>
        /// <param name="stream"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static ConfigAssetBundle FromSingleStream(MEGame game, Stream stream, string fileName = null)
        {
            return new ConfigAssetBundle(game, stream, fileName);
        }

        /// <summary>
        /// Gets an enumerator that returns all asset names.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> GetAssetNames()
        {
            return Assets.Keys;
        }

        /// <summary>
        /// Generate a ConfigAssetBundle from the specified single packed file (.bin)
        /// </summary>
        /// <param name="game"></param>
        /// <param name="singleFile"></param>
        /// <exception cref="Exception"></exception>
        public static ConfigAssetBundle FromSingleFile(MEGame game, string singleFile)
        {
            using var stream = File.OpenRead(singleFile);
            return new ConfigAssetBundle(game, stream, Path.GetFileName(singleFile));
        }

        /// <summary>
        /// Generates a bundle object based on the CookedPCConsole folder specified
        /// </summary>
        /// <param name="game">What game this bundle is for</param>
        /// <param name="cookedDir">The full path to the cooked directory</param>
        /// <param name="dlcFolderName">The name of the DLC folder, e.g. DLC_MOD_XXX</param>
        /// <returns>Asset bundle object if the bundle was loaded; if the bundle failed to load, it returns null instead</returns>
        public static ConfigAssetBundle FromDLCFolder(MEGame game, string cookedDir, string dlcFolderName)
        {
            try
            {
                var assetBundle = new ConfigAssetBundle(game, cookedDir, dlcFolderName);
                return assetBundle.Assets != null ? assetBundle : null;
            }
            catch (Exception e)
            {
                LECLog.Exception(e, $@"Exception building asset bundle from DLC folder {game} {cookedDir} {dlcFolderName}:");
                return null;
            }
        }

        /// <summary>
        /// Generates a bundle object based on the CookedPCConsole folder specified
        /// </summary>
        /// <param name="game">What game this bundle is for</param>
        /// <param name="cookedDir">The full path to the cooked directory</param>
        /// <param name="dlcFolderName">The name of the DLC folder, e.g. DLC_MOD_XXX</param>
        private ConfigAssetBundle(MEGame game, string cookedDir, string dlcFolderName)
        {
            Game = game;
            CookedDir = cookedDir;
            DLCFolderName = dlcFolderName;

#if DEBUG
            DebugFileName = dlcFolderName;
#endif
            if (game == MEGame.LE1)
            {
                // Load any M3CD files and merge them together to produce the final results DLC configuration bundle.
                var m3cds = Directory.GetFiles(cookedDir, @"*" + ConfigMerge.CONFIG_MERGE_EXTENSION,
                        SearchOption.TopDirectoryOnly)
                    .Where(x => Path.GetFileName(x).StartsWith(ConfigMerge.CONFIG_MERGE_PREFIX))
                    .ToList(); // Find CoalescedMerge-*.m3cd files
                
                foreach (var m3cd in m3cds)
                {
                    LECLog.Information($@"Merging M3 Config Delta {m3cd} in {dlcFolderName}");
                    var m3cdasset = ConfigFileProxy.LoadIni(m3cd);
                    ConfigMerge.PerformMerge(this, m3cdasset);
                }
            }
            else if (game.IsGame2())
            {
                var iniFiles = Directory.GetFiles(cookedDir, @"*.ini", SearchOption.TopDirectoryOnly);
                foreach (var ini in iniFiles)
                {
                    var fname = Path.GetFileNameWithoutExtension(ini);
                    if (!CoalescedConverter.ProperNames.Contains(fname, StringComparer.OrdinalIgnoreCase))
                        continue; // Not supported. localization files are only supported in the main single file.
                    Assets[fname] = ConfigFileProxy.LoadIni(ini);
                }
            }
            else if (game.IsGame3())
            {
                var coalFile = Path.Combine(cookedDir, $@"Default_{dlcFolderName}.bin");
                if (File.Exists(coalFile))
                {
                    Assets = CoalescedConverter.DecompileGame3ToAssets(coalFile, stripExtensions: true);
                }
                else
                {
                    LECLog.Error($@"{game} config file does not exist: {coalFile}, using blank assets");
                }
            }
            else
            {
                throw new Exception(LECLocalizationShim.GetString(LECLocalizationShim.string_interp_XDoesNotSupportGameY, nameof(ConfigAssetBundle), game));
            }
        }

        public CoalesceAsset GetAsset(string assetName, bool createIfNotFound = true)
        {
            var asset = Path.GetFileNameWithoutExtension(assetName);
            if (Assets.TryGetValue(asset, out var result))
                return result;

            if (createIfNotFound)
            {
                Assets[asset] = new CoalesceAsset($@"{asset}.ini"); // Even game 3 uses .ini, I think...
                return Assets[asset];
            }

            return null;
        }

        /// <summary>
        /// Commits this bundle to the specified single config file
        /// </summary>
        public void CommitAssets(string outPath, MELocalization loc)
        {
            if (Game is MEGame.LE1 or MEGame.LE2)
            {
                // Combine the assets
                var inis = new CaseInsensitiveDictionary<DuplicatingIni>();
                foreach (var asset in Assets)
                {
                    inis[asset.Key] = CoalesceAsset.ToIni(asset.Value);
                }

                var compiledStream = CoalescedConverter.CompileLE1LE2FromMemory(inis, loc);
                compiledStream.WriteToFile(outPath);
                HasChanges = false;
            }
            else if (Game.IsGame3())
            {
                // This is kind of a hack, but it works.
                var compiled = CoalescedConverter.CompileFromMemory(Assets.ToDictionary(x => x.Key, x => x.Value.ToXmlString()));
                compiled.WriteToFile(outPath);
                HasChanges = false;
            }
            else
            {
                LECLog.Error($@"Unsupported game for single-file config merge: {Game}");
            }
        }

        /// <summary>
        /// Commits this bundle to the same folder it was loaded from
        /// </summary>
        public void CommitDLCAssets(string outPath = null)
        {
            if (Game.IsGame2())
            {
                foreach (var v in Assets)
                {
                    var outFile = Path.Combine(outPath ?? CookedDir, Path.GetFileNameWithoutExtension(v.Key) + @".ini");
                    File.WriteAllText(outFile, v.Value.GetGame2IniText());
                }
                HasChanges = false;
            }
            else if (Game.IsGame3())
            {
                var coalFile = Path.Combine(outPath ?? CookedDir, $@"Default_{DLCFolderName}.bin");
                CommitAssets(coalFile, MELocalization.INT); // DLC does not support localization files as part of config.
                HasChanges = false;
            }
        }

        /// <summary>
        /// Merges this bundle into the specified one, applying the changes.
        /// </summary>
        /// <param name="destBundle"></param>
        public void MergeInto(ConfigAssetBundle destBundle)
        {
            foreach (var myAsset in Assets)
            {
                var matchingDestAsset = destBundle.GetAsset(myAsset.Key);
                foreach (var mySection in myAsset.Value.Sections)
                {
                    var matchingDestSection = matchingDestAsset.GetOrAddSection(mySection.Key);
                    foreach (var entry in mySection.Value)
                    {
                        ConfigMerge.MergeEntry(matchingDestSection, entry.Value, destBundle.Game);
                    }
                }
            }
        }
    }
}
