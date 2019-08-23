using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal;

namespace ME3Explorer.Packages
{
    public static class MEPackageExtensions
    {

        public static string GetEntryString(this IMEPackage pcc, int index)
        {
            if (index == 0)
            {
                return "Null";
            }
            string retStr = "Entry not found";
            IEntry coreRefEntry = pcc.getEntry(index);
            if (coreRefEntry != null)
            {
                retStr = coreRefEntry is ImportEntry ? "[I] " : "[E] ";
                retStr += coreRefEntry.GetIndexedFullPath;
            }
            return retStr;
        }

        public static string FollowLink(this IMEPackage pcc, int uIndex)
        {
            if (pcc.isUExport(uIndex))
            {
                ExportEntry parent = pcc.getUExport(uIndex);
                return pcc.FollowLink(parent.idxLink) + parent.ObjectName + ".";
            }
            if (pcc.isUImport(uIndex))
            {
                ImportEntry parent = pcc.getUImport(uIndex);
                return pcc.FollowLink(parent.idxLink) + parent.ObjectName + ".";
            }
            return "";
        }

    }
    public static class ExportEntryExtensions
    {

        public static T GetProperty<T>(this ExportEntry export, string name) where T : UProperty
        {
            return export.GetProperties().GetProp<T>(name);
        }

        public static void WriteProperty(this ExportEntry export, UProperty prop)
        {
            var props = export.GetProperties();
            props.AddOrReplaceProp(prop);
            export.WriteProperties(props);
        }

        public static bool RemoveProperty(this ExportEntry export, string propname)
        {
            var props = export.GetProperties();
            UProperty propToRemove = null;
            foreach (UProperty prop in props)
            {
                if (prop.Name.Name == propname)
                {
                    propToRemove = prop;
                    break;
                }
            }

            //outside for concurrent collection modification
            if (propToRemove != null)
            {
                props.Remove(propToRemove);
                export.WriteProperties(props);
                return true;
            }

            return false;
        }

        public static bool IsTexture(this ExportEntry exportEntry)
        {
            return exportEntry.ClassName == "Texture2D" ||
                   exportEntry.ClassName == "LightMapTexture2D" ||
                   exportEntry.ClassName == "ShadowMapTexture2D" ||
                   exportEntry.ClassName == "TerrainWeightMapTexture" ||
                   exportEntry.ClassName == "TextureFlipBook";
        }



        public static bool IsDescendantOf(this ExportEntry export, ExportEntry ancestor)
        {
            IEntry exp = export;
            while (exp.HasParent)
            {
                exp = exp.Parent;
                if (exp == ancestor)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
