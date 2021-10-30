using System;
using System.Collections.Generic;
using System.Linq;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    /// <summary>
    /// A class to filter assets based on a number of enabled specifications
    /// </summary>
    /// <typeparam name="T">Record type to filter</typeparam>
    public class GenericAssetFilter<T>
    {
        public List<IAssetSpecification<T>> Filters { get; set; } = new();
        public SearchSpecification<T> Search { get; set; }

        protected GenericAssetFilter() { }

        /// <summary>
        /// Creates a filter from a number of specifications
        /// </summary>
        /// <param name="specs">Specifications that should apply to this record type</param>
        /// <param name="searchPredicate">Lambda for how the searchbox should operate on this record type</param>
        public GenericAssetFilter(IEnumerable<IAssetSpecification<T>> specs,
            Predicate<(string SearchText, T Record)> searchPredicate = null)
        {
            Filters = specs.ToList();
            Search = new SearchSpecification<T>(searchPredicate);
        }

        /// <summary>
        /// Returns whether this record matches all enabled filters.
        /// </summary>
        /// <param name="obj">Record to filter</param>
        /// <returns></returns>
        public virtual bool Filter(object obj)
        {
            if (obj is T record)
            {
                return GetSpecifications().Where(s => s.IsSelected || !s.ShowInUI).All(spec => spec.MatchesSpecification(record));
            }
            return false;
        }

        /// <summary>
        /// Get the specifications that should be used in the filter. Specifications that are not selected can be safely returned by this method.
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerable<IAssetSpecification<T>> GetSpecifications()
        {
            return Filters.Append(Search);
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
        public SingleOptionFilter(IEnumerable<IAssetSpecification<T>> specs, Predicate<(string SearchText, T Record)> searchPredicate = null)
            : base(specs, searchPredicate) { }

        public override void SetSelected(IAssetSpecification<T> spec)
        {
            if (!spec.IsSelected)
            {
                foreach (var fSpec in Filters) fSpec.IsSelected = false;
            }
            base.SetSelected(spec);
        }
    }
}