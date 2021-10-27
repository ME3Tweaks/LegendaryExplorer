using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public class GenericAssetFilter<T> : IAssetFilter<T>
    {
        public List<IAssetSpecification<T>> Filters { get; set; } = new();

        protected GenericAssetFilter()
        {
        }

        public GenericAssetFilter(IEnumerable<IAssetSpecification<T>> specs)
        {
            Filters = specs.ToList();
        }

        public virtual bool Filter(T record)
        {
            return Filters.Where(s => s.IsSelected).All(spec => spec.MatchesSpecification(record));
        }

        private void ToggleFilter(object obj)
        {
            if (obj is IAssetSpecification<T> spec)
            {
                SetSelected(spec);
            }
        }

        public virtual void SetSelected(IAssetSpecification<T> spec)
        {
            spec.IsSelected = !spec.IsSelected;
        }
    }
}