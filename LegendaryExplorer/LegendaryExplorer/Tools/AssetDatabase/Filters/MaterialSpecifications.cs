using System;
using System.Linq;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.AssetDatabase.Filters
{
    /// <summary>
    /// Specification to filter materials based on a BoolProperty
    /// </summary>
    public class MaterialBoolSpec : AssetSpecification<MaterialRecord>
    {
        /// <summary>
        /// If true, spec instead filters materials that do not have this property, or have false values
        /// </summary>
        public string PropertyName { get; set; }
        public bool Inverted { get; init; }

        public MaterialBoolSpec() { }
        public MaterialBoolSpec(BoolProperty boolFilter)
        {
            FilterName = boolFilter.Name;
            PropertyName = boolFilter.Name;
        }

        public override bool MatchesSpecification(MaterialRecord mr)
        {
            var anyTrue = mr.MatSettings.Any(s => s.Name == PropertyName && s.Parm2 == "True");
            return anyTrue ^ Inverted;
        }
    }

    /// <summary>
    /// Specification to filter materials based on MatSettings parameters
    /// </summary>
    public class MaterialSettingSpec : AssetSpecification<MaterialRecord>
    {
        public bool Inverted { get; init; }
        private readonly string _settingName;
        private readonly string _param1;
        private readonly string _param2;
        private readonly Predicate<MatSetting> _customPredicate;

        public MaterialSettingSpec(string filterName, string settingName, string param1 = "", string param2 = "")
        {
            FilterName = filterName;
            _settingName = settingName;
            _param1 = param1;
            _param2 = param2;
        }

        public MaterialSettingSpec(string filterName, string settingName, Predicate<MatSetting> predicate)
        {
            // Custom predicate option has an additional check against settingName.
            // We could remove this and make code a bit simpler, but we would need to add settingName checks to all predicates
            FilterName = filterName;
            _settingName = settingName;
            _customPredicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        public override bool MatchesSpecification(MaterialRecord mr)
        {
            Func<MatSetting, bool> predicate = _customPredicate is not null ? PredicateMatches : ParametersMatch;
            var specMatches = mr.MatSettings.Any(predicate);
            return specMatches ^ Inverted;
        }

        private bool PredicateMatches(MatSetting s)
        {
            if (s.Name == _settingName)
            {
                return _customPredicate(s);
            }
            return false;
        }

        private bool ParametersMatch(MatSetting s)
        {
            if (s.Name == _settingName)
            {
                if (!string.IsNullOrEmpty(_param1) && s.Parm1 != _param1)
                {
                    return false;
                }
                if (!string.IsNullOrEmpty(_param2) && s.Parm2 != _param2)
                {
                    return false;
                }
                return true;
            }
            return false;
        }
    }
}