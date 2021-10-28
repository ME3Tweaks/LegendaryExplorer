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

        public bool ShowInUI { get; init; } = true;

        public abstract bool MatchesSpecification(T item);
    }

    public abstract class MaterialSpecification : AssetSpecification<MaterialRecord>
    {
        public abstract override bool MatchesSpecification(MaterialRecord item);
    }

    public class PredicateSpecification<T> : AssetSpecification<T>
    {
        private readonly Predicate<T> _predicate;

        public PredicateSpecification(string filterName, Predicate<T> predicate, string description = null)
        {
            FilterName = filterName;
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            Description = description;
        }

        public override bool MatchesSpecification(T item)
        {
            return _predicate?.Invoke(item) ?? true;
        }
    }

    public class SearchSpecification<T> : AssetSpecification<T>
    {
        public string SearchText { get; set; }
        private readonly Predicate<(string, T)> _predicate;

        public SearchSpecification(Predicate<(string, T)> predicate)
        {
            _predicate = predicate;
            ShowInUI = false;
            IsSelected = true;
        }

        public override bool MatchesSpecification(T item)
        {
            if (!string.IsNullOrEmpty(SearchText))
            {
                return _predicate?.Invoke((SearchText, item)) ?? true;
            }
            return true;
        }
    }

    /// <summary>
    /// Specification that returns true if any of the input specifications are true
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class OrSpecification<T> : IAssetSpecification<T>
    {
        public string FilterName { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsSelected { get; set; }
        public bool ShowInUI { get; } = false;

        private readonly IEnumerable<IAssetSpecification<T>> _specifications;

        public OrSpecification(params IAssetSpecification<T>[] specs)
        {
            _specifications = specs;
            IsSelected = _specifications.Any();
        }

        public OrSpecification(IEnumerable<IAssetSpecification<T>> specs)
        {
            _specifications = specs;
            IsSelected = _specifications.Any();
        }

        public bool MatchesSpecification(T item)
        {
            var selectedSpecs = _specifications.Where(sp => sp.IsSelected).ToList();
            if (!Enumerable.Any(selectedSpecs) || IsSelected == false) return true; // Fallthrough, accept all items
            else return selectedSpecs.Any(sp => sp.MatchesSpecification(item));
        }
    }

    /// <summary>
    /// Used to represent a Separator MenuItem when bound in the UI, and should have no behavior whatsoever
    /// </summary>
    public class UISeparator<T> : IAssetSpecification<T>
    {
        public string FilterName { get; set; } = "separator";
        public string Description { get; set; }
        public bool IsSelected { get => false; set { } }
        public bool ShowInUI { get; } = true;

        public bool MatchesSpecification(T item)
        {
            return true;
        }
    }

    public class FileListSpecification : AssetSpecification<object>
    {
        public new bool ShowInUI { get; } = false;
        public ObservableDictionary<int, string> CustomFileList { get; set; } = new();
        public override bool MatchesSpecification(object item)
        {
            if (item is IAssetRecord record && !CustomFileList.IsEmpty())
            {
                return record.AssetUsages.Select(usage => usage.FileKey).Intersect(CustomFileList.Keys).Any();
            }
            return true;
        }
    }
}