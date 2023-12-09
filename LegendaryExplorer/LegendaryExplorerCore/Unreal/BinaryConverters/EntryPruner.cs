using System;
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
        /// <summary>
        /// The list of arrayproperties on BioWorldInfo to enumerate and remove trashed items from
        /// </summary>
        private static readonly string[] BWIProperitesToCleanupOnTrash = new string[] { "ClientDestroyedActorContent" }; // Array cause we might add more as we encountered them

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

            // 02/04/2023: #353 Remove from ClientDestroyedActorContent and TextureToInstancesMap
            var level = pcc.FindExport("TheWorld.PersistentLevel");
            if (level != null)
            {
                var l = ObjectBinary.From<Level>(level);
                var trashed = false;
                foreach (var item in itemsToTrash.Where(x => x.IsTexture()))
                {
                    trashed |= l.TextureToInstancesMap.Remove(item.UIndex);
                }

                if (trashed)
                {
                    level.WriteBinary(l);
                }

                var bwi = pcc.GetUExport(l.Actors[0]); // 0 is always BioWorldInfo
                var props = bwi.GetProperties();
                var uindexes = itemsToTrash.Select(x => x.UIndex).ToList();
                foreach (var propName in BWIProperitesToCleanupOnTrash)
                {
                    var array = props.GetProp<ArrayProperty<ObjectProperty>>(propName);
                    if (array != null)
                    {
                        array.RemoveAll(x => x == null || uindexes.Contains(x.Value)); // Remove it
                    }
                }

                bwi.WriteProperties(props);
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
            if (GlobalUnrealObjectInfo.GetClassOrStructInfo(newGame, typeName) == null)
            {
                // We cannot determine properties of this class in target game. The class will probably be ported over.
                return props;
            }
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
                                if (HasIncompatibleImmutabilities(asp.Reference, out bool newImmutability))
                                {
                                    // Attempt to correct an incompatible property
                                    var correctedProperty = AttemptArrayStructCorrection(asp, sourcePcc.Game, newGame);
                                    if (correctedProperty == null)
                                    {
                                        // Correction was not performed - strip the property
                                        Debug.WriteLine($"Trimmed incompatible immutable array property {prop.Name} from {typeName}");
                                        removedProperties = true;
                                        break;
                                    }
                                    Debug.WriteLine($"Corrected incompatible immutable array property {prop.Name} from {typeName} to compatible version");
                                    newProps.Add(correctedProperty);
                                    continue; // Continue parsing
                                }
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
#if DEBUG
                    // For debugging
                    //var firstGameProps = GlobalUnrealObjectInfo.GetClassOrStructInfo(sourcePcc.Game, structType).properties;
                    //var secondGameProps = GlobalUnrealObjectInfo.GetClassOrStructInfo(newGame, structType).properties;
                    //for (int i = 0; i < firstGameProps.Count; i++)
                    //{
                    //    Debug.WriteLine($"{firstGameProps[i].Key}\t\t{secondGameProps[i].Key}");
                    //}
#endif

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

        /// <summary>
        /// Attempts to correct an ArrayProperty<StructProperty>
        /// </summary>
        /// <param name="asp"></param>
        /// <param name="sourcePccGame"></param>
        /// <param name="newGame"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private static Property AttemptArrayStructCorrection(ArrayProperty<StructProperty> arrayOfStructs, MEGame sourcePccGame, MEGame newGame)
        {
            if (sourcePccGame == MEGame.ME3 && newGame == MEGame.LE3 && arrayOfStructs.Name == "Slots")
            {
                // This makes a copy to ensure whatever calls this doesn't get a side effect
                // Correction: CoverSlot ME3 -> LE3
                ArrayProperty<StructProperty> newProp = new ArrayProperty<StructProperty>("Slots");
                foreach (var sp in arrayOfStructs)
                {
                    var newStruct = new StructProperty(sp.StructType, sp.Properties, sp.Name, sp.IsImmutable);
                    var slotMarker = newStruct.Properties[15]; // SlotMarker is at slot 15 in ME3, 11 in LE3
                    newStruct.Properties.RemoveAt(15); // Remove property - it must be here or it will shift by 1!
                    newStruct.Properties.Insert(11, slotMarker);
                    newProp.Add(newStruct);
                }

                return newProp;
            }
            else if (sourcePccGame == MEGame.LE2 && newGame == MEGame.LE3 && arrayOfStructs.Name == "Slots")
            {
                // This has potential to be very slow because we have to reference the level index

                // This requires adding a few extra properties. We have no way to fill them in so we just fill it with blank data.
                ArrayProperty<StructProperty> newProp = new ArrayProperty<StructProperty>("Slots");
                foreach (var sp in arrayOfStructs)
                {
                    var newStruct = new StructProperty(sp.StructType, sp.Properties, sp.Name, sp.IsImmutable);

                    var fireLinks = sp.Properties.GetProp<ArrayProperty<StructProperty>>("FireLinks"); // Needs the struct properties converted?
                    //var rejectedFireLinks = sp.Properties.GetProp<ArrayProperty<StructProperty>>("RejectedFireLinks"); // Needs the struct properties converted?
                    var exposedLinks = sp.Properties.GetProp<ArrayProperty<StructProperty>>("ExposedFireLinks"); // Converts to ExposedCoverPackedProperties?
                    var dangerLinks = sp.Properties.GetProp<ArrayProperty<StructProperty>>("DangerLinks"); // Converts to DangerCoverPackedProperties?
                    var turnTarget = sp.Properties.GetProp<ArrayProperty<StructProperty>>("TurnTarget"); // Converts to TurnTargetPackedProperties?

                    sp.Properties.Remove(fireLinks);
                    //sp.Properties.Remove(rejectedFireLinks);
                    sp.Properties.Remove(exposedLinks);
                    sp.Properties.Remove(dangerLinks);
                    sp.Properties.Remove(turnTarget);
                    sp.Properties.RemoveNamedProperty("ForcedFireLinks"); // This is not present in LE3

                    // FireLinks
                    var newFireLinks = new ArrayProperty<StructProperty>("FireLinks");
                    foreach (var fl in fireLinks)
                    {
                        // Todo - convert
                    }

                    // sp.Properties.RemoveNamedProperty("ForcedFireLinks"); // Not sure what this is. // Transient so it's not serialized


                    var newExposedCoverPackedProperties = new ArrayProperty<IntProperty>("ExposedCoverPackedProperties");
                    var newDangerCoverPackedProperties = new ArrayProperty<IntProperty>("CoverTurnTargetPackedProperties");


                    sp.Properties.Insert(1, newFireLinks);

                    sp.Properties.Insert(3, newExposedCoverPackedProperties);
                    sp.Properties.Insert(4, newDangerCoverPackedProperties);
                    sp.Properties.Insert(5, new ArrayProperty<StructProperty>("SlipTarget")); // We will not have anything to populate this with.

                    newStruct.Properties.Insert(12, new IntProperty(0, "TurnTargetPackedProperties"));  // We will not have anything to populate this with.
                    newStruct.Properties.Insert(13, new IntProperty(0, "CoverTurnTargetPackedProperties")); // We will not have anything to populate this with.


                    newStruct.Properties.Insert(28, new BoolProperty(false, "bCanCoverTurn_Left")); // Index needs validated
                    newStruct.Properties.Insert(29, new BoolProperty(false, "bCanCoverTurn_Right"));

                    newStruct.Properties.Insert(36, new BoolProperty(false, "bAllowCoverTurn"));
                    newStruct.Properties.Insert(39, new BoolProperty(false, "bUnSafeCover"));



                    newProp.Add(newStruct);
                }

                return newProp;
            }
            return null;
        }
    }
}
