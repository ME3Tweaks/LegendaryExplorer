using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Gammtek.Collections.ObjectModel;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public interface IAssetSpecification<in T>
    {
        string FilterName { get; set; }
        string Description { get; set; }
        bool IsSelected { get; set; }
        bool ShowInUI { get; }
        bool MatchesSpecification(T item);
    }

    /// <summary>
    /// Base class for all specification, implementing INotifyPropertyChanged
    /// </summary>
    /// <typeparam name="T">The type of asset being filtered</typeparam>
    public abstract class AssetSpecification<T> : NotifyPropertyChangedBase, IAssetSpecification<T>
    {
        private string _filterName;
        public string FilterName { get => _filterName; set => SetProperty(ref _filterName, value); }
        private string _description;
        public string Description { get => _description; set => SetProperty(ref _description, value); }
        private bool _isSelected;
        public virtual bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

        /// <summary>
        /// Should this Specification show up in the UI?
        /// </summary>
        public bool ShowInUI { get; init; } = true;

        public abstract bool MatchesSpecification(T item);
    }
}