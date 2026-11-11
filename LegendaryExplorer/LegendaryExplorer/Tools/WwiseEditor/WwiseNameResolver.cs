using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorer.Misc.ExperimentsTools;

namespace LegendaryExplorer.Tools.WwiseEditor
{
    /// <summary>
    /// Resolves Wwise FNV-1 32-bit IDs back to names observed in package files or supplied by the user.
    /// Wwise hashes lowercased ASCII names, so this deliberately uses the same helper as the rest of the
    /// application instead of maintaining a second hash implementation.
    /// </summary>
    public sealed class WwiseNameResolver
    {
        private readonly Dictionary<uint, HashSet<string>> packageNames = new();
        private readonly Dictionary<uint, HashSet<string>> supplementalNames = new();

        /// <summary>
        /// Replaces the names discovered in the currently displayed package. Supplemental names are retained.
        /// </summary>
        public void SetPackageNames(IEnumerable<string> names)
        {
            packageNames.Clear();
            AddNames(packageNames, names);
        }

        /// <summary>
        /// Adds names supplied by the user. These remain available until the Wwise Editor window is closed.
        /// </summary>
        public int AddSupplementalNames(IEnumerable<string> names) => AddNames(supplementalNames, names);

        public bool TryResolve(uint id, out string name)
        {
            var matches = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddMatches(packageNames, id, matches);
            AddMatches(supplementalNames, id, matches);

            name = string.Join(" | ", matches.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            return matches.Count != 0;
        }

        private static int AddNames(Dictionary<uint, HashSet<string>> destination, IEnumerable<string> names)
        {
            int added = 0;
            foreach (string name in names ?? Enumerable.Empty<string>())
            {
                string trimmedName = name?.Trim();
                if (string.IsNullOrEmpty(trimmedName))
                {
                    continue;
                }

                uint id = SharedMethods.CalculateFNV132Hash(trimmedName);
                if (!destination.TryGetValue(id, out HashSet<string> matchingNames))
                {
                    matchingNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    destination.Add(id, matchingNames);
                }

                if (matchingNames.Add(trimmedName))
                {
                    added++;
                }
            }

            return added;
        }

        private static void AddMatches(Dictionary<uint, HashSet<string>> source, uint id, HashSet<string> destination)
        {
            if (source.TryGetValue(id, out HashSet<string> names))
            {
                destination.UnionWith(names);
            }
        }
    }
}
