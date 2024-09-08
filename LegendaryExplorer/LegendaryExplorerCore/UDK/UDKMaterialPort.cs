using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Localization;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.UDK
{
    /// <summary>
    /// Ports assets from a game into UDK so the materials can be used in-editor. Full expression information must be available in the source data for this to be useful; UDK cannot compile shaders without it.
    /// </summary>
    public static class UDKMaterialPort
    {
        /// <summary>
        /// If asset should be ported - should be used in conjunction with ForcedExport checks.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        private static bool ShouldPortAsset(ExportEntry x)
        {
            return (x.IsA("Material") ||
                    x.IsA("Texture") ||
                    x.ClassName.CaseInsensitiveEquals("MaterialInstanceConstant"));
        }

        public static void PortMaterialsIntoUDK(MEGame game, string inputPath, string folderNameOverride = null, string gameRootOverride = null, Func<ExportEntry, bool> shouldPortDelegate = null)
        {
            if (UDKDirectory.UDKGamePath == null && gameRootOverride == null)
                return;

            shouldPortDelegate ??= ShouldPortAsset;


            var basePath = Path.Combine(UDKDirectory.GetSharedPath(gameRootOverride), folderNameOverride ?? $"{game}MaterialPort");
            Directory.CreateDirectory(basePath);

            // Clear existing files
            var delFiles = Directory.GetFiles(basePath);
            foreach (var df in delFiles)
            {
                File.Delete(df);
            }

            var me1Path1 = Path.Combine(inputPath);

            var files = Directory.GetFiles(me1Path1, "*.*", SearchOption.AllDirectories).ToList();
            files.Remove(Path.Combine(inputPath, "Core.u"));
            files.Remove(Path.Combine(inputPath, "Engine.u"));
            files.Remove(Path.Combine(inputPath, "EngineFonts.upk"));
            files.Remove(Path.Combine(inputPath, "EngineResources.upk"));
            files.Remove(Path.Combine(inputPath, "EngineMaterials.upk"));
            files.Remove(Path.Combine(inputPath, "EngineScenes.upk"));
            files.Remove(Path.Combine(inputPath, "Engine_MI_Shaders.upk"));
            files.Remove(Path.Combine(inputPath, "NodeBuddies.upk"));
            files.Remove(Path.Combine(inputPath, "EditorMaterials.upk"));
            files.Remove(Path.Combine(inputPath, "EditorMeshes.upk"));
            files.Remove(Path.Combine(inputPath, "EditorResources.upk"));

            PackageCache cache = new PackageCache();
            //foreach(var file in files)
            Parallel.ForEach(files.Where(x => x.RepresentsPackageFilePath()),new ParallelOptions() {MaxDegreeOfParallelism = 8}, file =>
            {
                //if (!file.Contains("BIOG_V_Z", StringComparison.OrdinalIgnoreCase))
                //    continue;

                var isSafeFile = EntryImporter.IsSafeToImportFrom(file, game, file);
                var quickSourceP = MEPackageHandler.UnsafePartialLoad(file, x => false);
                if (quickSourceP.FileNameNoExtension.CaseInsensitiveEquals("EngineDebugMaterials"))
                    return; // Do not port
                if (quickSourceP.FileNameNoExtension.CaseInsensitiveEquals("EngineMaterials"))
                    return; // Do not port
                if (quickSourceP.FileNameNoExtension.CaseInsensitiveEquals("EngineVolumetrics"))
                    return; // Do not port
                if (quickSourceP.FileNameNoExtension.CaseInsensitiveEquals("Engine_MI_Shaders"))
                    return; // Do not port

                var quickPortItems = quickSourceP.Exports.Any(x => (!x.IsForcedExport || isSafeFile) && shouldPortDelegate(x)); 
                if (quickPortItems)
                {
                    using var sourceP = MEPackageHandler.OpenMEPackage(file);

                    #region Not-Forced Export (Goes in this package)

                    var nonForcedPortItems = sourceP.Exports.Where(x=> !x.IsForcedExport && shouldPortDelegate(x)).ToList();
                    if (nonForcedPortItems.Count > 0)
                    {
                        Debug.WriteLine($"Porting {file} to UDK");
                        var destFile = Path.Combine(basePath, sourceP.FileNameNoExtension + ".upk");
                        using var destP = MEPackageHandler.CreateAndOpenPackage(destFile, MEGame.UDK);
                        foreach (var mat in nonForcedPortItems)
                        {
                            if (mat.Parent != null && mat.Parent.ClassName != "Package")
                                continue; // Ignore these.
                            if (mat.ObjectName == "PRO_Before")
                                continue; // This texture is broken in ME1
                            if (mat.ObjectName == "END70_light")
                                continue; // This texture is broken in ME1
                            if (mat.ObjectName == "fx_Tech")
                                continue; // This texture is broken in ME1
                            if (mat.ObjectName == "TMP_Aplpha")
                                continue; // This texture is broken in ME1

                            // Trim before porting so it doesn't try to bring in an import
                            mat.RemoveProperty("PhysMaterial");

                            if (mat.ClassName is "Material")
                            {
                                bool canPort = true;
                                foreach (var sub in mat.FileRef.Exports.Where(x => x.idxLink == mat.UIndex).ToList())
                                {
                                    if (sub.ClassName.StartsWith("BioMaterialExpression"))
                                    {
                                        // This won't exist in UDK and will crash it on use... probably
                                        canPort = false;
                                        break;
                                    }
                                }

                                if (!canPort)
                                    continue; // Skip it.
                            }
                            else if (mat.ClassName.CaseInsensitiveEquals("MaterialInstanceConstant"))
                            {
                                mat.RemoveProperty("bHasStaticPermutationResource");
                                mat.WriteBinary([]); // Remove binary.
                            }

                            EntryExporter.ExportExportToPackage(mat, destP, out var ported, cache, new RelinkerOptionsPackage(cache) { ImportExportDependencies = true, CheckImportsWhenExportingToPackage = false });
                        }

                        CorrectExpressions(destP);

                        foreach (var tex in destP.Exports.Where(x => x.IsTexture()))
                        {
                            tex.RemoveProperty("TextureFileCacheName");
                            tex.RemoveProperty("TextureFileCacheGuid");
                        }
                        if (destP.Exports.Count > 0)
                        {
                            destP.Save();
                        }
                        else
                        {
                            // Don't leave empty file
                            File.Delete(destP.FilePath);
                        }
                    }

                    #endregion

                    if (isSafeFile)
                    {
                        var forcedPortItems = sourceP.Exports.Where(x => x.IsForcedExport && shouldPortDelegate(x)).ToList();
                        foreach (var forcedPortItem in forcedPortItems)
                        {
                            // This is VERY slow
                            var packageName = forcedPortItem.GetRootName();
                            var destFile = Path.Combine(basePath, packageName + ".upk");
                            IMEPackage destP = null;
                            if (File.Exists(destFile))
                            {
                                destP = MEPackageHandler.OpenMEPackage(destFile);
                            }
                            else
                            {
                                destP = MEPackageHandler.CreateAndOpenPackage(destFile, MEGame.UDK);
                            }

                            // Brings over a lot of stuff we don't want in UDK
                            forcedPortItem.RemoveProperty("PhysMaterial");

                            IEntry testRoot = forcedPortItem;
                            while (testRoot.Parent.Parent != null)
                            {
                                testRoot = testRoot.Parent;
                            }

                            var origIndex = testRoot.Parent.UIndex;
                            testRoot.idxLink = 0;
                            EntryExporter.ExportExportToPackage(forcedPortItem, destP, out var ported, cache, new RelinkerOptionsPackage(cache) { ImportExportDependencies = true, CheckImportsWhenExportingToPackage = false });
                            testRoot.idxLink = origIndex; // Restore.
                            var portedE = (ported as ExportEntry);
                            portedE.ExportFlags &= ~UnrealFlags.EExportFlags.ForcedExport;
                            CorrectExpressions(destP);

                            foreach (var tex in destP.Exports.Where(x => x.IsTexture()))
                            {
                                tex.RemoveProperty("TextureFileCacheName");
                                tex.RemoveProperty("TextureFileCacheGuid");
                            }

                            if (destP.Exports.Count > 0)
                            {
                                destP.Save();
                            }
                            else
                            {
                                // Don't leave empty file
                                File.Delete(destP.FilePath);
                            }
                        }
                    }
                }
            }
            ); // Parallel

            var testFiles = Directory.GetFiles(basePath);
            foreach (var tf in testFiles)
            {
                var package = MEPackageHandler.UnsafePartialLoad(tf, x => false);
                if (package.Exports.Count == 0)
                {
                    // Problem!!
                    Debug.WriteLine($"ERROR: PACKAGE HAS ZERO EXPORTS: {tf}");
                }

                if (package.Names.Any(x => x == "BIOC_Base" || x == "SFXGame"))
                {
                    Debug.WriteLine($"ERROR: PACKAGE HAS BIOC_BASE/SFXGame NAME: {tf}");
                }

                foreach (var imp in package.Imports)
                {
                    // Has to be 'Core.Package', not just 'Package'.
                    if (GlobalUnrealObjectInfo.IsAKnownNativeClass(imp.InstancedFullPath, MEGame.UDK))
                        continue;

                    var resolved = EntryImporter.ResolveImport(imp, cache, unsafeLoad: true, gameRootOverride: gameRootOverride);
                    if (resolved == null)
                    {
                        Debug.WriteLine($"ERROR: IMPORT FAILED TO RESOLVE: {imp.InstancedFullPath} IN: {tf}");
                    }
                }

                var item = new ReferenceCheckPackage();
                EntryChecker.CheckReferences(item, package, LECLocalizationShim.NonLocalizedStringConverter);
                foreach (var v in item.GetSignificantIssues())
                {
                    Debug.WriteLine($"{v.Entry?.InstancedFullPath} {v.Message}");
                }
            }

        }

        private static void CorrectExpressions(IMEPackage destP)
        {
            foreach (var expr in destP.Exports.Where(x => x.IsA("MaterialExpression")))
            {
                var props = expr.GetProperties();
                var edX = props.GetProp<IntProperty>("EditorX");
                var edY = props.GetProp<IntProperty>("EditorY");
                if (edX != null)
                {
                    edX.Name = "MaterialExpressionEditorX";
                }

                if (edY != null)
                {
                    edY.Name = "MaterialExpressionEditorY";
                }

                props.AddOrReplaceProp(new ObjectProperty(expr.Parent, "Material"));

                expr.WriteProperties(props);

                expr.ObjectFlags = UnrealFlags.EObjectFlags.Public |
                                   UnrealFlags.EObjectFlags.LoadForClient |
                                   UnrealFlags.EObjectFlags.LoadForServer |
                                   UnrealFlags.EObjectFlags.LoadForEdit |
                                   UnrealFlags.EObjectFlags.Standalone;
            }
        }
    }
}