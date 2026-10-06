using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorer.Tools.WwiseEditor
{
    /// <summary>
    /// A resolved WwiseEvent export and the IDs by which its graph node may be referenced. The export's stored ID
    /// and the hash of its object name are both retained because some packages have missing or stale ID properties.
    /// </summary>
    public sealed class WwiseEventLookup
    {
        public string Name { get; }
        public IReadOnlyList<uint> IDs { get; }
        public string DisplayName => Name;

        public WwiseEventLookup(string name, IEnumerable<uint> ids)
        {
            Name = name;
            IDs = ids.Where(id => id != 0).Distinct().ToList();
        }

        public bool Matches(uint id) => IDs.Contains(id);
    }
}
