using System;
using System.Collections.Generic;
using LegendaryExplorer.SharedUI;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public interface IAssetFilter<in T>
    {
        public bool Filter(T record);
    }

    class TextureFilter : GenericAssetFilter<TextureRecord>
    {
    }

    public class SingleOptionFilter<T> : GenericAssetFilter<T>
    {
        public SingleOptionFilter(IEnumerable<IAssetSpecification<T>> specs) : base(specs)
        {

        }
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