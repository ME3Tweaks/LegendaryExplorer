using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Misc;

namespace LegendaryExplorerCore.GameFilesystem
{
    /// <summary>
    /// Enum defining armor types in LE1/ME1, used in Autoload.ini (well, technically not, but it could be!)
    /// </summary>
    public enum EAutoloadBioArmorType
    {
        ARMOR_TYPE_NONE = 0,
        ARMOR_TYPE_CLOTHING = 1,
        ARMOR_TYPE_LIGHT = 2,
        ARMOR_TYPE_MEDIUM = 3,
        ARMOR_TYPE_HEAVY = 4,
        ARMOR_TYPE_MAX = 5,
    };

    /// <summary>
    /// Autoload definition for adding armors - UNUSED, NOT SURE IF IT EVEN WORKS. Here for documentation purposes
    /// </summary>
    public class AutoloadArmor
    {
        // Only works for humans.
        public EAutoloadBioArmorType ArmorType { get; set; }
        public string ModelVariation { get; set; }
        public int? MaterialVariation { get; set; }
        public string MeshPackageName { get; set; }
        public string MaterialPackageName { get; set; }
        public int? MaterialsPerVariation { get; set; }

        public static AutoloadArmor SerializeFrom(int number, DuplicatingIni.Section section)
        {
            var armorType = section.GetValue($"ArmorType{number}");
            if (armorType == null)
                return null; // Not defined.

            AutoloadArmor armor = new AutoloadArmor();

            // ArmorType
            if (int.TryParse(armorType.Value, out var armorTypeInt))
            {
                armor.ArmorType = (EAutoloadBioArmorType)armorTypeInt;
            }

            // ModelVariation
            armor.ModelVariation = section.GetValue($"ModelVariation{number}")?.Value;

            // MaterialVariation
            var materialVariation = section.GetValue($"MaterialVariation{number}");
            if (int.TryParse(materialVariation.Value, out var materialVariationInt))
            {
                armor.MaterialVariation = materialVariationInt;
            }

            // MeshPackageName
            armor.MeshPackageName = section.GetValue($"MeshPackageName{number}")?.Value;

            // MaterialPackageName
            armor.MaterialPackageName = section.GetValue($"MaterialPackageName{number}")?.Value;

            // MaterialsPerVariation
            var materialsPerVariation = section.GetValue($"MaterialsPerVariation{number}");
            if (int.TryParse(materialsPerVariation.Value, out var materialsPerVariationInt))
            {
                armor.MaterialsPerVariation = materialsPerVariationInt;
            }

            return armor;
        }

        public void SerializeInto(int number, DuplicatingIni.Section section)
        {
            section[$"ArmorType{number}"].Value = ((int)ArmorType).ToString();
            section[$"ModelVariation{number}"].Value = ModelVariation;
            section[$"MaterialVariation{number}"].Value = ((int)MaterialVariation).ToString();
            section[$"MeshPackageName{number}"].Value = MeshPackageName;
            section[$"MaterialPackageName{number}"].Value = MaterialPackageName;
            section[$"MaterialsPerVariation{number}"].Value = ((int)MaterialVariation).ToString();
        }
    }

    /// <summary>
    /// C# representation of Autoload.ini from ME1/LE1
    /// </summary>
    public class AutoloadIni
    {
        /*
           2DA
           GlobalTalkTable
           DotU // DOES NOTHING IN LE1
           GlobalPackage // DOES NOT ROOT IN LE1 (unless with our ASI)
           PlotManagerStateTransitionMap
           PlotManagerConsequenceMap
           PlotManagerOutcomeMap
           PlotManagerQuestMap
           PlotManagerCodexMap
           PlotManagerConditionals
           ArmorMale
           ArmorFemale
           HeadGearMale
           HeadGearFemale

           // Scene stuff
           ModName
           ModMount
         */

        // Packages
        public List<string> Bio2DAs = [];
        public List<string> GlobalTalkTables = [];
        public List<string> DotUs = [];
        public List<string> GlobalPackages = [];
        public List<string> PlotManagerStateTransitionMaps = [];
        public List<string> PlotManagerConsequenceMaps = [];
        public List<string> PlotManagerOutcomeMaps = [];
        public List<string> PlotManagerQuestMaps = [];
        public List<string> PlotManagerCodexMaps = [];
        public List<string> PlotManagerConditionals = [];

        // GUI
        // Not used by LE1 but set anyways by ME3TweaksCore Starter Kit
        public int DLCModNameStrRef;

        // Custom scene stuff
        public string ModName;
        public int ModMount;

        public AutoloadIni() { }

        public AutoloadIni(string path)
        {
            var autoloadIni = DuplicatingIni.LoadIni(path);
            LoadPackages(autoloadIni);
            LoadGUI(autoloadIni);
            LoadModdingInfo(autoloadIni);
        }

        private void LoadModdingInfo(DuplicatingIni ini)
        {
            var section = ini.GetSection("ME1DLCMOUNT");
            if (section == null)
                return;

            ModName = section.GetValue("ModName")?.Value;
            int.TryParse(section.GetValue("ModMount")?.Value, out ModMount);
        }

