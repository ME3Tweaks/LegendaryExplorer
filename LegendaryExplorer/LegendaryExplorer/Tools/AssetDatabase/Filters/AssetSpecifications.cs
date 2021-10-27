using LegendaryExplorer.Misc;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public interface IAssetSpecification<in T>
    {
        string FilterName { get; set; }
        string Description { get; set; }
        bool IsSelected { get; set; }
        bool MatchesSpecification(T item);
    }

    /// <summary>
    /// Base class for all filters, implementing INotifyPropertyChanged
    /// </summary>
    /// <typeparam name="T">The type of asset being filtered</typeparam>
    public abstract class AssetSpecification<T> : NotifyPropertyChangedBase, IAssetSpecification<T>
    {
        private string _filterName;
        public string FilterName { get => _filterName; set => SetProperty(ref _filterName, value); }
        public string Description { get; set; }
        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        public AssetSpecification() { }
        public abstract bool MatchesSpecification(T item);
    }

    public abstract class AnimationSpecification : AssetSpecification<AnimationRecord>
    {
        public abstract override bool MatchesSpecification(AnimationRecord item);
    }

    public abstract class ClassSpecification : AssetSpecification<ClassRecord>
    {
        public abstract override bool MatchesSpecification(ClassRecord item);
    }

    public abstract class MaterialSpecification : AssetSpecification<MaterialRecord>
    {
        public abstract override bool MatchesSpecification(MaterialRecord item);
    }

    public abstract class MeshSpecification : AssetSpecification<MeshRecord>
    {
        public abstract override bool MatchesSpecification(MeshRecord item);
    }

    public abstract class TextureSpecification : AssetSpecification<TextureRecord>
    {
        public abstract override bool MatchesSpecification(TextureRecord item);
    }

    public abstract class ParticleSysSpecification : AssetSpecification<ParticleSysRecord>
    {
        public abstract override bool MatchesSpecification(ParticleSysRecord item);
    }
}