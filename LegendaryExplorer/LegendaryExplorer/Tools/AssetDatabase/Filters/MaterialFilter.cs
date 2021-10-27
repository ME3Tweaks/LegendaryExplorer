using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using SharpDX.Direct2D1;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public interface IAssetFilter<in T>
    {
        public bool Filter(T record);
    }

    public class MaterialFilter : IAssetFilter<MaterialRecord>
    {
        public List<IAssetSpecification<MaterialRecord>> FilterOptions { get; set; } = new();

        public List<IAssetSpecification<MaterialRecord>> BlendMode { get; set; } = new();
        public ObservableCollection<IAssetSpecification<MaterialRecord>> GeneratedOptions { get; set; } = new();

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

            FilterOptions = new ()
            {
                new MaterialPredicateSpec("Hide DLC only Materials", mr => !mr.IsDLCOnly),
                new MaterialPredicateSpec("Only Decal Materials",
                    mr => mr.MaterialName.Contains("Decal", StringComparison.OrdinalIgnoreCase)),
                new MaterialSettingSpec("Only Unlit Materials", "LightingModel", parm2: "MLM_Unlit"),
                new MaterialSettingSpec("Hide SkeletalMesh exclusive Materials", "bUsedWithSkeletalMesh", parm2: "True") {Inverted = true},
                new MaterialSettingSpec("Only 2 sided Materials", "TwoSided", parm2: "True"),
                new MaterialSettingSpec("Only Backface culled (1 side)", "TwoSided", parm2: "True") {Inverted = true},
                new FilterSeparator<MaterialRecord>(),
                new MaterialSettingSpec("Must have color setting", "VectorParameter",
                    setting => setting.Parm1.Contains("color", StringComparison.OrdinalIgnoreCase)),
                new MaterialPredicateSpec("Must have texture setting",
                    mr => mr.MatSettings.Any(x => x.Name == "TextureSampleParameter2D")),
                new MaterialSettingSpec("Must have talk scalar setting", "ScalarParameter",
                    setting => setting.Parm1.Contains("talk", StringComparison.OrdinalIgnoreCase))
            };

            // TODO: Make BlendMode work as an OR list instead of an AND or just a single option
            BlendMode = new ()
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

        public bool Filter(MaterialRecord mr)
        {
            var enabledOptions = GetEnabledSpecifications();
            return enabledOptions.All(spec => spec.MatchesSpecification(mr));
        }

        private IEnumerable<IAssetSpecification<MaterialRecord>> GetEnabledSpecifications()
        {
            return FilterOptions.Concat(BlendMode).Concat(GeneratedOptions).Where(spec => spec.IsSelected);
        }

        public void SetSelected(MaterialSpecification spec)
        {
            if (!spec.IsSelected && BlendMode.Contains(spec))
            {
                foreach (var blendSpec in BlendMode) blendSpec.IsSelected = false;
            }

            spec.IsSelected = !spec.IsSelected;
        }
    }
}