        private void LoadGUI(DuplicatingIni ini)
        {
            var section = ini.GetSection("GUI");
            if (section == null)
                return;

            int.TryParse(section.GetValue("NameStrRef")?.Value, out DLCModNameStrRef);
        }

        private void LoadPackages(DuplicatingIni ini)
        {
            var section = ini.GetSection("Packages");
            if (section == null)
                return;

            int keyIndex = 1;
            string entry;

            // 2DA
            while ((entry = section.GetValue($"2DA{keyIndex++}")?.Value) != null)
            {
                Bio2DAs.Add(entry);
            }

            // GlobalTalkTables
            keyIndex = 1;
            while ((entry = section.GetValue($"GlobalTalkTable{keyIndex++}")?.Value) != null)
            {
                GlobalTalkTables.Add(entry);
            }

            // GlobalPackage
            keyIndex = 1;
            while ((entry = section.GetValue($"GlobalPackage{keyIndex++}")?.Value) != null)
            {
                GlobalPackages.Add(entry);
            }

            // PlotManagerConditionals
            keyIndex = 1;
            while ((entry = section.GetValue($"PlotManagerConditionals{keyIndex++}")?.Value) != null)
            {
                PlotManagerConditionals.Add(entry);
            }

            // PlotManagerStateTransitionMap
            keyIndex = 1;
            while ((entry = section.GetValue($"PlotManagerStateTransitionMap{keyIndex++}")?.Value) != null)
            {
                PlotManagerStateTransitionMaps.Add(entry);
            }

            // PlotManagerStateTransitionMap
            keyIndex = 1;
            while ((entry = section.GetValue($"PlotManagerStateTransitionMap{keyIndex++}")?.Value) != null)
            {
                PlotManagerConsequenceMaps.Add(entry);
            }

            // PlotManagerOutcomeMap
            keyIndex = 1;
            while ((entry = section.GetValue($"PlotManagerOutcomeMap{keyIndex++}")?.Value) != null)
            {
                PlotManagerOutcomeMaps.Add(entry);
            }

            // PlotManagerQuestMap
            keyIndex = 1;
            while ((entry = section.GetValue($"PlotManagerQuestMap{keyIndex++}")?.Value) != null)
            {
                PlotManagerQuestMaps.Add(entry);
            }

            // PlotManagerCodexMap
            keyIndex = 1;
            while ((entry = section.GetValue($"PlotManagerCodexMap{keyIndex++}")?.Value) != null)
            {
                PlotManagerCodexMaps.Add(entry);
            }
        }

        private void SerializeList(DuplicatingIni ini, string sectionName, string prefix, List<string> list)
        {
            var section = ini.GetOrAddSection(sectionName);
            int keyIndex = 1;
            foreach (var item in list)
            {
                section.SetSingleEntry($"{prefix}{keyIndex++}", item);
            }
        }

        /// <summary>
        /// Serializes this Autoload.ini to its text representation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            DuplicatingIni ini = new DuplicatingIni();
            SerializeList(ini, "Packages", "2DA", Bio2DAs);
            SerializeList(ini, "Packages", "DotU", DotUs); // Technically only matters in ME1, PS3 EoR port broke this
            SerializeList(ini, "Packages", "GlobalTalkTable", GlobalTalkTables);
            SerializeList(ini, "Packages", "GlobalPackage", GlobalPackages);
            SerializeList(ini, "Packages", "PlotManagerConditionals", PlotManagerConditionals);
            SerializeList(ini, "Packages", "PlotManagerStateTransitionMap", PlotManagerStateTransitionMaps);
            SerializeList(ini, "Packages", "PlotManagerConsequenceMap", PlotManagerConsequenceMaps);
            SerializeList(ini, "Packages", "PlotManagerOutcomeMap", PlotManagerOutcomeMaps);
            SerializeList(ini, "Packages", "PlotManagerQuestMap", PlotManagerQuestMaps);
            SerializeList(ini, "Packages", "PlotManagerCodexMap", PlotManagerCodexMaps);

            SerializeValue(ini, "GUI", "NameStrRef", DLCModNameStrRef);
            SerializeValue(ini, "ME1DLCMOUNT", "ModName", ModName);
            SerializeValue(ini, "ME1DLCMOUNT", "ModMount", ModMount);

            return ini.ToString();
        }

        private void SerializeValue(DuplicatingIni ini, string sectionName, string keyName, int intVal)
        {
            if (intVal == -1)
                return; // Do not serialize -1.

            var section = ini.GetOrAddSection(sectionName);
            section.SetSingleEntry(keyName, intVal.ToString());
        }

        private void SerializeValue(DuplicatingIni ini, string sectionName, string keyName, string str)
        {
            if (str == null)
                return; // Do not serialize null values.

            var section = ini.GetOrAddSection(sectionName);
            section.SetSingleEntry(keyName, str);
        }
    }
}
