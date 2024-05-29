using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Collections.ObjectModel;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    /// <summary>
    /// Matches an asset on an input predicate
    /// </summary>
    /// <typeparam name="T"></typeparam>
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

    /// <summary>
    /// Matches an asset based on the current search text via an input predicate
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class SearchSpecification<T> : AssetSpecification<T>
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
    /// Matches an asset based on whether any of it's usages are in the Custom File List
    /// </summary>
    public class FileListSpecification : AssetSpecification<IAssetRecord>
    {
        public new bool ShowInUI { get; } = false;
        public ObservableDictionary<int, string> CustomFileList { get; set; } = new();

        public override bool MatchesSpecification(IAssetRecord item)
        {
            if (!CustomFileList.IsEmpty())
            {
                return item.AssetUsages.Select(usage => usage.FileKey).Intersect(CustomFileList.Keys).Any();
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

        public bool IsSelected
        {
            get => false;
            set { }
        }

        public bool ShowInUI { get; } = true;

        public bool MatchesSpecification(T item)
        {
            return true;
        }
    }

    /// <summary>
    /// Specification that always returns true, cannot be selected, and invokes an action when you attempt to select it.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ActionSpecification<T> : AssetSpecification<T>
    {
        // This class feels kinda like an abuse of the filter system, it only implements this interface so it can be bound in the UI without extra work. Meh.
        private readonly Action _onSelection;
        public override bool IsSelected
        {
            get => false;
            set => _onSelection?.Invoke();
        }

        public ActionSpecification(string name, Action actionOnSelection, string description = null)
        {
            FilterName = name;
            Description = description;
            _onSelection = actionOnSelection;
        }

        public override bool MatchesSpecification(T item) => true;
    }
}