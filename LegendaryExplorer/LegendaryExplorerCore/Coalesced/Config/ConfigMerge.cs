using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorerCore.Coalesced.Config
{
    /// <summary>
    /// Class for handling merging config deltas - this code is heavily used by ME3Tweaks Mod Manager and other tools.
    /// </summary>
    public static class ConfigMerge
    {
        // DO NOT CHANGE - THESE MUST BE HERE 
        // SO LE1 CAN ACCURATELY COMPILE A MERGED BUNDLE
        // If you change them you're gonna break a ton of mods
        public const string CONFIG_MERGE_PREFIX = @"ConfigDelta-";
        public const string CONFIG_MERGE_EXTENSION = @".m3cd";

#if DEBUG
        // Change to true to generate log output for config merge.
        private static readonly bool DebugConfigMerge = false;
#else
        private static readonly bool DebugConfigMerge = false;
#endif
        /// <summary>
        /// Splits a delta section name into filename and actual section name in the config file.
        /// </summary>
        /// <param name="deltaSectionName"></param>
        /// <param name="iniSectionName"></param>
        /// <returns></returns>
        private static string GetConfigFileData(string deltaSectionName, out string iniSectionName)
        {
            var result = deltaSectionName.Substring(0, deltaSectionName.IndexOf(@" "));
            iniSectionName = deltaSectionName.Substring(result.Length + 1); // The rest of the string
            return result;
        }

        /// <summary>
        /// Merges the delta into the asset bundle
        /// </summary>
        /// <param name="assetBundle">Bundle to merge into</param>
        /// <param name="delta">Delta of changes to apply</param>
        /// <param name="game"></param>
        public static void PerformMerge(ConfigAssetBundle assetBundle, CoalesceAsset delta)
        {
            foreach (var section in delta.Sections)
            {
                var iniFilename = GetConfigFileData(section.Key, out var iniSectionName);
                var asset = assetBundle.GetAsset(iniFilename);

                // We have the ini file, now we need the section...
                var inisection = asset.GetOrAddSection(iniSectionName);
                foreach (var entry in section.Value.Values)
                {
                    assetBundle.HasChanges |= MergeEntry(inisection, entry, assetBundle.Game);
                }
            }
        }

        /// <summary>
        /// Merges the incoming property into the target section
        /// </summary>
        /// <param name="targetSection"></param>
        /// <param name="incomingProperty"></param>
        /// <param name="game"></param>
        /// <returns></returns>
        internal static bool MergeEntry(CoalesceSection targetSection, CoalesceProperty incomingProperty, MEGame game)
        {
            bool hasChanged = false;

            // Check if this is a double typed property. If it is, we should process it as an addition rather 
            // than as a merge.
            if (applyDoubleTypedItem(targetSection, incomingProperty))
            {
                return true;
            }

            foreach (var prop in incomingProperty)
            {
                switch (prop.ParseAction)
                {
                    case CoalesceParseAction.New: // Type 0 - Overwrite or add
                        {
                            if (targetSection.TryGetValue(incomingProperty.Name, out var values))
                            {
                                values.Clear(); // Remove all existing values on this property.
                                LECLog.Debug($@"ConfigMerge::MergeEntry - Setting value {incomingProperty.Name}->{prop.Value} in {targetSection.Name}", shouldLog: DebugConfigMerge);
                                values.Add(new CoalesceValue(prop.Value, game == MEGame.LE1 ? CoalesceParseAction.Add : prop.ParseAction)); // Add our entry to this property.
                                continue;
                            }

                            // We are just adding the new property itself.
                            // Todo: Double check if we need double typing (++/--/..) for LE2/LE3 so you can run it on existing stuff as well as basedon stuff
                            LECLog.Debug($@"ConfigMerge::MergeEntry - Setting NEW value {incomingProperty.Name}->{prop.Value}", shouldLog: DebugConfigMerge);
                            targetSection.AddEntry(new CoalesceProperty(incomingProperty.Name, new CoalesceValue(prop.Value, game == MEGame.LE1 ? CoalesceParseAction.Add : prop.ParseAction))); // Add our property to the list
                            hasChanged = true;
                        }
                        break;
                    case CoalesceParseAction.Add: // Type 2 - add
                        LECLog.Debug($@"ConfigMerge::MergeEntry - Adding value {incomingProperty.Name}->{prop.Value} to {targetSection.Name}", shouldLog: DebugConfigMerge);
                        targetSection.AddEntry(new CoalesceProperty(incomingProperty.Name, prop.Value)); // Add our property to the list
                        hasChanged = true;
                        break;
                    case CoalesceParseAction.AddUnique:
                        {
                            if (targetSection.TryGetValue(incomingProperty.Name, out var values))
                            {
                                for (int i = values.Count - 1; i >= 0; i--)
                                {
                                    if (values[i].Value == prop.Value)
                                    {
                                        LECLog.Debug($@"ConfigMerge::MergeEntry - Not adding duplicate value {incomingProperty.Name}->{prop.Value} on {targetSection.Name}",
                                            shouldLog: DebugConfigMerge);
                                        continue;
                                    }
                                }
                            }
                            // It's new just add the whole thing or did not find existing one
                            // Todo: LE1 only supports type 2
                            // Todo: Double check if we need double typing (++/--/..) for LE2/LE3 so you can run it on existing stuff as well as basedon stuff
                            LECLog.Debug($@"ConfigMerge::MergeEntry - Adding unique value {incomingProperty.Name}->{prop.Value} to {targetSection.Name}",
                                shouldLog: DebugConfigMerge);
                            targetSection.AddEntry(new CoalesceProperty(incomingProperty.Name, new CoalesceValue(prop.Value, game == MEGame.LE1 ? CoalesceParseAction.Add : prop.ParseAction))); // Add our property to the list
                            hasChanged = true;

                        }
                        break;
                    case CoalesceParseAction.RemoveProperty: // Type 1
                        LECLog.Debug($@"ConfigMerge::MergeEntry - Removing entire property {incomingProperty.Name} from {targetSection.Name}",
                            shouldLog: DebugConfigMerge);

                        targetSection.RemoveAllNamedEntries(incomingProperty.Name);
                        hasChanged = true;
                        break;
                    case CoalesceParseAction.Remove: // Type 4
                        {
                            if (targetSection.TryGetValue(incomingProperty.Name, out var values))
                            {
                                for (int i = values.Count - 1; i >= 0; i--)
                                {
                                    if (values[i].Value == prop.Value)
                                    {
                                        LECLog.Debug($@"ConfigMerge::MergeEntry - Removing value {incomingProperty.Name}->{prop.Value} from {targetSection.Name}",
                                            shouldLog: DebugConfigMerge);
                                        values.RemoveAt(i); // Remove this value
                                        hasChanged = true;
                                    }
                                }
                            }
                        }
                        break;
                    default:
                        LECLog.Warning($@"MERGE TYPE NOT IMPLEMENTED: {prop.ParseAction}");
                        break;
                }
            }

            return hasChanged;
        }

        /// <summary>
        /// Adjusts the typing of the values of this property if it is a double typed property name - e.g. it starts with !/., even after we identified the parsing type.
        /// </summary>
        /// <param name="incomingProperty">Property to account for.</param>
        /// <returns>The same property, but with the value type adjusted and name corrected if it was a double typed property.</returns>
        private static bool applyDoubleTypedItem(CoalesceSection targetSection, CoalesceProperty incomingProperty)
        {
            if (!ConfigFileProxy.IsTyped(incomingProperty.Name) || incomingProperty.Count == 0) return false;

            // Double typed
            var originalType = incomingProperty.First().ParseAction;
            var newType = ConfigFileProxy.GetIniDataType(incomingProperty.Name);
            incomingProperty.Name = ConfigFileProxy.StripType(incomingProperty.Name);

            // Change incoming typings to match the double type value
            for (int i = 0; i < incomingProperty.Count; i++)
            {
                var val = incomingProperty[i];
                val.ValueType = CoalesceValue.GetValueType(newType);
                incomingProperty[i] = val; // Struct assignment
            }

            if (originalType == CoalesceParseAction.Add)
            {
                targetSection.AddEntry(incomingProperty);
            }
            else if (originalType == CoalesceParseAction.AddUnique)
            {
                targetSection.AddEntryIfUnique(incomingProperty);
            }
            else
            {
                LECLog.Warning($@"Double typed config delta has unsupported original typing: {originalType}. Must be Add or AddUnique only.");
            }

            return true;
        }
    }
}
