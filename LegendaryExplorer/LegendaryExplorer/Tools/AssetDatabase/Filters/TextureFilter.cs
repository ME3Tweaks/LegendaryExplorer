using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public class TextureFilter : GenericAssetFilter<TextureRecord>
    {
        public ObservableCollection<IAssetSpecification<TextureRecord>> LODGroups { get; }
        private readonly List<PredicateSpecification<TextureRecord>> GeneratedLodFilters;
        private readonly List<PredicateSpecification<TextureRecord>> SingleSelectionSizes;
        private readonly List<PredicateSpecification<TextureRecord>> TextureTypes;

        private readonly string[] _lodGroups =
        [
            "World", "WorldSpecular", "WorldNormalMap", "AmbientLightMap", "Shadowmap",
            "Environment64", "Environment128", "Environment256", "Environment512", "Environment1024",
            "VFX64", "VFX128", "VFX256", "VFX512", "VFX1024",
            "APL64", "APL128", "APL256", "APL512", "APL1024",
            "UI", "Promotional", "Character1024", "CharacterDiff", "CharacterNorm", "CharacterSpec",
            "n/a"
        ];

        public TextureFilter(FileListSpecification fileList)
        {
            Search = new SearchSpecification<TextureRecord>(TextureSearch);
            TextureTypes = 
            [ 
                new PredicateSpecification<TextureRecord>("Only TextureCubes", tr => tr.CFormat == "TextureCube"), 
                new PredicateSpecification<TextureRecord>("Only TextureMovies", tr => tr.CFormat == "TextureMovie")
            ];
            SingleSelectionSizes =
            [
                new("Only Textures 1024 or larger", tr => tr.SizeX >= 1024 || tr.SizeY >= 1024),
                new("Only Textures 4096 or larger", tr => tr.SizeX >= 4096 || tr.SizeY >= 4096)
            ];

            Filters =
            [
                fileList,
                .. TextureTypes,
                new UISeparator<TextureRecord>(),
                .. SingleSelectionSizes,
            ];

            GeneratedLodFilters = [.. _lodGroups.Select(g => new PredicateSpecification<TextureRecord>(g, tr => tr.TexGrp == g))];
            LODGroups =
            [
                .. GeneratedLodFilters,
                new UISeparator<TextureRecord>(),
                new ActionSpecification<TextureRecord>("Show all", () =>
                {
                    foreach (var spec in GeneratedLodFilters) spec.IsSelected = true;
                }),
                new ActionSpecification<TextureRecord>("Clear all", () =>
                {
                    foreach (var spec in GeneratedLodFilters) spec.IsSelected = false;
                }),
            ];
            UpdateFilterCache();
        }

        public override void SetSelected(IAssetSpecification<TextureRecord> spec)
        {
            if (spec is PredicateSpecification<TextureRecord> ps)
            {
                if (SingleSelectionSizes.Contains(ps) && !ps.IsSelected)
                {
                    foreach (var size in SingleSelectionSizes) size.IsSelected = false;
                }
                else if (TextureTypes.Contains(ps) && !ps.IsSelected)
                {
                    foreach (var size in TextureTypes) size.IsSelected = false;
                }
            }

            base.SetSelected(spec);
        }

        protected override IEnumerable<IAssetSpecification<TextureRecord>> GetAdditionalSpecifications()
        {
            return [new OrSpecification<TextureRecord>(GeneratedLodFilters)];
        }

        public static bool TextureSearch((string search, TextureRecord record) t)
        {
            var (text, tr) = t;
            text = text.ToLower();
            bool showThis = tr.TextureName.ToLower().Contains(text) || tr.CRC.ToLower().Contains(text) || tr.ParentPackage.ToLower().Contains(text);

            if (!showThis && text.StartsWith("size: ") && text.Contains('x') && text.Length > 6)
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
}