using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.Win32;

namespace LegendaryExplorer.Tools.Sequence_Editor.Experiments
{
    /// <summary>
    /// Experiments in Sequence Editor (Mgamerz' stuff)
    /// </summary>
    public static class SequenceEditorExperimentsM
    {
        public static void CommitSequenceObjectPositions(SequenceEditorWPF seqEd)
        {
            if (seqEd.CurrentObjects.Any)
            {
                foreach (var seqObj in seqEd.CurrentObjects)
                {
                    var x = seqObj.OffsetX;
                    var y = seqObj.OffsetY;
                    var knownX = seqObj.Export.GetProperty<IntProperty>("ObjPosX")?.Value;
                    var knownY = seqObj.Export.GetProperty<IntProperty>("ObjPosY")?.Value;

                    if (knownX == null && knownY == null)
                    {
                        Debug.WriteLine($"X: {x} Y: {y} for {seqObj.Export.InstancedFullPath}");
                        seqObj.Export.WriteProperty(new IntProperty((int)x, "ObjPosX"));
                        seqObj.Export.WriteProperty(new IntProperty((int)y, "ObjPosY"));
                    }
                    else
                    {
                        if (knownX != null && knownX.Value != (int)Math.Round(x))
                        {
                            Debug.WriteLine($"X: {x} for {seqObj.Export.InstancedFullPath}");
                            seqObj.Export.WriteProperty(new IntProperty((int)x, "ObjPosX"));
                        }
                        if (knownY != null && knownY.Value != (int)Math.Round(y))
                        {
                            Debug.WriteLine($"Y: {y} for {seqObj.Export.InstancedFullPath}");
                            seqObj.Export.WriteProperty(new IntProperty((int)y, "ObjPosY"));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Installs a sequence from a package to another package
        /// </summary>
        /// <param name="seqEd"></param>
        /// <returns></returns>
        public static ExportEntry InstallSequencePrefab(SequenceEditorWPF seqEd)
        {
            OpenFileDialog ofd = AppDirectories.GetOpenPackageDialog();
            bool reload = false;
            var result = ofd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                // Open package and have user select sequence to import
                using var p = MEPackageHandler.OpenMEPackage(ofd.FileName, forceLoadFromDisk: true); // Force load from disk as we will be making changes to remove sequence parent refs
                var sequenceToImport = EntrySelector.GetEntry<ExportEntry>(seqEd, p, "Select a sequence to import and add to this sequence.", x => x.ClassName == "Sequence");
                if (sequenceToImport == null)
                {
                    return null;
                }

                var parentSequence = seqEd.SelectedSequence ?? seqEd.Pcc.FindExport("TheWorld.PersistentLevel.Main_Sequence");
                return InstallSequencePrefab(sequenceToImport, parentSequence);
            }

            return null;
        }

        /// <summary>
        /// Copies a sequence from one package to another
        /// </summary>
        /// <param name="sequenceToImport"></param>
        /// <param name="targetParentSequence"></param>
        /// <returns></returns>
        public static ExportEntry InstallSequencePrefab(ExportEntry sequenceToImport, ExportEntry targetParentSequence)
        {
            // Prevents bringing additional stuff over

            var sourceIdxLink = sequenceToImport.idxLink;
            var parentSeqProp = sequenceToImport.GetProperty<ObjectProperty>("ParentSequence");

            sequenceToImport.idxLink = 0;
            sequenceToImport.RemoveProperty("ParentSequence");

            EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sequenceToImport,
                targetParentSequence.FileRef, targetParentSequence, true, new RelinkerOptionsPackage(),
                out var newUiSeq);
            var expCount =
                targetParentSequence.FileRef.Exports.Count(x => x.InstancedFullPath == newUiSeq.InstancedFullPath);
            if (expCount > 1)
            {
                // update the index
                newUiSeq.ObjectName = targetParentSequence.FileRef.GetNextIndexedName(sequenceToImport.ObjectName.Name);
            }

            var installedSequence = newUiSeq as ExportEntry;

            // These properties are not used by game and are only for developer reference;
            // They are useful to track where a sequence came from and when it was installed, in the event that you
            // change the backing prefab and need to know when this was installed
            installedSequence.WriteProperty(new StrProperty(sequenceToImport.FileRef.FileNameNoExtension ?? "(null)", "ModSourcePackageName"));
            installedSequence.WriteProperty(new StrProperty(DateTime.Now.ToString(CultureInfo.InvariantCulture), "PrefabInstallTime"));

            KismetHelper.AddObjectToSequence(installedSequence, targetParentSequence);

            // Restore the data so we don't technically 'modify' the source
            // This will mark that export as modified technically.
            sequenceToImport.idxLink = sourceIdxLink;
            if (parentSeqProp != null)
            {
                sequenceToImport.WriteProperty(parentSeqProp);
            }

            return newUiSeq as ExportEntry;
        }

        /// <summary>
        /// Might not be that reliable it seems
        /// </summary>
        /// <param name="seqEd"></param>
        public static void CheckSequenceSets(SequenceEditorWPF seqEd)
        {
            if (seqEd.CurrentObjects.Any)
            {
                foreach (var seqObj in seqEd.CurrentObjects)
                {
                    //if (seqObj.Export.ClassName == "SeqAct_SetInt")
                    //Debug.WriteLine("hi");
                    if (seqObj.Export.IsA("SeqAct_SetSequenceVariable"))
                    {
                        var varLinks = KismetHelper.GetVariableLinksOfNode(seqObj.Export);
                        foreach (var link in varLinks)
                        {
                            //link.

                            foreach (var linkedNode in link.LinkedNodes.OfType<ExportEntry>())
                            {
                                if (!linkedNode.IsA(link.ExpectedTypeName))
                                {
                                    Debug.WriteLine("NOT A THING");
                                }

                                var propertyInfo = GlobalUnrealObjectInfo.GetPropertyInfo(seqEd.Pcc.Game, link.PropertyName, linkedNode.ClassName, containingExport: linkedNode);
                                if (propertyInfo == null)
                                {
                                    Debug.WriteLine($"SEQCHECK: {seqObj.Export.UIndex} {seqObj.Export.ObjectName.Instanced} writes a property named {link.PropertyName}, but it doesn't exist on class {linkedNode.ClassName}!");
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void LoadCustomClassesFromFile(SequenceEditorWPF seqEd)
        {
            OpenFileDialog ofd = AppDirectories.GetOpenPackageDialog();
            bool reload = false;
            var result = ofd.ShowDialog();
            if (result.HasValue && result.Value)
            {
                using var p = MEPackageHandler.OpenMEPackage(ofd.FileName, forceLoadFromDisk: true);
                reload = LoadCustomClassesFromPackage(p);
            }

            if (reload)
            {
                seqEd.RefreshToolboxItems();
            }
        }

        public static void LoadCustomClassesFromCurrentPackage(SequenceEditorWPF seqEd)
        {
            if (seqEd.Pcc == null)
                return;

            if (LoadCustomClassesFromPackage(seqEd.Pcc))
            {
                seqEd.RefreshToolboxItems();
            }
        }

        private static bool LoadCustomClassesFromPackage(IMEPackage p)
        {
            var reload = false;
            foreach (var e in p.Exports.Where(x => x.IsClass && x.InheritsFrom("SequenceObject")))
            {
                var classInfo = GlobalUnrealObjectInfo.generateClassInfo(e);
                var defaults = p.GetUExport(ObjectBinary.From<UClass>(e).Defaults);
                Debug.WriteLine($@"Inventorying {e.InstancedFullPath}");
                GlobalUnrealObjectInfo.GenerateSequenceObjectInfoForClassDefaults(defaults);
                GlobalUnrealObjectInfo.InstallCustomClassInfo(e.ObjectName, classInfo, e.Game);
                reload = true;
            }

            return reload;
        }

        public static void ConvertSeqAct_Log_objComments(IMEPackage package, PackageCache cache = null)
        {
            cache ??= new PackageCache(); // So we don't have to open file like 50 times
            foreach (var seqLog in package.Exports.Where(x => x.ClassName == "SeqAct_Log").ToList())
            {
                bool alreadyAdded = false; // Has this objcomment been added already (has this been run on this file already?)
                var owningSequence = seqLog.GetProperty<ObjectProperty>("ParentSequence").ResolveToEntry(seqLog.FileRef) as ExportEntry;
                var existingObjComment = seqLog.GetProperty<ArrayProperty<StrProperty>>("m_aObjComment");
                if (existingObjComment == null || existingObjComment.IsEmpty())
                    continue; // Nothing to do with this object

                var objComment = existingObjComment[0];

                // How this works is we just delete all existing var links and re-attach the originals.
                // This is simpler than trying to figure out what needs to be done to add links and such

                var existingVarLinks = KismetHelper.GetVariableLinksOfNode(seqLog);
                var existingOutLinks = KismetHelper.GetOutputLinksOfNode(seqLog);

                // First we check if this has already been done so we don't add duplicates
                var strVarLink = existingVarLinks.FirstOrDefault(x => x.LinkDesc == "String");
                if (strVarLink != null)
                {
                    // Check it
                    foreach (var strNode in strVarLink.LinkedNodes.OfType<ExportEntry>())
                    {
                        var strValue = strNode.GetProperty<StrProperty>("StrValue");
                        if (objComment.Equals(strValue))
                        {
                            alreadyAdded = true;
                            break;
                        }
                    }
                }

                if (alreadyAdded)
                    continue;

                // Not a duplicate so we need to replace and add ours
                var newProps = SequenceObjectCreator.GetSequenceObjectDefaults(seqLog.FileRef, seqLog.ClassName, seqLog.FileRef.Game, cache);
                newProps.AddOrReplaceProp(existingObjComment);

                // Reattach existing var links, replacing ones of same name
                var newVarLinks = KismetHelper.GetVariableLinks(newProps, seqLog.FileRef);
                for (int i = 0; i < newVarLinks.Count; i++)
                {
                    var existingLink = existingVarLinks.FirstOrDefault(x => x.LinkDesc == newVarLinks[i].LinkDesc);
                    if (existingLink != null)
                    {
                        newVarLinks[i] = existingLink; // Replace
                    }
                }

                KismetHelper.WriteVariableLinksToProperties(newVarLinks, newProps);
                KismetHelper.WriteOutputLinksToProperties(existingOutLinks, newProps);

                seqLog.WriteProperties(newProps); // Write it out as ObjectCreator doesn't use propcollection

                // Create our strObject
                var newStrNode = LEXSequenceObjectCreator.CreateSequenceObject(seqLog.FileRef, "SeqVar_String", cache);
                newStrNode.WriteProperty(new StrProperty(objComment, "StrValue"));
                KismetHelper.AddObjectToSequence(newStrNode, owningSequence);
                KismetHelper.CreateVariableLink(seqLog, "String", newStrNode);
            }
        }
    }
}
