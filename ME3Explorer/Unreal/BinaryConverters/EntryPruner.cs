using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public static class EntryPruner
    {
        public static void TrashEntries(IMEPackage pcc, IEnumerable<IEntry> itemsToTrash)
        {
            ExportEntry trashTopLevel = pcc.Exports.FirstOrDefault(x => x.idxLink == 0 && x.ObjectName == UnrealPackageFile.TrashPackageName);
            ImportEntry packageImport = pcc.Imports.FirstOrDefault(x => x.GetFullPath == "Core.Package");
            if (packageImport == null)
            {
                ImportEntry coreImport = pcc.Imports.FirstOrDefault(x => x.GetFullPath == "Core");
                if (coreImport == null)
                {
                    //really small file
                    coreImport = new ImportEntry(pcc)
                    {
                        idxObjectName = pcc.FindNameOrAdd("Core"),
                        idxClassName = pcc.FindNameOrAdd("Package"),
                        idxLink = 0,
                        idxPackageFile = pcc.FindNameOrAdd("Core")
                    };
                    pcc.addImport(coreImport);
                }

                //Package isn't an import, could be one of the 2DA files or other small ones
                packageImport = new ImportEntry(pcc)
                {
                    idxObjectName = pcc.FindNameOrAdd("Package"),
                    idxClassName = pcc.FindNameOrAdd("Class"),
                    idxLink = coreImport.UIndex,
                    idxPackageFile = pcc.FindNameOrAdd("Core")
                };
                pcc.addImport(packageImport);
            }

            foreach (IEntry entry in itemsToTrash)
            {
                if (entry == trashTopLevel || entry.ObjectName == "Trash") //don't trash what's already been trashed
                {
                    continue;
                }
                trashTopLevel = TrashEntry(entry, trashTopLevel, packageImport.UIndex);
            }
            pcc.RemoveTrailingTrash();
        }

        /// <summary>
        /// Trashes an entry.
        /// </summary>
        /// <param name="entry">Entry to trash</param>
        /// <param name="trashContainer">Container for trash. Pass null if you want to create the trash container from the passed in value.</param>
        /// <param name="packageClassIdx">Idx for package class. Prevents multiple calls to find it</param>
        /// <returns>New trash container, otherwise will be null</returns>
        public static ExportEntry TrashEntry(IEntry entry, ExportEntry trashContainer, int packageClassIdx)
        {
            IMEPackage pcc = entry.FileRef;
            if (entry is ImportEntry imp)
            {
                if (trashContainer == null)
                {
                    trashContainer = TrashEntry(new ExportEntry(pcc), null, packageClassIdx);
                    pcc.addExport(trashContainer);
                    trashContainer.indexValue = 0;
                }
                imp.idxClassName = pcc.FindNameOrAdd("Package");
                imp.idxPackageFile = pcc.FindNameOrAdd("Core");
                imp.idxLink = trashContainer.UIndex;
                imp.idxObjectName = pcc.FindNameOrAdd("Trash");
                imp.indexValue = 0;
            }
            else if (entry is ExportEntry exp)
            {
                MemoryStream trashData = new MemoryStream();
                trashData.WriteInt32(-1);
                trashData.WriteInt32(pcc.findName("None"));
                trashData.WriteInt32(0);
                exp.Data = trashData.ToArray();
                exp.idxArchtype = 0;
                exp.idxSuperClass = 0;
                exp.indexValue = 0;
                exp.idxClass = packageClassIdx;
                exp.ObjectFlags &= ~UnrealFlags.EObjectFlags.HasStack;
                if (trashContainer == null)
                {
                    exp.idxObjectName = pcc.FindNameOrAdd(UnrealPackageFile.TrashPackageName);
                    exp.idxLink = 0;
                    if (exp.idxLink == exp.UIndex)
                    {
                        Debugger.Break();
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
                    exp.idxObjectName = pcc.FindNameOrAdd("Trash");
                    exp.PackageGUID = Guid.Empty;
                }
            }
            return trashContainer;
        }

        public static void TrashIncompatibleEntries(MEPackage pcc, MEGame oldGame, MEGame newGame)
        {
            var entries = new EntryCollection(pcc);
            var oldClasses = UnrealObjectInfo.GetClasses(oldGame);
            var newClasses = UnrealObjectInfo.GetClasses(newGame);
            var classesToRemove = oldClasses.Keys.Except(newClasses.Keys).ToHashSet();
            foreach (IEntry entry in entries)
            {
                if (classesToRemove.Contains(entry.ClassName) || (entry.ClassName == "Class" && classesToRemove.Contains(entry.ObjectName)))
                {
                    TrashEntries(pcc, entries.FlattenTree(entry.UIndex));
                }
            }
        }

        public static PropertyCollection RemoveIncompatibleProperties(IMEPackage sourcePcc, PropertyCollection props, string typeName, MEGame newGame)
        {
            var infoProps = UnrealObjectInfo.GetAllProperties(newGame, typeName);

            var newProps = new PropertyCollection();
            foreach (UProperty prop in props)
            {
                if (infoProps.ContainsKey(prop.Name))
                {
                    switch (prop)
                    {
                        case ArrayProperty<DelegateProperty> adp:
                            //don't think these exist? if they do, delete them
                            break;
                        case ArrayProperty<EnumProperty> aep:
                            if (UnrealObjectInfo.GetEnumValues(newGame, aep.Reference) is List<NameReference> enumValues)
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
                            break;
                        case ArrayProperty<ObjectProperty> asp:
                            for (int i = asp.Count - 1; i >= 0; i--)
                            {
                                if (asp[i].Value == 0 || sourcePcc.getEntry(asp[i].Value) is IEntry entry && !entry.GetFullPath.StartsWith(UnrealPackageFile.TrashPackageName))
                                {
                                    continue;
                                }
                                //delete if it references a trashed entry or if value is invalid
                                asp.RemoveAt(i);
                            }
                            newProps.Add(asp);
                            break;
                        case ArrayProperty<StructProperty> asp:
                            if (UnrealObjectInfo.GetStructs(newGame).ContainsKey(asp.Reference))
                            {
                                if (HasIncompatibleImmutabilities(asp.Reference, out bool newImmutability)) break;
                                foreach (StructProperty structProperty in asp)
                                {
                                    structProperty.Properties = RemoveIncompatibleProperties(sourcePcc, structProperty.Properties, structProperty.StructType, newGame);
                                    structProperty.IsImmutable = newImmutability;
                                }
                                newProps.Add(asp);
                            }
                            break;
                        case DelegateProperty delegateProperty:
                            //script related, so just delete it.
                            break;
                        case EnumProperty enumProperty:
                            if (UnrealObjectInfo.GetEnumValues(newGame, enumProperty.EnumType) is List<NameReference> values)
                            {
                                if (!values.Contains(enumProperty.Value))
                                {
                                    enumProperty.Value = values.First(); //hope that the first value is a reasonable default
                                }
                                newProps.Add(enumProperty);
                            }
                            break;
                        case ObjectProperty objectProperty:
                        {
                            if (objectProperty.Value == 0 || sourcePcc.getEntry(objectProperty.Value) is IEntry entry && !entry.GetFullPath.StartsWith(UnrealPackageFile.TrashPackageName))
                            {
                                newProps.Add(objectProperty);
                            }
                            break;
                        }
                        case StructProperty structProperty:
                            string structType = structProperty.StructType;
                            if (UnrealObjectInfo.GetStructs(newGame).ContainsKey(structType))
                            {
                                if (HasIncompatibleImmutabilities(structType, out bool newImmutability)) break;
                                structProperty.Properties = RemoveIncompatibleProperties(sourcePcc, structProperty.Properties, structType, newGame);
                                structProperty.IsImmutable = newImmutability;
                                newProps.Add(structProperty);
                            }
                            break;
                        default:
                            newProps.Add(prop);
                            break;
                    }
                }
            }

            return newProps;

            bool HasIncompatibleImmutabilities(string structType, out bool newImmutability)
            {
                bool sourceIsImmutable = UnrealObjectInfo.IsImmutable(structType, sourcePcc.Game);
                newImmutability = UnrealObjectInfo.IsImmutable(structType, newGame);
                
                if (sourceIsImmutable && newImmutability && !UnrealObjectInfo.GetClassOrStructInfo(sourcePcc.Game, structType).properties
                                                                             .SequenceEqual(UnrealObjectInfo.GetClassOrStructInfo(newGame, structType).properties))
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

    public class EntryCollection : IEnumerable<IEntry>
    {
        public EntryCollection(IMEPackage pcc)
        {
            imports = pcc.Imports.Select(import => new TreeNode<IEntry, int>(import)).ToList();
            exports = pcc.Exports.Select(export => new TreeNode<IEntry, int>(export)).ToList();

            foreach (TreeNode<IEntry, int> node in exports.Concat(imports))
            {
                this[node.Data.idxLink]?.Children.Add(node.Data.UIndex);
            }
        }

        private readonly List<TreeNode<IEntry, int>> imports;
        private readonly List<TreeNode<IEntry, int>> exports;

        public TreeNode<IEntry, int> this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return null;
                }

                if (index < 0)
                {
                    index = -index - 1;
                    return imports[index];
                }

                return exports[index - 1];
            }
        }

        public IEnumerable<IEntry> GetDirectChildren(int uIndex)
        {
            foreach (int i in this[uIndex])
            {
                yield return this[i].Data;
            }
        }

        public List<IEntry> FlattenTree(int uIndex)
        {
            var entries = new List<IEntry> {this[uIndex].Data};
            foreach (int i in this[uIndex])
            {
                entries.AddRange(FlattenTree(i));
            }

            return entries;
        }

        public IEntry GetEntry(int uIndex)
        {
            return this[uIndex].Data;
        }

        public IEnumerator<IEntry> GetEnumerator()
        {
            foreach (TreeNode<IEntry, int> node in exports)
            {
                yield return node.Data;
            }
            foreach (TreeNode<IEntry, int> node in imports)
            {
                yield return node.Data;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
