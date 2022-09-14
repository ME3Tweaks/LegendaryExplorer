﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Paths;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Memory;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public static class EntryPruner
    {
        public static void TrashEntriesAndDescendants(IEnumerable<IEntry> itemsToTrash)
        {
            if (!itemsToTrash.Any()) return;
            var entriesToTrash = new List<IEntry>();
            entriesToTrash.AddRange(itemsToTrash);
            foreach (var entry in itemsToTrash)
            {
                entriesToTrash.AddRange(entry.GetAllDescendants());
            }

            // Trash in order of bottom first. This ensures that the package lookup tree stays correct. If we modified top first
            // it would break the lookup tree as the InstancedFullPath would no longer be accurate
            TrashEntries(entriesToTrash[0].FileRef, entriesToTrash.OrderByDescending(x => x.InstancedFullPath.Count(y => y == '.')));
        }
        public static void TrashEntryAndDescendants(IEntry entry)
        {
            var entriesToTrash = new List<IEntry> { entry };
            entriesToTrash.AddRange(entry.GetAllDescendants());
            TrashEntries(entry.FileRef, entriesToTrash.OrderByDescending(x => x.InstancedFullPath.Count(y => y == '.')));
        }
        public static void TrashEntries(IMEPackage pcc, IEnumerable<IEntry> itemsToTrash)
        {
            ExportEntry trashTopLevel = pcc.FindExport(UnrealPackageFile.TrashPackageName);
            IEntry packageClass = pcc.getEntryOrAddImport("Core.Package");

            foreach (IEntry entry in itemsToTrash)
            {
                if (entry == trashTopLevel || entry.ObjectName == "Trash") //don't trash what's already been trashed
                {
                    continue;
                }
                trashTopLevel = TrashEntry(entry, trashTopLevel, packageClass);
            }
            pcc.RemoveTrailingTrash();
        }

        /// <summary>
        /// Trashes an entry.
        /// </summary>
        /// <param name="entry">Entry to trash</param>
        /// <param name="trashContainer">Container for trash. Pass null if you want to create the trash container from the passed in value.</param>
        /// <param name="packageClass">Package class. Prevents multiple calls to find it</param>
        /// <returns>New trash container, otherwise will be null</returns>
        private static ExportEntry TrashEntry(IEntry entry, ExportEntry trashContainer, IEntry packageClass)
        {
            IMEPackage pcc = entry.FileRef;
            if (entry is ImportEntry imp)
            {
                if (trashContainer == null)
                {
                    trashContainer = new ExportEntry(pcc, 0, UnrealPackageFile.TrashPackageName);
                    pcc.AddExport(trashContainer);
                    trashContainer = TrashEntry(trashContainer, null, packageClass);
                    trashContainer.indexValue = 0;
                }
                imp.ClassName = "Package";
                imp.PackageFile = "Core";
                imp.idxLink = trashContainer.UIndex;
                imp.ObjectName = "Trash";
                imp.indexValue = 0;
            }
            else if (entry is ExportEntry exp)
            {
                using MemoryStream trashData = MemoryManager.GetMemoryStream();
                trashData.WriteInt32(-1);
                trashData.WriteInt32(pcc.FindNameOrAdd("None"));
                trashData.WriteInt32(0);
                exp.Data = trashData.ToArray();
                exp.Archetype = null;
                exp.SuperClass = null;
                exp.indexValue = 0;
                exp.Class = packageClass;
                exp.ObjectFlags &= ~UnrealFlags.EObjectFlags.HasStack;
                exp.ObjectFlags &= ~UnrealFlags.EObjectFlags.ArchetypeObject;
                exp.ObjectFlags &= ~UnrealFlags.EObjectFlags.ClassDefaultObject;
                exp.ComponentMap = null;
                if (trashContainer == null)
                {
                    exp.ObjectName = UnrealPackageFile.TrashPackageName;
                    exp.idxLink = 0;
                    if (exp.idxLink == exp.UIndex)
                    {
                        Debugger.Break(); // RECURSIVE LOOP DETECTION!!
                    }
                    exp.PackageGUID = UnrealPackageFile.TrashPackageGuid;
                    trashContainer = exp;
                }
                else
                {
                    exp.idxLink = trashContainer.UIndex;
                    if (exp.idxLink == exp.UIndex)
                    {
                        //This should not occur
                        Debugger.Break();
                    }
                    exp.ObjectName = "Trash";
                    exp.PackageGUID = Guid.Empty;
                }
                //(pcc as UnrealPackageFile).EntryLookupTable[exp.InstancedFullPath] = exp;
            }
            return trashContainer;
        }

        public static void TrashIncompatibleEntries(MEPackage pcc, MEGame oldGame, MEGame newGame)
        {
            var entries = pcc.Tree;
            var oldClasses = GlobalUnrealObjectInfo.GetClasses(oldGame);
            var newClasses = GlobalUnrealObjectInfo.GetClasses(newGame);
            var classesToRemove = oldClasses.Keys.Except(newClasses.Keys).ToHashSet();
            foreach (IEntry entry in entries)
            {
                if (classesToRemove.Contains(entry.ClassName) || (entry.ClassName == "Class" && classesToRemove.Contains(entry.ObjectName.Name))
                    || entry is ExportEntry exp && (exp.Archetype?.IsTrash() ?? false))
                {
                    TrashEntries(pcc, entries.FlattenTreeOf(entry.UIndex));
                }
            }
        }
        /// <summary>
        /// Removes properties that are not compatible with the export. Basic types are not pruned (such as Int, Float, Name)
        /// </summary>
        /// <param name="sourcePcc"></param>
        /// <param name="props"></param>
        /// <param name="typeName"></param>
        /// <param name="newGame"></param>
        /// <param name="removedProperties"></param>
        /// <returns></returns>
        public static PropertyCollection RemoveIncompatibleProperties(IMEPackage sourcePcc, PropertyCollection props, string typeName, MEGame newGame, ref bool removedProperties)
        {
            var infoProps = GlobalUnrealObjectInfo.GetAllProperties(newGame, typeName);
            var newProps = new PropertyCollection();
            foreach (Property prop in props)
            {
                if (infoProps.ContainsKey(prop.Name))
                {
                    switch (prop)
                    {
                        case ArrayProperty<EnumProperty> aep:
                            if (GlobalUnrealObjectInfo.GetEnumValues(newGame, aep.Reference) is List<NameReference> enumValues)
                            {
                                foreach (EnumProperty enumProperty in aep)
                                {
                                    if (!enumValues.Contains(enumProperty.Value))
                                    {
                                        enumProperty.Value = enumValues.First(); //hope that the first value is a reasonable default
                                    }
                                }
                                newProps.Add(aep);
                            }
                            else
                            {
                                Debug.WriteLine($"Trimmed property {prop.Name} from {typeName}");
                                removedProperties = true;
                            }
                            break;
                        case ArrayProperty<ObjectProperty> asp:
                            for (int i = asp.Count - 1; i >= 0; i--)
                            {
                                if (asp[i].Value == 0 || sourcePcc.GetEntry(asp[i].Value) is IEntry entry && !entry.FullPath.StartsWith(UnrealPackageFile.TrashPackageName))
                                {
                                    continue;
                                }
                                //delete if it references a trashed entry or if value is invalid
                                asp.RemoveAt(i);
                            }
                            newProps.Add(asp);
                            break;
                        case ArrayProperty<StructProperty> asp:
                            if (GlobalUnrealObjectInfo.GetStructs(newGame).ContainsKey(asp.Reference))
                            {
                                if (HasIncompatibleImmutabilities(asp.Reference, out bool newImmutability)) break;
                                foreach (StructProperty structProperty in asp)
                                {
                                    structProperty.Properties = RemoveIncompatibleProperties(sourcePcc, structProperty.Properties, structProperty.StructType, newGame, ref removedProperties);
                                    structProperty.IsImmutable = newImmutability;
                                }
                                newProps.Add(asp);
                            }
                            else
                            {
                                Debug.WriteLine($"Trimmed property {prop.Name} from {typeName}");
                                removedProperties = true;
                            }
                            break;
                        case DelegateProperty delegateProperty:
                            //script related, so just delete it.
                            // ?? Could this be automatically converted these days?
                            removedProperties = true;
                            Debug.WriteLine($"Trimmed property {prop.Name} from {typeName}");
                            break;
                        case EnumProperty enumProperty:
                            if (GlobalUnrealObjectInfo.GetEnumValues(newGame, enumProperty.EnumType) is List<NameReference> values)
                            {
                                values.Add(new NameReference("None"));
                                if (!values.Contains(enumProperty.Value))
                                {
                                    enumProperty.Value = values.First(); //hope that the first value is a reasonable default
                                }
                                newProps.Add(enumProperty);
                            }
                            else
                            {
                                Debug.WriteLine($"Trimmed property {prop.Name} from {typeName}");
                                removedProperties = true;
                            }
                            break;
                        case ObjectProperty objectProperty:
                            if (objectProperty.Value == 0 || sourcePcc.GetEntry(objectProperty.Value) is IEntry ent && !ent.FullPath.StartsWith(UnrealPackageFile.TrashPackageName))
                            {
                                newProps.Add(objectProperty);
                            }
                            else
                            {
                                Debug.WriteLine($"Trimmed property {prop.Name} from {typeName}");
                                removedProperties = true;
                            }
                            break;
                        case StructProperty structProperty:
                            string structType = structProperty.StructType;
                            if (GlobalUnrealObjectInfo.GetStructs(newGame).ContainsKey(structType))
                            {
                                if (HasIncompatibleImmutabilities(structType, out bool newImmutability))
                                {
                                    Debug.WriteLine($"Trimmed property {prop.Name} from {typeName}, as the struct immutabilities are not guaranteed compatible");
                                    removedProperties = true;
                                    break;
                                }
                                structProperty.Properties = RemoveIncompatibleProperties(sourcePcc, structProperty.Properties, structType, newGame, ref removedProperties);
                                structProperty.IsImmutable = newImmutability;
                                newProps.Add(structProperty);
                            }
                            else
                            {
                                Debug.WriteLine($"Trimmed property {prop.Name} from {typeName}");
                                removedProperties = true;
                            }
                            break;
                        default:
                            newProps.Add(prop);
                            break;
                    }
                }
                else
                {
                    // CROSSGEN-V TEST: Don't remove USEFUL but non-functional properties 
                    switch (prop)
                    {
                        case StrProperty when prop.Name == "ObjName":
                            newProps.Add(prop);
                            continue;
                        default:
                            removedProperties = true;
                            continue;
                    }
                    // End CROSSGEN-V

                    // OLD CODE
                    // removedProperties = true;
                }
            }

            return newProps;

            bool HasIncompatibleImmutabilities(string structType, out bool newImmutability)
            {
                bool sourceIsImmutable = GlobalUnrealObjectInfo.IsImmutable(structType, sourcePcc.Game);
                newImmutability = GlobalUnrealObjectInfo.IsImmutable(structType, newGame);

                if (sourceIsImmutable && newImmutability && !GlobalUnrealObjectInfo.GetClassOrStructInfo(sourcePcc.Game, structType).properties
                                                                             .SequenceEqual(GlobalUnrealObjectInfo.GetClassOrStructInfo(newGame, structType).properties))
                {
                    //both immutable, but have different properties
                    return true;
                }

                if (!sourceIsImmutable && newImmutability)
                {
                    //can't easily guarantee it will have have all neccesary properties
                    return true;
                }

                return false;
            }
        }
    }
}
