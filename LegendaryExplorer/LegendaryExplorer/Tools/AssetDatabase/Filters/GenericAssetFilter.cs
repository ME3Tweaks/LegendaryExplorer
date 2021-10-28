using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public interface IAssetFilter<in T>
    {
        public bool Filter(T record);
    }

    /// <summary>
    /// A generic class to filter assets based on a number of enabled Specifications
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class GenericAssetFilter<T> : IAssetFilter<T>
    {
        public List<IAssetSpecification<T>> Filters { get; set; } = new();

        protected GenericAssetFilter() { }

        public GenericAssetFilter(IEnumerable<IAssetSpecification<T>> specs)
        {
            Filters = specs.ToList();
        }

        public virtual bool Filter(T record)
        {
            return Filters.Where(s => s.IsSelected).All(spec => spec.MatchesSpecification(record));
        }

        public virtual void SetSelected(IAssetSpecification<T> spec)
        {
            spec.IsSelected = !spec.IsSelected;
        }
    }

    /// <summary>
    /// A class to filter assets based on a single specification. Only one of the input specs can be enabled at a time
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class SingleOptionFilter<T> : GenericAssetFilter<T>
    {
        public SingleOptionFilter(IEnumerable<IAssetSpecification<T>> specs) : base(specs) { }

        public override void SetSelected(IAssetSpecification<T> spec)
        {
            if (!spec.IsSelected)
            {
                foreach (var fSpec in Filters) fSpec.IsSelected = false;
            }
            spec.IsSelected = !spec.IsSelected;
        }
    }
}