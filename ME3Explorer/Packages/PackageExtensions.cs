using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.BinaryConverters;

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
            IEntry coreRefEntry = pcc.GetEntry(index);
            if (coreRefEntry != null)
            {
                retStr = coreRefEntry is ImportEntry ? "[I] " : "[E] ";
                retStr += coreRefEntry.InstancedFullPath;
            }
            return retStr;
        }

        public static string FollowLink(this IMEPackage pcc, int uIndex)
        {
            if (pcc.IsUExport(uIndex))
            {
                ExportEntry parent = pcc.GetUExport(uIndex);
                return $"{pcc.FollowLink(parent.idxLink)}{parent.ObjectName}.";
            }
            if (pcc.IsImport(uIndex))
            {
                ImportEntry parent = pcc.GetImport(uIndex);
                return $"{pcc.FollowLink(parent.idxLink)}{parent.ObjectName}.";
            }
            return "";
        }

        //if neccessary, will fill in parents as Package Imports (if the import you need has non-Package parents, don't use this method)
        public static IEntry getEntryOrAddImport(this IMEPackage pcc, string fullPath, string className = "Class", string packageFile = "Core")
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return null;
            }

            //see if this import exists locally
            foreach (ImportEntry imp in pcc.Imports)
            {
                if (imp.FullPath == fullPath)
                {
                    return imp;
                }
            }

            //see if this is an export and exists locally
            foreach (ExportEntry exp in pcc.Exports)
            {
                if (exp.FullPath == fullPath)
                {
                    return exp;
                }
            }

            string[] pathParts = fullPath.Split('.');

            IEntry parent = pcc.getEntryOrAddImport(string.Join(".", pathParts.Take(pathParts.Length - 1)), "Package");

            var import = new ImportEntry(pcc)
            {
                idxLink = parent?.UIndex ?? 0,
                ClassName = className,
                ObjectName = pathParts.Last(),
                PackageFile = packageFile
            };
            pcc.AddImport(import);
            return import;
        }

        public static bool AddToLevelActorsIfNotThere(this IMEPackage pcc, ExportEntry actor)
        {
            if (pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is ExportEntry levelExport)
            {
                Level level = ObjectBinary.From<Level>(levelExport);
                if (level.Actors.Contains(actor.UIndex))
                {
                    return false;
                }
                level.Actors.Add(actor.UIndex);
                levelExport.setBinaryData(level.ToBytes(pcc));
                return true;
            }
            return false;
        }

        public static bool RemoveFromLevelActors(this IMEPackage pcc, ExportEntry actor)
        {
            if (pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is ExportEntry levelExport)
            {
                Level level = ObjectBinary.From<Level>(levelExport);
                if (level.Actors.Remove(actor.UIndex))
                {
                    levelExport.setBinaryData(level.ToBytes(pcc));
                    return true;
                }
            }
            return false;
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
                if (prop.Name == propname)
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
    }

    public static class IEntryExtensions
    {
        public static bool IsTrash(this IEntry entry)
        {
            return entry.ObjectName == UnrealPackageFile.TrashPackageName || entry.Parent?.ObjectName.Name == UnrealPackageFile.TrashPackageName;
        }

        public static bool IsTexture(this IEntry entry) =>
            entry.ClassName == "Texture2D" ||
            entry.ClassName == "LightMapTexture2D" ||
            entry.ClassName == "ShadowMapTexture2D" ||
            entry.ClassName == "TerrainWeightMapTexture" ||
            entry.ClassName == "TextureFlipBook";

        public static bool IsPartOfClassDefinition(this ExportEntry entry) =>
            entry.ClassName == "Class" ||
            entry.ClassName == "Function" ||
            entry.ClassName == "State" ||
            entry.ClassName == "Const" ||
            entry.ClassName == "Enum" ||
            entry.ClassName == "ScriptStruct" ||
            entry.ClassName == "IntProperty" ||
            entry.ClassName == "BoolProperty" ||
            entry.ClassName == "FloatProperty" ||
            entry.ClassName == "NameProperty" ||
            entry.ClassName == "StrProperty" ||
            entry.ClassName == "StringRefProperty" ||
            entry.ClassName == "ByteProperty" ||
            entry.ClassName == "ObjectProperty" ||
            entry.ClassName == "ComponentProperty" ||
            entry.ClassName == "InterfaceProperty" ||
            entry.ClassName == "ArrayProperty" ||
            entry.ClassName == "StructProperty" ||
            entry.ClassName == "BioMask4Property" ||
            entry.ClassName == "MapProperty" ||
            entry.ClassName == "ClassProperty" ||
            entry.ClassName == "DelegateProperty";

        public static bool IsDescendantOf(this IEntry entry, IEntry ancestor)
        {
            while (entry.HasParent)
            {
                entry = entry.Parent;
                if (entry == ancestor)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Gets direct children of <paramref name="entry"/>. O(n) over all IEntrys in the file,
        /// so if you will be calling this multiple times, consider using an <see cref="EntryTree"/> instead.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static List<IEntry> GetChildren(this IEntry entry)
        {
            var kids = new List<IEntry>();
            kids.AddRange(entry.FileRef.Exports.Where(export => export.idxLink == entry.UIndex));
            kids.AddRange(entry.FileRef.Imports.Where(import => import.idxLink == entry.UIndex));
            return kids;
        }

        /// <summary>
        /// Gets all descendents of <paramref name="entry"/>. O(nk) where n is exports + imports, and k is average tree depth,
        /// so consider using an <see cref="EntryTree"/> instead.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static List<IEntry> GetAllDescendants(this IEntry entry)
        {
            var kids = new List<IEntry>();
            kids.AddRange(entry.FileRef.Exports.Where(export => export.IsDescendantOf(entry)));
            kids.AddRange(entry.FileRef.Imports.Where(import => import.IsDescendantOf(entry)));
            return kids;
        }

        public static Dictionary<IEntry, List<string>> GetEntriesThatReferenceThisOne(this IEntry baseEntry)
        {
            var result = new Dictionary<IEntry, List<string>>();
            int baseUIndex = baseEntry.UIndex;
            foreach (ExportEntry exp in baseEntry.FileRef.Exports)
            {
                try
                {
                    if (exp == baseEntry)
                    {
                        continue;
                    }
                    //find header references
                    if (exp.Archetype == baseEntry)
                    {
                        result.AddToListAt(exp, "Header: Archetype");
                    }
                    if (exp.Class == baseEntry)
                    {
                        result.AddToListAt(exp, "Header: Class");
                    }
                    if (exp.SuperClass == baseEntry)
                    {
                        result.AddToListAt(exp, "Header: SuperClass");
                    }
                    if (exp.HasComponentMap && exp.ComponentMap.Any(kvp => kvp.Value == baseUIndex))
                    {
                        result.AddToListAt(exp, "Header: ComponentMap");
                    }

                    //find stack references
                    if (exp.HasStack && exp.Data is byte[] data
                                     && (baseUIndex == BitConverter.ToInt32(data, 0) || baseUIndex == BitConverter.ToInt32(data, 4)))
                    {
                        result.AddToListAt(exp, "Stack");
                    }


                    //find property references
                    findPropertyReferences(exp.GetProperties(), exp, "Property:");

                    //find binary references
                    if (!exp.IsDefaultObject && ObjectBinary.From(exp) is ObjectBinary objBin)
                    {
                        List<(UIndex, string)> indices = objBin.GetUIndexes(exp.FileRef.Game);
                        foreach ((UIndex uIndex, string propName) in indices)
                        {
                            if (uIndex == baseUIndex)
                            {
                                result.AddToListAt(exp, $"(Binary prop: {propName})");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    result.AddToListAt(exp, "Exception occured while reading this export!");
                }
            }

            return result;

            void findPropertyReferences(PropertyCollection props, ExportEntry exp, string prefix = "")
            {
                foreach (UProperty prop in props)
                {
                    switch (prop)
                    {
                        case ObjectProperty objectProperty:
                            if (objectProperty.Value == baseUIndex)
                            {
                                result.AddToListAt(exp, $"{prefix} {objectProperty.Name}");
                            }
                            break;
                        case DelegateProperty delegateProperty:
                            if (delegateProperty.Value.Object == baseUIndex)
                            {
                                result.AddToListAt(exp, $"{prefix} {delegateProperty.Name}");
                            }
                            break;
                        case StructProperty structProperty:
                            findPropertyReferences(structProperty.Properties, exp, $"{prefix} {structProperty.Name}:");
                            break;
                        case ArrayProperty<ObjectProperty> arrayProperty:
                            for (int i = 0; i < arrayProperty.Count; i++)
                            {
                                ObjectProperty objProp = arrayProperty[i];
                                if (objProp.Value == baseUIndex)
                                {
                                    result.AddToListAt(exp, $"{prefix} {arrayProperty.Name}[{i}]");
                                }
                            }
                            break;
                        case ArrayProperty<StructProperty> arrayProperty:
                            for (int i = 0; i < arrayProperty.Count; i++)
                            {
                                StructProperty structProp = arrayProperty[i];
                                findPropertyReferences(structProp.Properties, exp, $"{prefix} {arrayProperty.Name}[{i}]:");
                            }
                            break;
                    }
                }
            }
        }

        public static void CondenseArchetypes(this ExportEntry stmActor)
        {
            while (stmActor.Archetype is ExportEntry archetype)
            {
                var archProps = archetype.GetProperties();
                foreach (UProperty prop in archProps)
                {
                    if (!stmActor.GetProperties().ContainsNamedProp(prop.Name))
                    {
                        stmActor.WriteProperty(prop);
                    }
                }

                stmActor.Archetype = archetype.Archetype;
            }
        }

        public static void setBinaryData(this ExportEntry export, ObjectBinary bin) => export.setBinaryData(bin.ToBytes(export.FileRef));
    }
}
