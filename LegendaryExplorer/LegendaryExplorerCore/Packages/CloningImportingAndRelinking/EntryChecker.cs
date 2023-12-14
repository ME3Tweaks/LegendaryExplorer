using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Localization;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Packages.CloningImportingAndRelinking
{

    public class ReferenceCheckPackage
    {
        // The list of generated warnings, errors, and blocking errors
        private List<EntryStringPair> BlockingErrors { get; } = new();
        private List<EntryStringPair> SignificantIssues { get; } = new();
        private List<EntryStringPair> InfoWarnings { get; } = new();

        private object syncLock = new object();

        public IReadOnlyCollection<EntryStringPair> GetBlockingErrors() => BlockingErrors;
        public IReadOnlyCollection<EntryStringPair> GetSignificantIssues() => SignificantIssues;
        public IReadOnlyCollection<EntryStringPair> GetInfoWarnings() => InfoWarnings;

        public void AddBlockingError(string message, IEntry entry = null)
        {
            lock (syncLock)
            {
                BlockingErrors.Add(new EntryStringPair(entry, message));
            }
        }

        public void AddBlockingError(string message, LEXOpenable entry)
        {
            lock (syncLock)
            {
                BlockingErrors.Add(new EntryStringPair(entry, message));
            }
        }

        public void AddSignificantIssue(string message, IEntry entry = null)
        {
            lock (syncLock)
            {
                SignificantIssues.Add(new EntryStringPair(entry, message));
            }
        }

        public void AddSignificantIssue(string message, LEXOpenable entry)
        {
            lock (syncLock)
            {
                SignificantIssues.Add(new EntryStringPair(entry, message));
            }
        }

        public void AddInfoWarning(string message, IEntry entry = null)
        {
            lock (syncLock)
            {
                InfoWarnings.Add(new EntryStringPair(entry, message));
            }
        }

        public void AddInfoWarning(string message, LEXOpenable entry)
        {
            lock (syncLock)
            {
                InfoWarnings.Add(new EntryStringPair(entry, message));
            }
        }


        public void ClearMessages()
        {
            BlockingErrors.Clear();
            SignificantIssues.Clear();
            InfoWarnings.Clear();
        }
    }

    /// <summary>
    /// Checks property types against the database to determine if property types are of the correct typing.
    /// </summary>
    public class EntryChecker
    {

        /// <summary>
        /// Checks object and name references for invalid values and if values are of the incorrect typing. Returns localized messages, if you do not want localized messages, pass it the NonLocalizedStringConverter delegate from this class.
        /// </summary>
        /// <param name="item"></param>
        public static void CheckReferences(ReferenceCheckPackage item, string basePath, LECLocalizationShim.GetLocalizedStringDelegate localizationDelegate, Action<string> statusUpdateDelegate, Action<string> logMessageDelegate = null, List<string> referencedFiles = null, CancellationTokenSource cts = null)
        {
            referencedFiles ??= Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Where(x => x.RepresentsPackageFilePath()).ToList();
            int numChecked = 0;
            Parallel.ForEach(referencedFiles,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Min(3, Environment.ProcessorCount)
                },
                f =>
                {
                    if (cts != null && cts.IsCancellationRequested)
                        return;
                    //if (!f.Contains("BioA_Nor")) return;
                    var lnumChecked = Interlocked.Increment(ref numChecked);
                    statusUpdateDelegate?.Invoke(localizationDelegate(LECLocalizationShim.string_checkingNameAndObjectReferences) + $@" [{lnumChecked - 1}/{referencedFiles.Count}]");

                    var relativePath = f.Substring(basePath.Length + 1);
                    logMessageDelegate?.Invoke($@"Checking package and name references in {relativePath}");
                    var package = MEPackageHandler.OpenMEPackage(f, forceLoadFromDisk: true);
                    CheckReferences(item, package, localizationDelegate, relativePath, cts);
                });
        }

        public static void CheckReferences(ReferenceCheckPackage item, IMEPackage package, LECLocalizationShim.GetLocalizedStringDelegate localizationDelegate, string relativePath = null, CancellationTokenSource cts = null)
        {
            string fName = Path.GetFileName(package.FilePath);
            foreach (ExportEntry exp in package.Exports)
            {
                if (cts != null && cts.IsCancellationRequested)
                    return;

                // Has to be done before accessing the name because it will cause infinite crash loop
                //Debug.WriteLine($"Checking {exp.UIndex} {exp.InstancedFullPath} in {exp.FileRef.FilePath}");
                if (exp.idxLink == exp.UIndex)
                {
                    item.AddBlockingError(localizationDelegate(LECLocalizationShim.string_interp_fatalExportCircularReference, relativePath ?? fName, exp.UIndex));
                    continue;
                }

                var prefix = localizationDelegate(LECLocalizationShim.string_interp_warningGenericExportPrefix, relativePath ?? fName, exp.UIndex, exp.ObjectName.Name, exp.ClassName);
                try
                {
                    checkName(item, localizationDelegate, () => exp.ObjectName, "Object Name", $"export {exp.UIndex}", relativePath, fName, exp);

                    if (exp.idxArchetype != 0 && !package.IsEntry(exp.idxArchetype))
                    {
                        item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningArchetypeOutsideTables, prefix, exp.idxArchetype), exp);
                    }

                    if (exp.idxSuperClass != 0 && !package.IsEntry(exp.idxSuperClass))
                    {
                        item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningSuperclassOutsideTables, prefix, exp.idxSuperClass), exp);
                    }

                    if (exp.idxClass != 0 && !package.IsEntry(exp.idxClass))
                    {
                        item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningClassOutsideTables, prefix, exp.idxClass), exp);
                    }

                    if (exp.idxLink != 0 && !package.IsEntry(exp.idxLink))
                    {
                        item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningLinkOutsideTables, prefix, exp.idxLink), exp);
                    }



                    if (exp.HasComponentMap)
                    {
                        foreach (var c in exp.ComponentMap)
                        {
                            if (c.Value != 0 && !package.IsEntry(c.Value))
                            {
                                // Can components point to 0? I don't think so
                                item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningComponentMapItemOutsideTables, prefix, c.Value), exp);
                            }
                        }
                    }

                    //find stack references
                    if (exp.HasStack && exp.DataReadOnly is var data)
                    {
                        var stack1 = EndianReader.ToInt32(data, 0, exp.FileRef.Endian);
                        var stack2 = EndianReader.ToInt32(data, 4, exp.FileRef.Endian);
                        if (stack1 != 0 && !package.IsEntry(stack1))
                        {
                            item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningExportStackElementOutsideTables, prefix, 0, stack1), exp);
                        }

                        if (stack2 != 0 && !package.IsEntry(stack2))
                        {
                            item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningExportStackElementOutsideTables, prefix, 1, stack2), exp);
                        }
                    }
                    else if (exp.TemplateOwnerClassIdx is var toci and >= 0)
                    {
                        var TemplateOwnerClassIdx = EndianReader.ToInt32(exp.DataReadOnly, toci, exp.FileRef.Endian);
                        if (TemplateOwnerClassIdx != 0 && !package.IsEntry(TemplateOwnerClassIdx))
                        {
                            item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningTemplateOwnerClassOutsideTables, prefix, toci.ToString(@"X6"), TemplateOwnerClassIdx), exp);
                        }
                    }

                    var props = exp.GetProperties();
                    foreach (var p in props)
                    {
                        recursiveCheckProperty(item, localizationDelegate, relativePath, exp.ClassName, exp, p);
                    }
                }
                catch (Exception e)
                {
                    item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningExceptionParsingProperties, prefix, e.Message), exp);
                    continue;
                }

                //find binary references
                try
                {
                    if (!exp.IsDefaultObject && ObjectBinary.From(exp) is ObjectBinary objBin)
                    {
                        List<int> indices = objBin.GetUIndexes(exp.FileRef.Game);
                        foreach (int uIndex in indices)
                        {
                            if (uIndex != 0 && !exp.FileRef.IsEntry(uIndex))
                            {
                                item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningBinaryReferenceOutsideTables, prefix, uIndex), exp);
                            }
                            else if (exp.FileRef.GetEntry(uIndex)?.ClassName == @"Package" && exp.FileRef.GetEntry(uIndex)?.ObjectName.ToString() == @"Trash")
                            {
                                item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningBinaryReferenceTrashed, prefix, uIndex), exp);
                            }
                            else if (exp.FileRef.GetEntry(uIndex)?.ObjectName.ToString() == UnrealPackageFile.TrashPackageName)
                            {
                                item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningBinaryReferenceTrashed, prefix, uIndex), exp);
                            }
                        }

                        var nameIndicies = objBin.GetNames(exp.FileRef.Game);
                        foreach (var ni in nameIndicies)
                        {
                            if (ni.Item1 == "")
                            {
                                item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningBinaryNameReferenceOutsideNameTable, prefix), exp);
                            }
                        }
                    }
                }
                catch (Exception e) /* when (!App.IsDebug)*/
                {
                    item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningUnableToParseBinary, prefix, e.Message), exp);
                }
            }

            foreach (ImportEntry imp in package.Imports)
            {
                if (imp.idxLink != 0 && !package.TryGetEntry(imp.idxLink, out _))
                {
                    item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningImportLinkOutideOfTables, relativePath ?? fName, imp.UIndex, imp.idxLink), imp);
                }
                else if (imp.idxLink == imp.UIndex)
                {
                    item.AddBlockingError(localizationDelegate(LECLocalizationShim.string_interp_fatalImportCircularReference, relativePath ?? fName, imp.UIndex), imp);
                }

                // Values check
                checkName(item, localizationDelegate, () => imp.PackageFile, "Package file", $"import {imp.UIndex}", relativePath, fName, imp);
                checkName(item, localizationDelegate, () => imp.ClassName, "Class name", $"import {imp.UIndex}", relativePath, fName, imp);
            }
        }

        private static void checkName(ReferenceCheckPackage item, LECLocalizationShim.GetLocalizedStringDelegate localizationDelegate,
            Func<string> getName, string nameBeingChecked, string itemBeingChecked, string relativePath, string fName, IEntry entry)
        {
            try
            {
                // Can't access idx vars so we have to do this
                var pf = getName();
            }
            catch (Exception)
            {

                item.AddBlockingError(localizationDelegate(LECLocalizationShim.string_interp_refCheckInvalidNameValue, relativePath ?? fName, nameBeingChecked, itemBeingChecked), entry);
            }
        }

        private static void recursiveCheckProperty(ReferenceCheckPackage item, LECLocalizationShim.GetLocalizedStringDelegate localizationDelegate, string relativePath, string containingClassOrStructName, IEntry entry, Property property)
        {
            var prefix = localizationDelegate(LECLocalizationShim.string_interp_warningPropertyTypingWrongPrefix, relativePath, entry.UIndex, entry.ObjectName.Name, entry.ClassName, property.StartOffset.ToString(@"X6"));
            if (property is UnknownProperty up)
            {
                item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningFoundBrokenPropertyData, prefix), entry);

            }
            else if (property is ObjectProperty op)
            {
                bool validRef = true;
                if (op.Value > 0 && op.Value > entry.FileRef.ExportCount)
                {
                    //bad
                    if (op.Name.Name != null)
                    {
                        item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningReferenceNotInExportTable, prefix, op.Name.Name, op.Value), entry);
                        validRef = false;
                    }
                    else
                    {
                        item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_nested_warningReferenceNoInExportTable, prefix, op.Value), entry);
                        validRef = false;
                    }
                }
                else if (op.Value < 0 && Math.Abs(op.Value) > entry.FileRef.ImportCount)
                {
                    //bad
                    if (op.Name.Name != null)
                    {
                        item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningReferenceNotInImportTable, prefix, op.Name.Name, op.Value), entry);
                        validRef = false;

                    }
                    else
                    {
                        item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_nested_warningReferenceNoInImportTable, prefix, op.Value), entry);
                        validRef = false;

                    }
                }
                else if (op.Value != 0 && entry.FileRef.GetEntry(op.Value).ClassName == "Package" &&
                         (entry.FileRef.GetEntry(op.Value)?.ObjectName.ToString() == @"Trash" ||
                          entry.FileRef.GetEntry(op.Value)?.ObjectName.ToString() ==
                          UnrealPackageFile.TrashPackageName))
                {
                    item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_nested_warningTrashedExportReference, prefix, op.Value), entry);
                    validRef = false;
                }

                // Check object is of correct typing?
                if (validRef && op.Value != 0)
                {
                    var referencedEntry = op.ResolveToEntry(entry.FileRef);
                    if (referencedEntry.FullPath.Equals(@"SFXGame.BioDeprecated", StringComparison.InvariantCulture)) return; //This will appear as wrong even though it's technically not
                    if (entry.FileRef.Game == MEGame.ME2)
                    {
                        if (op.Name == "m_oAreaMap" && referencedEntry.ClassName == @"BioSWF") return; //This will appear as wrong even though it's technically not (deprecated leftover)
                        if (op.Name == "AIController" && referencedEntry.ObjectName.Name.StartsWith("BioAI_")) return; //These are all deprecated. A few things use them still but the inheritance is wrong
                        if (op.Name == "TrackingSound" && entry.ClassName == "SFXSeqAct_SecurityCam" && referencedEntry.ClassName == "WwiseEvent") return; // Appears to be incorrect in vanilla. Don't report it as an issue
                    }

                    var propInfo = GlobalUnrealObjectInfo.GetPropertyInfo(entry.Game, op.Name, containingClassOrStructName, containingExport: entry as ExportEntry);
                    var customClassInfos = new Dictionary<string, ClassInfo>();

                    if (referencedEntry.ClassName == @"Class" && op.Value > 0)
                    {

                        // Make sure we have info about this class.
                        var lookupEnt = referencedEntry as ExportEntry;
                        while (lookupEnt != null && lookupEnt.IsClass && !GlobalUnrealObjectInfo.GetClasses(entry.FileRef.Game).ContainsKey(lookupEnt.ObjectName))
                        {
                            // Needs dynamically generated
                            var cc = GlobalUnrealObjectInfo.generateClassInfo(lookupEnt);
                            customClassInfos[lookupEnt.ObjectName] = cc;
                            lookupEnt = lookupEnt.Parent as ExportEntry;
                        }

                        // If we did not pull it previously, we should try again with our custom info.
                        if (propInfo == null && customClassInfos.Any())
                        {
                            propInfo = GlobalUnrealObjectInfo.GetPropertyInfo(entry.Game, op.Name,
                                containingClassOrStructName, customClassInfos[referencedEntry.ObjectName],
                                containingExport: entry as ExportEntry);
                        }
                    }

                    if (propInfo != null && propInfo.Reference != null)
                    {
                        // We can't resolve if an object inherits from a class object that's only defined in native.
                        // This is only possible if the reference is an import and it's importing native class object.
                        // Like Engine.CodecBinkMovie
                        if (!referencedEntry.IsAKnownNativeClass())
                        {
                            if (referencedEntry.ClassName == @"Class")
                            {
                                // Inherits
                                if (!referencedEntry.InheritsFrom(propInfo.Reference, customClassInfos /*, (entry as ExportEntry)?.SuperClassName) */))
                                {
                                    if (op.Name.Name != null)
                                    {
                                        item.AddSignificantIssue(localizationDelegate(
                                            LECLocalizationShim.string_interp_warningWrongPropertyTypingWrongMessage, prefix,
                                            op.Name.Name, op.Value, op.ResolveToEntry(entry.FileRef).InstancedFullPath,
                                            propInfo.Reference, referencedEntry.ClassName), entry);
                                    }
                                    else
                                    {
                                        item.AddSignificantIssue(localizationDelegate(
                                            LECLocalizationShim
                                                .string_interp_nested_warningWrongClassPropertyTypingWrongMessage,
                                            prefix, op.Value, op.ResolveToEntry(entry.FileRef).InstancedFullPath,
                                            propInfo.Reference, referencedEntry.ClassName), entry);
                                    }
                                }
                            }
                            else if (!referencedEntry.IsA(propInfo.Reference, customClassInfos))
                            {
                                // Is instance of
                                if (op.Name.Name != null)
                                {
                                    item.AddSignificantIssue(localizationDelegate(
                                        LECLocalizationShim.string_interp_warningWrongObjectPropertyTypingWrongMessage,
                                        prefix, op.Name.Name, op.Value,
                                        op.ResolveToEntry(entry.FileRef).InstancedFullPath, propInfo.Reference,
                                        referencedEntry.ClassName), entry);
                                }
                                else
                                {
                                    item.AddSignificantIssue(localizationDelegate(
                                        LECLocalizationShim.string_interp_nested_warningWrongObjectPropertyTypingWrongMessage,
                                        prefix, op.Value, op.ResolveToEntry(entry.FileRef).InstancedFullPath,
                                        propInfo.Reference, referencedEntry.ClassName), entry);
                                }
                            }
                        }
                    }
                }
            }
            else if (property is ArrayProperty<ObjectProperty> aop)
            {
                foreach (var p in aop)
                {
                    recursiveCheckProperty(item, localizationDelegate, relativePath, aop.Name, entry, p);
                }
            }
            else if (property is StructProperty sp)
            {
                foreach (var p in sp.Properties)
                {
                    recursiveCheckProperty(item, localizationDelegate, relativePath, sp.StructType, entry, p);
                }
            }
            else if (property is ArrayProperty<StructProperty> asp)
            {
                foreach (var p in asp)
                {
                    recursiveCheckProperty(item, localizationDelegate, relativePath, p.StructType, entry, p);
                }
            }
            else if (property is DelegateProperty dp)
            {
                if (dp.Value.ContainingObjectUIndex != 0 && !entry.FileRef.IsEntry(dp.Value.ContainingObjectUIndex))
                {
                    item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_warningDelegatePropertyIsOutsideOfExportTable, prefix, dp.Name.Name), entry);
                }
            } else if (property is NameProperty np)
            {
                if (np.Value.Name == "") // Failed to resolve
                {
                    item.AddSignificantIssue(localizationDelegate(LECLocalizationShim.string_interp_invalidNameIndexonNameProperty, prefix, property.Name.Instanced), entry);
                }
            }
        }

        /// <summary>
        /// Returns a list of duplicate indexes in a package file. Trash exports are ignored.
        /// </summary>
        /// <param name="Pcc">Package file to check against</param>
        /// <returns>A list of <see cref="EntryStringPair"/> objects that detail the second or further duplicate. If this list is empty, there are no duplicates detected.</returns>
        public static List<EntryStringPair> CheckForDuplicateIndices(IMEPackage Pcc)
        {
            var duplicates = new List<EntryStringPair>();
            var duplicatesPackagePathIndexMapping = new Dictionary<string, List<int>>();
            foreach (ExportEntry exp in Pcc.Exports)
            {
                string key = exp.InstancedFullPath;
                if (key.StartsWith(UnrealPackageFile.TrashPackageName))
                    continue; //Do not report these as requiring re-indexing.
                if (!duplicatesPackagePathIndexMapping.TryGetValue(key, out List<int> indexList))
                {
                    indexList = new List<int>();
                    duplicatesPackagePathIndexMapping[key] = indexList;
                }
                else
                {
                    duplicates.Add(new EntryStringPair(exp,
                        $"{exp.UIndex} {exp.InstancedFullPath} has duplicate index (index value {exp.indexValue})"));
                }

                indexList.Add(exp.UIndex);
            }

            // IMPORTS TOO
            foreach (ImportEntry imp in Pcc.Imports)
            {
                string key = imp.InstancedFullPath;
                if (key.StartsWith(UnrealPackageFile.TrashPackageName))
                    continue; //Do not report these as requiring re-indexing.
                if (!duplicatesPackagePathIndexMapping.TryGetValue(key, out List<int> indexList))
                {
                    indexList = new List<int>();
                    duplicatesPackagePathIndexMapping[key] = indexList;
                }
                else
                {
                    duplicates.Add(new EntryStringPair(imp, $"{imp.UIndex} {imp.InstancedFullPath} has duplicate index (index value {imp.indexValue})"));
                }

                indexList.Add(imp.UIndex);
            }

            return duplicates;
        }
    }
}
