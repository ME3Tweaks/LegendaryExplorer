using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public class TextureFilter : GenericAssetFilter<TextureRecord>
    {
        public ObservableCollection<IAssetSpecification<TextureRecord>> LODGroups { get; } = new();
        private List<PredicateSpecification<TextureRecord>> GeneratedLodFilters;
        private List<PredicateSpecification<TextureRecord>> SingleSelectionSizes;

        private readonly string[] _lodGroups = new[]
        {
            "World", "WorldSpecular", "WorldNormalMap", "AmbientLightMap", "Shadowmap",
            "Environment64", "Environment128", "Environment256", "Environment512", "Environment1024",
            "VFX64", "VFX128", "VFX256", "VFX512", "VFX1024",
            "APL64", "APL128", "APL256", "APL512", "APL1024",
            "UI", "Promotional", "Character1024", "CharacterDiff", "CharacterNorm", "CharacterSpec",
            "n/a"
        };

        public TextureFilter(FileListSpecification fileList)
        {
            Search = new SearchSpecification<TextureRecord>(TextureSearch);
            Filters = new List<IAssetSpecification<TextureRecord>>
            {
                fileList,
                new PredicateSpecification<TextureRecord>("Only TextureCubes", tr => tr.CFormat == "TextureCube"),
                new PredicateSpecification<TextureRecord>("Only TextureMovies", tr => tr.CFormat == "TextureMovie"),
                new UISeparator<TextureRecord>()
            };

            SingleSelectionSizes = new List<PredicateSpecification<TextureRecord>>()
            {
                new("Only Textures 1024 or larger", tr => tr.SizeX >= 1024 || tr.SizeY >= 1024),
                new("Only Textures 4096 or larger", tr => tr.SizeX >= 4096 || tr.SizeY >= 4096)
            };
            Filters.AddRange(SingleSelectionSizes);
            SetupLodGroups();
        }

        private void SetupLodGroups()
        {
            GeneratedLodFilters = _lodGroups
                .Select(g => new PredicateSpecification<TextureRecord>(g, tr => tr.TexGrp == g)).ToList();

            LODGroups.AddRange(GeneratedLodFilters);
            LODGroups.Add(new UISeparator<TextureRecord>());
            LODGroups.Add(new ActionSpecification<TextureRecord>("Show all", () =>
            {
                foreach (var spec in GeneratedLodFilters) spec.IsSelected = true;
            }));
            LODGroups.Add(new ActionSpecification<TextureRecord>("Clear all", () =>
            {
                foreach (var spec in GeneratedLodFilters) spec.IsSelected = false;
            }));
        }

        public override void SetSelected(IAssetSpecification<TextureRecord> spec)
        {
            if (spec is PredicateSpecification<TextureRecord> ps && SingleSelectionSizes.Contains(ps) && !ps.IsSelected)
            {
                foreach (var size in SingleSelectionSizes) size.IsSelected = false;
            }
            base.SetSelected(spec);
        }

        protected override IEnumerable<IAssetSpecification<TextureRecord>> GetAdditionalSpecifications()
        {
            return new[] {new OrSpecification<TextureRecord>(GeneratedLodFilters)};
        }

        private bool TextureSearch((string search, TextureRecord record) t)
        {
            var (text, tr) = t;
            text = text.ToLower();
            bool showThis = tr.TextureName.ToLower().Contains(text) || tr.CRC.ToLower().Contains(text) || tr.ParentPackage.ToLower().Contains(text);

            if (!showThis && text.StartsWith("size: ") && text.Contains("x") && text.Length > 6)
            {
                var sr = text.Remove(0, 6).ToLower().Split("x");
                if (int.TryParse(sr[0], out int xVal) && int.TryParse(sr[1], out int yVal))
                {
                    showThis = tr.SizeX == xVal && tr.SizeY == yVal;
                }
            }
            return showThis;
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

        public ActionSpecification(string name, Action actionOnSelection, string description = "")
        {
            FilterName = name;
            Description = description;
            _onSelection = actionOnSelection;
        }

        public override bool MatchesSpecification(T item) => true;
    }
}