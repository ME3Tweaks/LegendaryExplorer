using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    public interface IAssetFilter<in T>
    {
        public bool Filter(T record);
    }

    public class MaterialFilter : IAssetFilter<MaterialRecord>
    {
        public ObservableCollection<MaterialSpecification> FilterOptions { get; set; } = new();
        public ObservableCollection<MaterialSpecification> GeneratedOptions { get; set; } = new();

        public MaterialFilter()
        {
            PopulateFilterOptions();
        }

        public void LoadFromDatabase(AssetDB currentDb)
        {
            FilterOptions.Clear();
            PopulateFilterOptions();
            FilterOptions.AddRange(currentDb.MaterialBoolSpecs);
        }

        private void PopulateFilterOptions()
        {
            ///////////////////////////////////////
            // Add new custom Material Filters here
            ///////////////////////////////////////

            FilterOptions.Add(new MaterialPredicateSpec("Hide DLC only Materials", (mr => !mr.IsDLCOnly)));
            FilterOptions.Add(new MaterialPredicateSpec("Only Decal Materials", (mr => mr.MaterialName.Contains("Decal", StringComparison.OrdinalIgnoreCase))));
            FilterOptions.Add(new MaterialSettingSpec("Only Unlit Materials", "LightingModel", parm2: "MLM_Unlit"));
            FilterOptions.Add(new MaterialFilterSeparator());
            FilterOptions.Add(new MaterialSettingSpec("Must have color setting", "VectorParameter",
                setting => setting.Parm1.Contains("color", StringComparison.OrdinalIgnoreCase)));

            FilterOptions.Add(new MaterialPredicateSpec("Must have texture setting",
                mr => mr.MatSettings.Any(x => x.Name == "TextureSampleParameter2D")));

            FilterOptions.Add(new MaterialSettingSpec("Must have talk scalar setting", "ScalarParameter",
                setting => setting.Parm1.Contains("talk", StringComparison.OrdinalIgnoreCase)));
            FilterOptions.Add(new MaterialFilterSeparator());
        }

        public bool Filter(MaterialRecord mr)
        {
            var enabledOptions = GetEnabledSpecifications();
            return enabledOptions.All(spec => spec.MatchesSpecification(mr));
        }

        private IEnumerable<MaterialSpecification> GetEnabledSpecifications()
        {
            return FilterOptions.Concat(GeneratedOptions).Where(spec => spec.IsSelected);
        }

        public void SetSelected(MaterialSpecification spec)
        {
            spec.IsSelected = !spec.IsSelected;
        }
    }
}