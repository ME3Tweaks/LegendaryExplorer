using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Toolkit.HighPerformance;

namespace LegendaryExplorerCore.Packages
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
            else
            {
                Debug.WriteLine("ENTRY NOT FOUND: " + index);
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

        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("Use GetEntryOrAddImport instead, which requires className be specified", true)]
        public static IEntry getEntryOrAddImport(this IMEPackage pcc, string instancedFullPath, string className = "Class", string packageFile = "Core")
        {
            return GetEntryOrAddImport(pcc, instancedFullPath, className, packageFile);
        }

        /// <summary>
        /// Finds existing <see cref="ExportEntry"/> or <see cref="ImportEntry"/> in <paramref name="pcc"/>.
        /// </summary>
        /// <param name="pcc"></param>
        /// <param name="instancedFullPath">The ifp of the object to find or add as an import. </param>
        /// <param name="className">Found entry must be of this class. (Will disambiguate when two entries of different classes have the same ifp.)
        /// Also used in creation of <see cref="ImportEntry"/> if neccesary.</param>
        /// <param name="packageFile">Used in creation of <see cref="ImportEntry"/> if neccesary. Should be the packagefile the class is defined in. </param>
        /// <remarks>if neccessary, will fill in parents as Package Imports (if the import you need has non-Package parents, ensure they exist first)
        /// <code>
        /// //Without the first two lines, the class and function entries would get created as Package Imports if they did not exist.
        /// pcc.GetEntryOrAddImport("Engine.Actor", "Class");
        /// pcc.GetEntryOrAddImport("Engine.Actor.SpecialHandling", "Function");
        /// IEntry entry = pcc.GetEntryOrAddImport("Engine.Actor.SpecialHandling.ReturnValue", "ObjectProperty");
        /// </code>
        /// </remarks>
        /// <returns></returns>
        public static IEntry GetEntryOrAddImport(this IMEPackage pcc, string instancedFullPath, string className, string packageFile = "Core")
        {
            if (string.IsNullOrEmpty(instancedFullPath))
            {
                return null;
            }

            //see if this import exists locally
            var entry = pcc.FindEntry(instancedFullPath);
            if (entry != null)
            {
                if (className is not null && !entry.ClassName.CaseInsensitiveEquals(className))
                {
                    int lastIndexOf = instancedFullPath.LastIndexOf('.') + 1;
                    var name = NameReference.FromInstancedString(lastIndexOf > 0 ? instancedFullPath[lastIndexOf..] : instancedFullPath);
                    //matching ifp, but wrong class. fall back to linear search
                    foreach (IEntry ent in pcc.Exports.Concat<IEntry>(pcc.Imports))
                    {
                        if (ent.ObjectName == name && ent.InstancedFullPath.CaseInsensitiveEquals(instancedFullPath) 
                                                   && ent.ClassName.CaseInsensitiveEquals(className))
                        {
                            return ent;
                        }
                    }
                }
                else
                {
                    return entry;
                }
            }

            string[] pathParts = instancedFullPath.Split('.');

            IEntry parent = pcc.GetEntryOrAddImport(string.Join(".", pathParts[..^1]), null);

            var import = new ImportEntry(pcc, parent, NameReference.FromInstancedString(pathParts.Last()))
            {
                ClassName = className ?? "Package",
                PackageFile = packageFile
            };
            pcc.AddImport(import);
            return import;
        }

        public static bool AddToLevelActorsIfNotThere(this IMEPackage pcc, params ExportEntry[] actors) //TODO NET 9: change to span params
        {
            bool added = false;
            if (pcc.FindExport("TheWorld.PersistentLevel") is ExportEntry { ClassName: "Level" } levelExport)
            {
                var level = ObjectBinary.From<Level>(levelExport);
                foreach (ExportEntry actor in actors)
                {
                    if (!level.Actors.Contains(actor.UIndex))
                    {
                        added = true;
                        level.Actors.Add(actor.UIndex);
                    }
                }
                levelExport.WriteBinary(level);
            }

            return added;
        }

        public static bool RemoveFromLevelActors(this IMEPackage pcc, ExportEntry actor)
        {
            if (pcc.FindExport("TheWorld.PersistentLevel") is ExportEntry { ClassName: "Level" } levelExport)
            {
                var level = ObjectBinary.From<Level>(levelExport);
                if (level.Actors.Remove(actor.UIndex))
                {
                    levelExport.WriteBinary(level);
                    return true;
                }
            }
            return false;
        }

        public static bool LevelContainsActor(this IMEPackage pcc, ExportEntry actor)
        {
            if (pcc.FindExport("TheWorld.PersistentLevel") is ExportEntry { ClassName: "Level" } levelExport)
            {
                var level = ObjectBinary.From<Level>(levelExport);
                if (level.Actors.Contains(actor.UIndex))
                {
                    return true;
                }
            }
            return false;
        }

        public static Dictionary<IEntry, List<string>> FindUsagesOfName(this IMEPackage pcc, string name)
        {
            var result = new Dictionary<IEntry, List<string>>();
            foreach (ExportEntry exp in pcc.Exports)
            {
                try
                {
                    //find header references
                    if (exp.ObjectName.Name == name)
                    {
                        result.AddToListAt(exp, "Header: Object Name");
                    }
                    if (exp.HasComponentMap && exp.ComponentMap.Any(kvp => kvp.Key.Name == name))
                    {
                        result.AddToListAt(exp, "Header: ComponentMap");
                    }

                    if ((!exp.IsDefaultObject && exp.IsA("Component") || pcc.Game == MEGame.UDK && exp.ClassName.EndsWith("Component")) &&
                        exp.ParentFullPath.Contains("Default__") &&
                        exp.DataSize >= 12 && EndianReader.ToInt32(exp.DataReadOnly, 4, exp.FileRef.Endian) is int nameIdx && pcc.IsName(nameIdx) &&
                        pcc.GetNameEntry(nameIdx) == name)
                    {
                        result.AddToListAt(exp, "Component TemplateName (0x4)");
                    }

                    //find property references
                    findPropertyReferences(exp.GetProperties(), exp, false, "Property: ");

                    //find binary references
                    if (!exp.IsDefaultObject && ObjectBinary.From(exp) is { } objBin)
                    {
                        if (objBin is BioStage bioStage)
                        {
                            if (bioStage.length > 0 && name == "m_aCameraList")
                            {
                                result.AddToListAt(exp, "(Binary prop: m_aCameraList name)");
                            }
                            int i = 0;
                            foreach ((NameReference key, PropertyCollection props) in bioStage.CameraList)
                            {
                                if (key.Name == name)
                                {
                                    result.AddToListAt(exp, $"(Binary prop: m_aCameraList[{i}])");
                                }
                                findPropertyReferences(props, exp, false, "Binary prop: m_aCameraList[{i}].");
                                ++i;
                            }
                        }
                        else if (objBin is UScriptStruct scriptStruct)
                        {
                            findPropertyReferences(scriptStruct.Defaults, exp, false, "Binary Property:");
                        }
                        else
                        {
                            List<(NameReference, string)> names = objBin.GetNames(exp.FileRef.Game);
                            foreach ((NameReference nameRef, string propName) in names)
                            {
                                if (nameRef.Name == name)
                                {
                                    result.AddToListAt(exp, $"(Binary prop: {propName})");
                                }
                            }
                        }
                    }
                }
                catch
                {
                    result.AddToListAt(exp, "Exception occured while reading this export!");
                }
            }

            foreach (ImportEntry import in pcc.Imports)
            {
                try
                {
                    if (import.ObjectName.Name == name)
                    {
                        result.AddToListAt(import, "ObjectName");
                    }
                    if (import.PackageFile == name)
                    {
                        result.AddToListAt(import, "PackageFile");
                    }
                    if (import.ClassName == name)
                    {
                        result.AddToListAt(import, "Class");
                    }
                }
                catch (Exception e)
                {
                    result.AddToListAt(import, $"Exception occurred while reading this import: {e.Message}");
                }
            }

            return result;

            void findPropertyReferences(PropertyCollection props, ExportEntry exp, bool isInImmutable = false, string prefix = "")
            {
                foreach (Property prop in props)
                {
                    if (!isInImmutable && prop.Name.Name == name)
                    {
                        result.AddToListAt(exp, $"{prefix}{prop.Name} name");
                    }
                    switch (prop)
                    {
                        case NameProperty nameProperty:
                            if (nameProperty.Value.Name == name)
                            {
                                result.AddToListAt(exp, $"{prefix}{nameProperty.Name} value");
                            }
                            break;
                        case DelegateProperty delegateProperty:
                            if (delegateProperty.Value.FunctionName.Name == name)
                            {
                                result.AddToListAt(exp, $"{prefix}{delegateProperty.Name} function name");
                            }
                            break;
                        case EnumProperty enumProperty:
                            if (pcc.Game >= MEGame.ME3 && !isInImmutable && enumProperty.EnumType.Name == name)
                            {
                                result.AddToListAt(exp, $"{prefix}{enumProperty.Name} enum type");
                            }
                            if (enumProperty.Value.Name == name)
                            {
                                result.AddToListAt(exp, $"{prefix}{enumProperty.Name} enum value");
                            }
                            break;
                        case StructProperty structProperty:
                            if (!isInImmutable && structProperty.StructType == name)
                            {
                                result.AddToListAt(exp, $"{prefix}{structProperty.Name} struct type");
                            }
                            findPropertyReferences(structProperty.Properties, exp, structProperty.IsImmutable, $"{prefix}{structProperty.Name}: ");
                            break;
                        case ArrayProperty<NameProperty> arrayProperty:
                            for (int i = 0; i < arrayProperty.Count; i++)
                            {
                                NameProperty nameProp = arrayProperty[i];
                                if (nameProp.Value.Name == name)
                                {
                                    result.AddToListAt(exp, $"{prefix}{arrayProperty.Name}[{i}]");
                                }
                            }
                            break;
                        case ArrayProperty<EnumProperty> arrayProperty:
                            for (int i = 0; i < arrayProperty.Count; i++)
                            {
                                EnumProperty enumProp = arrayProperty[i];
                                if (enumProp.Value.Name == name)
                                {
                                    result.AddToListAt(exp, $"{prefix}{arrayProperty.Name}[{i}]");
                                }
                            }
                            break;
                        case ArrayProperty<StructProperty> arrayProperty:
                            for (int i = 0; i < arrayProperty.Count; i++)
                            {
                                StructProperty structProp = arrayProperty[i];
                                findPropertyReferences(structProp.Properties, exp, structProp.IsImmutable, $"{prefix}{arrayProperty.Name}[{i}].");
                            }
                            break;
                    }
                }
            }
        }

        public static HashSet<int> GetReferencedEntries(this IMEPackage pcc, bool getreferenced = true, bool getactorrefs = false, ExportEntry startatexport = null)
        {
            var result = new HashSet<int>();
            var entriesToEvaluate = new Stack<IEntry>();
            var entriesEvaluated = new HashSet<IEntry>();
            var entriesReferenced = new HashSet<IEntry>();
            if (startatexport != null) //Start at object
            {
                entriesToEvaluate.Push(startatexport);
                entriesReferenced.Add(startatexport);
                entriesEvaluated.Add(pcc.GetUExport(startatexport.idxLink));  //Do not go up the chain if parsing an export
            }
            else if (pcc.Exports.FirstOrDefault(exp => exp.ClassName == "Level") is ExportEntry levelExport) //Evaluate level with only actors, model+components, sequences and level class being processed.
            {
                var level = ObjectBinary.From<Level>(levelExport);
                entriesEvaluated.Add(null); //null stops future evaluations
                entriesEvaluated.Add(levelExport);
                entriesReferenced.Add(levelExport);
                var levelclass = levelExport.Class;
                entriesToEvaluate.Push(levelclass);
                entriesReferenced.Add(levelclass);
                foreach (int actoridx in level.Actors)
                {
                    var actor = pcc.GetEntry(actoridx);
                    entriesToEvaluate.Push(actor);
                    entriesReferenced.Add(actor);
                }
                var model = pcc.GetEntry(level.Model);
                entriesToEvaluate.Push(model);
                entriesReferenced.Add(model);
                foreach (var comp in level.ModelComponents)
                {
                    var compxp = pcc.GetEntry(comp);
                    entriesToEvaluate.Push(compxp);
                    entriesReferenced.Add(compxp);
                }
                foreach (var seq in level.GameSequences)
                {
                    var seqxp = pcc.GetEntry(seq);
                    entriesToEvaluate.Push(seqxp);
                    entriesReferenced.Add(seqxp);
                }
                var localpackage = pcc.Exports.FirstOrDefault(x => x.ClassName == "Package" && string.Equals(x.ObjectName.Instanced.ToString(), Path.GetFileNameWithoutExtension(pcc.FilePath), StringComparison.OrdinalIgnoreCase));  // Make sure world, localpackage, shadercache are all marked as referenced.
                entriesToEvaluate.Push(localpackage);
                entriesReferenced.Add(localpackage);
                var world = levelExport.Parent;
                entriesToEvaluate.Push(world);
                entriesReferenced.Add(world);
                var shadercache = pcc.Exports.FirstOrDefault(x => x.ClassName == "ShaderCache");
                if (shadercache != null)
                {
                    entriesEvaluated.Add(shadercache);
                    entriesReferenced.Add(shadercache);
                    entriesToEvaluate.Push(shadercache.Class);
                    entriesReferenced.Add(shadercache.Class);
                }
            }
            else
            {
                return result;  //If this has no level it is a reference / seekfree package and shouldn't be compacted.
            }

            var theserefs = new HashSet<IEntry>();
            while (!entriesToEvaluate.IsEmpty())
            {
                var ent = entriesToEvaluate.Pop();
                try
                {
                    if (entriesEvaluated.Contains(ent) || (ent?.UIndex ?? 0) == 0 || (getactorrefs && !ent.InstancedFullPath.Contains("PersistentLevel")))
                    {
                        continue;
                    }
                    entriesEvaluated.Add(ent);
                    if (ent.idxLink != 0)
                    {
                        theserefs.Add(pcc.GetEntry(ent.idxLink));
                    }
                    if (ent.UIndex < 0)
                    {
                        continue;
                    }
                    var exp = pcc.GetUExport(ent.UIndex);

                    //find header references only if doing non-actors
                    if (!getactorrefs)
                    {
                        if ((exp.Archetype?.UIndex ?? 0) != 0)
                        {
                            theserefs.Add(exp.Archetype);
                        }

                        if ((exp.Class?.UIndex ?? 0) != 0)
                        {
                            theserefs.Add(exp.Class);
                        }
                        if ((exp.SuperClass?.UIndex ?? 0) != 0)
                        {
                            theserefs.Add(exp.SuperClass);
                        }
                        if (exp.HasComponentMap)
                        {
                            foreach ((_, int index) in exp.ComponentMap)
                            {
                                theserefs.Add(pcc.GetEntry(index + 1));
                            }
                        }
                    }
                    else
                    {
                        exp.CondenseArchetypes();
                    }

                    //find property references
                    findPropertyReferences(exp.GetProperties(), exp);

                    //find binary references
                    if (!exp.IsDefaultObject && ObjectBinary.From(exp) is ObjectBinary objBin)
                    {
                        objBin.ForEachUIndex(exp.FileRef.Game, new ReferenceFinder(exp.UIndex, pcc, theserefs));
                    }

                    foreach (var reference in theserefs)
                    {
                        if (!entriesEvaluated.Contains(reference))
                        {
                            entriesToEvaluate.Push(reference);
                            entriesReferenced.Add(reference);
                        }
                    }
                    theserefs.Clear();
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error getting references {ent.UIndex} {ent.ObjectName.Instanced}: {e.Message}");
                }
            }
            if (getreferenced)
            {
                foreach (var entry in entriesReferenced)
                {
                    result.Add(entry?.UIndex ?? 0);
                }
            }
            else
            {
                foreach (var xp in pcc.Exports)
                {
                    if (!entriesReferenced.Contains(xp))
                    {
                        result.Add(xp?.UIndex ?? 0);
                    }
                }
                foreach (var im in pcc.Imports)
                {
                    if (!entriesReferenced.Contains(im))
                    {
                        result.Add(im?.UIndex ?? 0);
                    }
                }
            }

            return result;

            void findPropertyReferences(PropertyCollection props, ExportEntry exp)
            {
                foreach (Property prop in props)
                {
                    switch (prop)
                    {
                        case ObjectProperty objectProperty:
                            if (objectProperty.Value != 0 && objectProperty.Value != exp.UIndex)
                            {
                                theserefs.Add(pcc.GetEntry(objectProperty.Value));
                            }
                            break;
                        case DelegateProperty delegateProperty:
                            if (delegateProperty.Value.ContainingObjectUIndex != 0 && delegateProperty.Value.ContainingObjectUIndex != exp.UIndex)
                            {
                                theserefs.Add(pcc.GetEntry(delegateProperty.Value.ContainingObjectUIndex));
                            }
                            break;
                        case StructProperty structProperty:
                            findPropertyReferences(structProperty.Properties, exp);
                            break;
                        case ArrayProperty<ObjectProperty> arrayProperty:
                            for (int i = 0; i < arrayProperty.Count; i++)
                            {
                                ObjectProperty objProp = arrayProperty[i];
                                if (objProp.Value != 0 && objProp.Value != exp.UIndex)
                                {
                                    theserefs.Add(pcc.GetEntry(objProp.Value));
                                }
                            }
                            break;
                        case ArrayProperty<StructProperty> arrayProperty:
                            for (int i = 0; i < arrayProperty.Count; i++)
                            {
                                StructProperty structProp = arrayProperty[i];
                                findPropertyReferences(structProp.Properties, exp);
                            }
                            break;
                    }
                }
            }
        }
        
        private readonly struct ReferenceFinder : IUIndexAction
        {
            private readonly int CurrentExportUIndex;
            private readonly IMEPackage Pcc;
            private readonly HashSet<IEntry> Refs;

            public ReferenceFinder(int currentExportUIndex, IMEPackage pcc, HashSet<IEntry> refs)
            {
                CurrentExportUIndex = currentExportUIndex;
                Pcc = pcc;
                Refs = refs;
            }

            public void Invoke(ref int uIndex, string propName)
            {
                if (uIndex != CurrentExportUIndex)
                {
                    Refs.Add(Pcc.GetEntry(uIndex));
                }
            }
        }
    }

    public static class ExportEntryExtensions
    {
        public static T GetProperty<T>(this ExportEntry export, string name, PackageCache cache = null) where T : Property
        {
            return export.GetProperties(packageCache: cache).GetProp<T>(name);
        }

        /// <summary>
        /// Writes a property to the export, replacing a property with the same <see cref="Property.Name"/> and <see cref="Property.StaticArrayIndex"/> if it exists,
        /// otherwise adding a new one. 
        /// </summary>
        /// <param name="export"></param>
        /// <param name="prop"></param>
        public static void WriteProperty(this ExportEntry export, Property prop)
        {
            var props = export.GetProperties();
            props.AddOrReplaceProp(prop);
            export.WriteProperties(props);
        }

        public static bool RemoveProperty(this ExportEntry export, string propname)
        {
            var props = export.GetProperties();
            Property propToRemove = null;
            foreach (Property prop in props)
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
        public static void WritePropertyAndBinary(this ExportEntry export, Property prop, ObjectBinary binary)
        {
            var props = export.GetProperties();
            props.AddOrReplaceProp(prop);
            export.WritePropertiesAndBinary(props, binary);
        }

        public static bool IsInDefaultsTree(this ExportEntry export)
        {
            while (export is not null)
            {
                if (export.IsDefaultObject)
                {
                    return true;
                }
                export = export.Parent as ExportEntry;
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
            entry.ClassName
                is "Texture2D"
                or "LightMapTexture2D"
                or "ShadowMapTexture2D"
                or "TerrainWeightMapTexture"
                or "TextureFlipBook";

        [Obsolete($"Use {nameof(IsScriptExport)} instead", true)]
        public static bool IsPartOfClassDefinition(this ExportEntry entry) => IsScriptExport(entry);
        public static bool IsScriptExport(this ExportEntry entry) =>
            entry.ClassName
                is "Class"
                or "Function"
                or "State"
                or "Const"
                or "Enum"
                or "ScriptStruct"
                or "IntProperty"
                or "BoolProperty"
                or "FloatProperty"
                or "NameProperty"
                or "StrProperty"
                or "StringRefProperty"
                or "ByteProperty"
                or "ObjectProperty"
                or "ComponentProperty"
                or "InterfaceProperty"
                or "ArrayProperty"
                or "StructProperty"
                or "BioMask4Property"
                or "MapProperty"
                or "ClassProperty"
                or "DelegateProperty";

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
        /// Gets direct children of <paramref name="entry"/>. 
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static IEnumerable<IEntry> GetChildren(this IEntry entry)
        {
            return entry.FileRef.Tree.GetDirectChildrenOf(entry);
        }

        /// <summary>
        /// Gets direct children of <paramref name="entry"/> that are <typeparamref name="T"/>. 
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static IEnumerable<T> GetChildren<T>(this IEntry entry) where T : IEntry
        {
            foreach (IEntry ent in entry.FileRef.Tree.GetDirectChildrenOf(entry))
            {
                if (ent is T tmp)
                {
                    yield return tmp;
                }
            }
        }

        /// <summary>
        /// Gets all descendents of <paramref name="entry"/>.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static List<IEntry> GetAllDescendants(this IEntry entry)
        {
            return entry.FileRef.Tree.FlattenTreeOf(entry);
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
                    if (exp.HasComponentMap && exp.ComponentMap.Any(kvp => kvp.Value + 1 == baseUIndex))
                    {
                        result.AddToListAt(exp, "Header: ComponentMap");
                    }

                    //find stack references
                    if (exp.HasStack)
                    {
                        if (baseUIndex == EndianReader.ToInt32(exp.DataReadOnly, 0, exp.FileRef.Endian) || baseUIndex == EndianReader.ToInt32(exp.DataReadOnly, 4, exp.FileRef.Endian))
                        {
                            result.AddToListAt(exp, "Stack");
                        }
                    }
                    else if (exp.TemplateOwnerClassIdx is var toci and >= 0 && baseUIndex == EndianReader.ToInt32(exp.DataReadOnly, toci, exp.FileRef.Endian))
                    {
                        result.AddToListAt(exp, $"TemplateOwnerClass (Data offset 0x{toci:X})");
                    }

                    //find property references
                    findPropertyReferences(exp.GetProperties(), exp, "Property:");

                    //find binary references
                    if (!exp.IsDefaultObject
                        && exp.ClassName != "AnimSequence" //has no UIndexes, and is expensive to deserialize
                        && ObjectBinary.From(exp) is ObjectBinary objBin)
                    {
                        objBin.ForEachUIndex(exp.FileRef.Game, new ReferenceFinder(baseUIndex, exp, result));
                    }
                }
                catch (Exception e) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    result.AddToListAt(exp, $"Exception occurred while reading this export: {e.Message}");
                }
            }

            return result;

            void findPropertyReferences(PropertyCollection props, ExportEntry exp, string prefix = "")
            {
                foreach (Property prop in props)
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
                            if (delegateProperty.Value.ContainingObjectUIndex == baseUIndex)
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

        private readonly struct ReferenceFinder : IUIndexAction
        {
            private readonly int UIndexToFind;
            private readonly ExportEntry CurrentExport;
            private readonly Dictionary<IEntry, List<string>> ResultDict;

            public ReferenceFinder(int uIndexToFind, ExportEntry currentExport, Dictionary<IEntry, List<string>> resultDict)
            {
                UIndexToFind = uIndexToFind;
                CurrentExport = currentExport;
                ResultDict = resultDict;
            }

            public void Invoke(ref int uIndex, string propName)
            {
                if (uIndex == UIndexToFind)
                {
                    ResultDict.AddToListAt(CurrentExport, $"(Binary prop: {propName})");
                }
            }
        }

        public static int ReplaceAllReferencesToThisOne(this IEntry baseEntry, IEntry replacementEntry)
        {
            int rcount = 0;
            int selectedEntryUIndex = baseEntry.UIndex;
            int replacementUIndex = replacementEntry.UIndex;
            var references = baseEntry.GetEntriesThatReferenceThisOne();
            foreach ((IEntry entry, List<string> propsList) in references)
            {
                if (entry is ExportEntry exp)
                {
                    if (propsList.Any(l => l.StartsWith("Property:")))
                    {
                        var newprops = replacePropertyReferences(exp.GetProperties(), selectedEntryUIndex, replacementUIndex, ref rcount);
                        exp.WriteProperties(newprops);
                    }
                    else
                    {
                        if (propsList.Any(l => l.StartsWith("(Binary prop:")) && !exp.IsDefaultObject && ObjectBinary.From(exp) is ObjectBinary objBin)
                        {
                            var refReplacer = new ReferenceReplacer(selectedEntryUIndex, replacementUIndex);
                            objBin.ForEachUIndex(exp.FileRef.Game, refReplacer);
                            rcount += refReplacer.ReplacementCount;

                            //script relinking is not covered by standard binary relinking
                            if (objBin is UStruct uStruct && uStruct.ScriptBytes.Length > 0)
                            {
                                if (exp.Game is MEGame.ME3 | exp.Game.IsLEGame())
                                {
                                    (List<Token> tokens, _) = Bytecode.ParseBytecode(uStruct.ScriptBytes, exp);
                                    foreach (Token token in tokens)
                                    {
                                        foreach ((int pos, int type, int value) in token.inPackageReferences)
                                        {
                                            switch (type)
                                            {
                                                case Token.INPACKAGEREFTYPE_ENTRY when value == selectedEntryUIndex:
                                                    uStruct.ScriptBytes.OverwriteRange(pos, BitConverter.GetBytes(replacementUIndex));
                                                    rcount++;
                                                    break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    var func = entry.ClassName == "State" ? UE3FunctionReader.ReadState(exp) : UE3FunctionReader.ReadFunction(exp);
                                    func.Decompile(new TextBuilder(), false);

                                    foreach ((long position, IEntry ent) in func.EntryReferences)
                                    {
                                        if (ent.UIndex == selectedEntryUIndex && position < uStruct.ScriptBytes.Length)
                                        {
                                            uStruct.ScriptBytes.OverwriteRange((int)position, BitConverter.GetBytes(replacementUIndex));
                                            rcount++;
                                        }
                                    }
                                }
                            }

                            exp.WriteBinary(objBin);
                        }
                    }
                }
            }

            return rcount;

            static PropertyCollection replacePropertyReferences(PropertyCollection props, int targetUIndex, int replaceUIndex, ref int replacementCount)
            {
                var newprops = new PropertyCollection();
                foreach (Property prop in props)
                {
                    switch (prop)
                    {
                        case ObjectProperty objectProperty:
                            if (objectProperty.Value == targetUIndex)
                            {
                                objectProperty.Value = replaceUIndex;
                                replacementCount++;
                            }
                            break;
                        case DelegateProperty delegateProperty:
                            if (delegateProperty.Value.ContainingObjectUIndex == targetUIndex)
                            {
                                delegateProperty.Value = new ScriptDelegate(replaceUIndex, delegateProperty.Value.FunctionName);
                                replacementCount++;
                            }
                            break;
                        case StructProperty structProperty:
                            structProperty.Properties = replacePropertyReferences(structProperty.Properties, targetUIndex, replaceUIndex, ref replacementCount);
                            break;
                        case ArrayProperty<ObjectProperty> arrayProperty:
                            foreach (ObjectProperty objProp in arrayProperty)
                            {
                                if (objProp.Value == targetUIndex)
                                {
                                    objProp.Value = replaceUIndex;
                                    replacementCount++;
                                }
                            }
                            break;
                        case ArrayProperty<StructProperty> arrayProperty:
                            foreach (StructProperty structProp in arrayProperty)
                            {
                                structProp.Properties = replacePropertyReferences(structProp.Properties, targetUIndex, replaceUIndex, ref replacementCount);
                            }
                            break;
                    }
                    newprops.AddOrReplaceProp(prop);
                }

                return newprops;
            }
        }

        private readonly struct ReferenceReplacer : IUIndexAction
        {
            private readonly int UIndexToReplace;
            private readonly int ReplacementUIndex;
            
            //this is the c# version of a pointer to an int
            public readonly Box<int> ReplacementCount;

            public ReferenceReplacer(int uIndexToReplace, int replacementUIndex)
            {
                UIndexToReplace = uIndexToReplace;
                ReplacementUIndex = replacementUIndex;
                ReplacementCount = 0;
            }

            public void Invoke(ref int uIndex, string propName)
            {
                if (uIndex == UIndexToReplace)
                {
                    uIndex = ReplacementUIndex;
                    ReplacementCount.GetReference() += 1;
                }
            }
        }

        public static void CondenseArchetypes(this ExportEntry export, bool removeArchetypeLink = true)
        {
            IEntry archetypeEntry = export.Archetype;
            var properties = export.GetProperties();
            while (archetypeEntry is ExportEntry archetype)
            {
                var archProps = archetype.GetProperties();
                foreach (Property prop in archProps)
                {
                    if (!properties.ContainsNamedProp(prop.Name))
                    {
                        properties.AddOrReplaceProp(prop);
                    }
                }

                archetypeEntry = archetype.Archetype;
            }
            export.WriteProperties(properties);

            export.Archetype = removeArchetypeLink ? null : archetypeEntry;
        }

        public static T GetBinaryData<T>(this ExportEntry export) where T : ObjectBinary, new() => ObjectBinary.From<T>(export);

        public static T GetBinaryData<T>(this ExportEntry export, PackageCache packageCache) where T : ObjectBinary, new() => ObjectBinary.From<T>(export, packageCache);
    }
}
