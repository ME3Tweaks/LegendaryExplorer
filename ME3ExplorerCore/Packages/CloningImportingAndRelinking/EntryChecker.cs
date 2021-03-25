using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ME3ExplorerCore.Gammtek.IO;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3ExplorerCore.Packages.CloningImportingAndRelinking
{

    public class ReferenceCheckPackage
    {
        // The list of generated warnings, errors, and blocking errors
        private List<string> BlockingErrors = new List<string>();
        private List<string> SignificantIssues = new List<string>();
        private List<string> InfoWarnings = new List<string>();

        private object syncLock = new object();
        public void AddBlockingError(string message)
        {
            lock (syncLock)
            {
                BlockingErrors.Add(message);
            }
        }

        public void AddSignificantIssue(string message)
        {
            lock (syncLock)
            {
                SignificantIssues.Add(message);
            }
        }

        public void AddInfoWarning(string message)
        {
            lock (syncLock)
            {
                InfoWarnings.Add(message);
            }
        }
    }

    #region LOCALIZATION TABLE (for supporting M3 + ME3Exp)
    class M3L
    {
        public static string GetString(string key, params object[] parms)
        {
            // Basic non-localized converter
            switch (key)
            {
                case "string_interp_warningPropertyTypingWrongPrefix":
                    return "";
            }

            return $"ERROR! STRING KEYa NOT IN LOCALIZATION TABLE: {key}";
        }

        // DO NOT CHANGE THESE KEYS
        // THEY ARE IDENTICAL TO THE ONES IN M3
        // CHANGING THEM WILL CAUSE M3 LOCALIZATION FOR STRING TO FAIL
        internal const string string_interp_warningTemplateOwnerClassOutsideTables = "string_interp_warningTemplateOwnerClassOutsideTables";
        internal const string string_checkingNameAndObjectReferences = "string_checkingNameAndObjectReferences";
        internal const string string_interp_fatalExportCircularReference = "string_interp_fatalExportCircularReference";
        internal const string string_interp_warningArchetypeOutsideTables = "string_interp_warningArchetypeOutsideTables";
        internal const string string_interp_warningGenericExportPrefix = "string_interp_warningGenericExportPrefix";
        internal const string string_interp_warningSuperclassOutsideTables = "string_interp_warningSuperclassOutsideTables";
        internal const string string_interp_warningClassOutsideTables = "string_interp_warningClassOutsideTables";
        internal const string string_interp_warningLinkOutsideTables = "string_interp_warningLinkOutsideTables";
        internal const string string_interp_warningComponentMapItemOutsideTables = "string_interp_warningComponentMapItemOutsideTables";
        internal const string string_interp_warningExportStackElementOutsideTables = "string_interp_warningExportStackElementOutsideTables";
        internal const string string_interp_warningExceptionParsingProperties = "string_interp_warningExceptionParsingProperties";
        internal const string string_interp_warningBinaryReferenceOutsideTables = "string_interp_warningBinaryReferenceOutsideTables";
        internal const string string_interp_warningBinaryReferenceTrashed = "string_interp_warningBinaryReferenceTrashed";
        internal const string string_interp_warningBinaryNameReferenceOutsideNameTable = "string_interp_warningBinaryNameReferenceOutsideNameTable";
        internal const string string_interp_warningUnableToParseBinary = "string_interp_warningUnableToParseBinary";
        internal const string string_interp_warningImportLinkOutideOfTables = "string_interp_warningImportLinkOutideOfTables";
        internal const string string_interp_fatalImportCircularReference = "string_interp_fatalImportCircularReference";
        internal const string string_interp_warningPropertyTypingWrongPrefix = "string_interp_warningPropertyTypingWrongPrefix";
        internal const string string_interp_warningFoundBrokenPropertyData = "string_interp_warningFoundBrokenPropertyData";
        internal const string string_interp_warningReferenceNotInExportTable = "string_interp_warningReferenceNotInExportTable";
        internal const string string_interp_nested_warningReferenceNoInExportTable = "string_interp_nested_warningReferenceNoInExportTable";

        internal const string string_interp_warningReferenceNotInImportTable = "string_interp_warningReferenceNotInImportTable";
        internal const string string_interp_nested_warningReferenceNoInImportTable = "string_interp_nested_warningReferenceNoInImportTable";
        internal const string string_interp_nested_warningTrashedExportReference = "string_interp_nested_warningTrashedExportReference";
        internal const string string_interp_warningWrongPropertyTypingWrongMessage = "string_interp_warningWrongPropertyTypingWrongMessage";
        internal const string string_interp_nested_warningWrongClassPropertyTypingWrongMessage = "string_interp_nested_warningWrongClassPropertyTypingWrongMessage";
        internal const string string_interp_warningWrongObjectPropertyTypingWrongMessage = "string_interp_warningWrongObjectPropertyTypingWrongMessage";
        internal const string string_interp_nested_warningWrongObjectPropertyTypingWrongMessage = "string_interp_nested_warningWrongObjectPropertyTypingWrongMessage";
        internal const string string_interp_warningDelegatePropertyIsOutsideOfExportTable = "string_interp_warningDelegatePropertyIsOutsideOfExportTable";
    }

    #endregion

    /// <summary>
    /// Ported from ME3Tweaks Mod Manager
    /// Checks property types against the database to determine if property types are of the correct typing.
    /// </summary>
    public class EntryChecker
    {
        /// <summary>
        /// Non-localized text converter
        /// </summary>
        /// <param name="key"></param>
        /// <param name="parms"></param>
        /// <returns></returns>
        public static string NonLocalizedStringConveter(string key, params string[] parms)
        {
            return "OK";
        }

        public delegate string GetLocalizedStringDelegate(string str, params string[] parms);

        /// <summary>
        /// Checks object and name references for invalid values and if values are of the incorrect typing. Returns localized messages, if you do not want localized messages, pass it the NonLocalizedStringConverter delegate from this class.
        /// </summary>
        /// <param name="item"></param>
        public static void CheckReferences(ReferenceCheckPackage item, string basePath, ref bool CheckCancelled, GetLocalizedStringDelegate localizationDelegate, Action<string> statusUpdateDelegate, Action<string> logMessageDelegate = null, List<string> referencedFiles = null)
        {
            referencedFiles ??= Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories).Where(x => x.RepresentsPackageFilePath()).ToList();
            int numChecked = 0;

            Parallel.ForEach(referencedFiles,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = Math.Min(3, Environment.ProcessorCount)
                },
                f =>
                //foreach (var f in referencedFiles)
                {
                    //if (CheckCancelled) return;

                    var lnumChecked = Interlocked.Increment(ref numChecked);
                    statusUpdateDelegate?.Invoke(localizationDelegate(M3L.string_checkingNameAndObjectReferences) + $@" [{lnumChecked - 1}/{referencedFiles.Count}]");

                    var relativePath = f.Substring(basePath.Length + 1);
                    logMessageDelegate?.Invoke($@"Checking package and name references in {relativePath}");
                    var package = MEPackageHandler.OpenMEPackage(f, forceLoadFromDisk: true);
                    foreach (ExportEntry exp in package.Exports)
                    {
                        // Has to be done before accessing the name because it will cause infinite crash loop
                        //Debug.WriteLine($"Checking {exp.UIndex} {exp.InstancedFullPath} in {exp.FileRef.FilePath}");
                        if (exp.idxLink == exp.UIndex)
                        {
                            item.AddBlockingError(M3L.GetString(M3L.string_interp_fatalExportCircularReference, f.Substring(basePath.Length + 1), exp.UIndex));
                            continue;
                        }

                        var prefix = M3L.GetString(M3L.string_interp_warningGenericExportPrefix, f.Substring(basePath.Length + 1), exp.UIndex, exp.ObjectName.Name, exp.ClassName);
                        try
                        {
                            if (exp.idxArchetype != 0 && !package.IsEntry(exp.idxArchetype))
                            {
                                item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningArchetypeOutsideTables, prefix, exp.idxArchetype));
                            }

                            if (exp.idxSuperClass != 0 && !package.IsEntry(exp.idxSuperClass))
                            {
                                item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningSuperclassOutsideTables, prefix, exp.idxSuperClass));
                            }

                            if (exp.idxClass != 0 && !package.IsEntry(exp.idxClass))
                            {
                                item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningClassOutsideTables, prefix, exp.idxClass));
                            }

                            if (exp.idxLink != 0 && !package.IsEntry(exp.idxLink))
                            {
                                item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningLinkOutsideTables, prefix, exp.idxLink));
                            }

                            if (exp.HasComponentMap)
                            {
                                foreach (var c in exp.ComponentMap)
                                {
                                    if (!package.IsEntry(c.Value))
                                    {
                                        // Can components point to 0? I don't think so
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningComponentMapItemOutsideTables, prefix, c.Value));
                                    }
                                }
                            }

                            //find stack references
                            if (exp.HasStack && exp.Data is byte[] data)
                            {
                                var stack1 = EndianReader.ToInt32(data, 0, exp.FileRef.Endian);
                                var stack2 = EndianReader.ToInt32(data, 4, exp.FileRef.Endian);
                                if (stack1 != 0 && !package.IsEntry(stack1))
                                {
                                    item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningExportStackElementOutsideTables, prefix, 0, stack1));
                                }

                                if (stack2 != 0 && !package.IsEntry(stack2))
                                {
                                    item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningExportStackElementOutsideTables, prefix, 1, stack2));
                                }
                            }
                            else if (exp.TemplateOwnerClassIdx is var toci && toci >= 0)
                            {
                                var TemplateOwnerClassIdx = EndianReader.ToInt32(exp.Data, toci, exp.FileRef.Endian);
                                if (TemplateOwnerClassIdx != 0 && !package.IsEntry(TemplateOwnerClassIdx))
                                {
                                    item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningTemplateOwnerClassOutsideTables, prefix, toci.ToString(@"X6"), TemplateOwnerClassIdx));
                                }
                            }

                            var props = exp.GetProperties();
                            foreach (var p in props)
                            {
                                recursiveCheckProperty(item, relativePath, exp.ClassName, exp, p);
                            }
                        }
                        catch (Exception e)
                        {
                            item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningExceptionParsingProperties, prefix, e.Message));
                            continue;
                        }

                        //find binary references
                        try
                        {
                            if (!exp.IsDefaultObject && ObjectBinary.From(exp) is ObjectBinary objBin)
                            {
                                List<(UIndex, string)> indices = objBin.GetUIndexes(exp.FileRef.Game);
                                foreach ((UIndex uIndex, string propName) in indices)
                                {
                                    if (uIndex.value != 0 && !exp.FileRef.IsEntry(uIndex.value))
                                    {
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningBinaryReferenceOutsideTables, prefix, uIndex.value));
                                    }
                                    else if (exp.FileRef.GetEntry(uIndex.value)?.ObjectName.ToString() == @"Trash")
                                    {
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningBinaryReferenceTrashed, prefix, uIndex.value));
                                    }
                                    else if (exp.FileRef.GetEntry(uIndex.value)?.ObjectName.ToString() == @"ME3ExplorerTrashPackage")
                                    {
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningBinaryReferenceTrashed, prefix, uIndex.value));
                                    }
                                }

                                var nameIndicies = objBin.GetNames(exp.FileRef.Game);
                                foreach (var ni in nameIndicies)
                                {
                                    if (ni.Item1 == "")
                                    {
                                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningBinaryNameReferenceOutsideNameTable, prefix));
                                    }
                                }
                            }
                        }
                        catch (Exception e) /* when (!App.IsDebug)*/
                        {
                            item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningUnableToParseBinary, prefix, e.Message));
                        }
                    }

                    foreach (ImportEntry imp in package.Imports)
                    {
                        if (imp.idxLink != 0 && !package.TryGetEntry(imp.idxLink, out _))
                        {
                            item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningImportLinkOutideOfTables, f, imp.UIndex, imp.idxLink));
                        }
                        else if (imp.idxLink == imp.UIndex)
                        {
                            item.AddBlockingError(M3L.GetString(M3L.string_interp_fatalImportCircularReference, f, imp.UIndex));
                        }
                    }
                });
        }

        private static void recursiveCheckProperty(ReferenceCheckPackage item, string relativePath, string containingClassOrStructName, IEntry entry, Property property)
        {
            var prefix = M3L.GetString(M3L.string_interp_warningPropertyTypingWrongPrefix, relativePath, entry.UIndex, entry.ObjectName.Name, entry.ClassName, property.StartOffset.ToString(@"X6"));
            if (property is UnknownProperty up)
            {
                item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningFoundBrokenPropertyData, prefix));

            }
            else if (property is ObjectProperty op)
            {
                bool validRef = true;
                if (op.Value > 0 && op.Value > entry.FileRef.ExportCount)
                {
                    //bad
                    if (op.Name.Name != null)
                    {
                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningReferenceNotInExportTable, prefix, op.Name.Name, op.Value));
                        validRef = false;
                    }
                    else
                    {
                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_nested_warningReferenceNoInExportTable, prefix, op.Value));
                        validRef = false;
                    }
                }
                else if (op.Value < 0 && Math.Abs(op.Value) > entry.FileRef.ImportCount)
                {
                    //bad
                    if (op.Name.Name != null)
                    {
                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningReferenceNotInImportTable, prefix, op.Name.Name, op.Value));
                        validRef = false;

                    }
                    else
                    {
                        item.AddSignificantIssue(M3L.GetString(M3L.string_interp_nested_warningReferenceNoInImportTable, prefix, op.Value));
                        validRef = false;

                    }
                }
                else if (entry.FileRef.GetEntry(op.Value)?.ObjectName.ToString() == @"Trash" || entry.FileRef.GetEntry(op.Value)?.ObjectName.ToString() == @"ME3ExplorerTrashPackage")
                {
                    item.AddSignificantIssue(M3L.GetString(M3L.string_interp_nested_warningTrashedExportReference, prefix, op.Value));
                    validRef = false;
                }

                // Check object is of correct typing?
                if (validRef && op.Value != 0)
                {
                    var referencedEntry = op.ResolveToEntry(entry.FileRef);
                    if (referencedEntry.FullPath.Equals(@"SFXGame.BioDeprecated", StringComparison.InvariantCulture)) return; //This will appear as wrong even though it's technically not

                    var propInfo = UnrealObjectInfo.GetPropertyInfo(entry.Game, op.Name, containingClassOrStructName, containingExport: entry as ExportEntry);
                    var customClassInfos = new Dictionary<string, ClassInfo>();

                    if (referencedEntry.ClassName == @"Class" && op.Value > 0)
                    {

                        // Make sure we have info about this class.
                        var lookupEnt = referencedEntry as ExportEntry;
                        while (lookupEnt != null && lookupEnt.IsClass && !UnrealObjectInfo.GetClasses(entry.FileRef.Game).ContainsKey(lookupEnt.ObjectName))
                        {
                            // Needs dynamically generated
                            var cc = UnrealObjectInfo.generateClassInfo(lookupEnt);
                            customClassInfos[lookupEnt.ObjectName] = cc;
                            lookupEnt = lookupEnt.Parent as ExportEntry;
                        }

                        // If we did not pull it previously, we should try again with our custom info.
                        if (propInfo == null && customClassInfos.Any())
                        {
                            propInfo = UnrealObjectInfo.GetPropertyInfo(entry.Game, op.Name,
                                containingClassOrStructName, customClassInfos[referencedEntry.ObjectName],
                                containingExport: entry as ExportEntry);
                        }
                    }

                    if (propInfo != null && propInfo.Reference != null)
                    {
                        // We can't resolve if an object inherits from a class object that's only defined in native.
                        // This is only possible if the refernce is an import and it's importing native class object.
                        // Like Engine.CodecBinkMovie
                        if (!referencedEntry.IsAKnownNativeClass())
                        {
                            if (referencedEntry.ClassName == @"Class")
                            {
                                // Inherits
                                if (!referencedEntry.InheritsFrom(propInfo.Reference, customClassInfos))
                                {
                                    if (op.Name.Name != null)
                                    {
                                        item.AddSignificantIssue(M3L.GetString(
                                            M3L.string_interp_warningWrongPropertyTypingWrongMessage, prefix,
                                            op.Name.Name, op.Value, op.ResolveToEntry(entry.FileRef).FullPath,
                                            propInfo.Reference, referencedEntry.ObjectName));
                                    }
                                    else
                                    {
                                        item.AddSignificantIssue(M3L.GetString(
                                            M3L
                                                .string_interp_nested_warningWrongClassPropertyTypingWrongMessage,
                                            prefix, op.Value, op.ResolveToEntry(entry.FileRef).FullPath,
                                            propInfo.Reference, referencedEntry.ObjectName));
                                    }
                                }
                            }
                            else if (!referencedEntry.IsA(propInfo.Reference, customClassInfos))
                            {
                                // Is instance of
                                if (op.Name.Name != null)
                                {
                                    item.AddSignificantIssue(M3L.GetString(
                                        M3L.string_interp_warningWrongObjectPropertyTypingWrongMessage,
                                        prefix, op.Name.Name, op.Value,
                                        op.ResolveToEntry(entry.FileRef).FullPath, propInfo.Reference,
                                        referencedEntry.ObjectName));
                                }
                                else
                                {
                                    item.AddSignificantIssue(M3L.GetString(
                                        M3L.string_interp_nested_warningWrongObjectPropertyTypingWrongMessage,
                                        prefix, op.Value, op.ResolveToEntry(entry.FileRef).FullPath,
                                        propInfo.Reference, referencedEntry.ObjectName));
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
                    recursiveCheckProperty(item, relativePath, aop.Name, entry, p);
                }
            }
            else if (property is StructProperty sp)
            {
                foreach (var p in sp.Properties)
                {
                    recursiveCheckProperty(item, relativePath, sp.StructType, entry, p);
                }
            }
            else if (property is ArrayProperty<StructProperty> asp)
            {
                foreach (var p in asp)
                {
                    recursiveCheckProperty(item, relativePath, p.StructType, entry, p);
                }
            }
            else if (property is DelegateProperty dp)
            {
                if (dp.Value.Object != 0 && !entry.FileRef.IsEntry(dp.Value.Object))
                {
                    item.AddSignificantIssue(M3L.GetString(M3L.string_interp_warningDelegatePropertyIsOutsideOfExportTable, prefix, dp.Name.Name));
                }
            }
        }
    }
}
