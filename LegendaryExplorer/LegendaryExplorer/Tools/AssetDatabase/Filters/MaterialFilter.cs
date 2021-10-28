using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public class MaterialFilter : GenericAssetFilter<MaterialRecord>
    {
        public List<IAssetSpecification<MaterialRecord>> BlendModes { get; private set; } = new();
        public ObservableCollection<IAssetSpecification<MaterialRecord>> GeneratedOptions { get; } = new();

        public MaterialFilter()
        {
            PopulateFilterOptions();
        }

        public void LoadFromDatabase(AssetDB currentDb)
        {
            GeneratedOptions.Clear();
            GeneratedOptions.AddRange(currentDb.MaterialBoolSpecs);
        }

        private void PopulateFilterOptions()
        {
            ///////////////////////////////////////
            // Add new custom Material Filters here
            ///////////////////////////////////////

            Filters = new ()
            {
                new PredicateSpecification<MaterialRecord>("Hide DLC only Materials", mr => !mr.IsDLCOnly),
                new PredicateSpecification<MaterialRecord>("Only Decal Materials",
                    mr => mr.MaterialName.Contains("Decal", StringComparison.OrdinalIgnoreCase)),
                new MaterialSettingSpec("Only Unlit Materials", "LightingModel", parm2: "MLM_Unlit"),
                new MaterialSettingSpec("Hide SkeletalMesh exclusive Materials", "bUsedWithSkeletalMesh", parm2: "True") {Inverted = true},
                new MaterialSettingSpec("Only 2 sided Materials", "TwoSided", parm2: "True"),
                new MaterialSettingSpec("Only Backface culled (1 side)", "TwoSided", parm2: "True") {Inverted = true},
                new UISeparator<MaterialRecord>(),
                new MaterialSettingSpec("Must have color setting", "VectorParameter",
                    setting => setting.Parm1.Contains("color", StringComparison.OrdinalIgnoreCase)),
                new PredicateSpecification<MaterialRecord>("Must have texture setting",
                    mr => mr.MatSettings.Any(x => x.Name == "TextureSampleParameter2D")),
                new MaterialSettingSpec("Must have talk scalar setting", "ScalarParameter",
                    setting => setting.Parm1.Contains("talk", StringComparison.OrdinalIgnoreCase))
            };

            BlendModes = new ()
            {
                new MaterialSettingSpec("Translucent or Additive (Opaque)", "BlendMode", (s => s.Parm2 == "BLEND_Translucent" || s.Parm2 == "BLEND_Additive"))
                {
                    Description = "BLEND_Translucent or BLEND_Additive. The 'opaque' filter in previous AssetDB versions."
                },
                new MaterialSettingSpec("Opaque", "BlendMode", parm2: "BLEND_Opaque"),
                new MaterialSettingSpec("Masked", "BlendMode", parm2: "BLEND_Masked"),
                new MaterialSettingSpec("Translucent", "BlendMode", parm2: "BLEND_Translucent"),
                new MaterialSettingSpec("Additive", "BlendMode", parm2: "BLEND_Additive"),
                new MaterialSettingSpec("Modulate", "BlendMode", parm2: "BLEND_Modulate"),
                new MaterialSettingSpec("Soft Masked", "BlendMode", parm2: "BLEND_SoftMasked"),
                new MaterialSettingSpec("Alpha Composite", "BlendMode", parm2: "BLEND_AlphaComposite"),
            };
        }

        public override bool Filter(MaterialRecord mr)
        {
            var enabledOptions = GetEnabledSpecifications();
            return enabledOptions.All(spec => spec.MatchesSpecification(mr));
        }

        private IEnumerable<IAssetSpecification<MaterialRecord>> GetEnabledSpecifications()
        {
            var blendModeOr = new OrSpecification<MaterialRecord>(BlendModes); // Matches spec if any of the selected BlendModes are true
            return Filters.Append(blendModeOr).Concat(GeneratedOptions).Where(spec => spec.IsSelected);
        }
    }
}