using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.GameFilesystem;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Packages.CloningImportingAndRelinking;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3Explorer.Packages
{
    public static class EntryImporterExtended
    {
        //public static IEntry EnsureClassIsInFile(IMEPackage pcc, string className)
        //{
        //    //check to see class is already in file
        //    foreach (ImportEntry import in pcc.Imports)
        //    {
        //        if (import.IsClass && import.ObjectName == className)
        //        {
        //            return import;
        //        }
        //    }
        //    foreach (ExportEntry export in pcc.Exports)
        //    {
        //        if (export.IsClass && export.ObjectName == className)
        //        {
        //            return export;
        //        }
        //    }

        //    ClassInfo info = UnrealObjectInfo.GetClassOrStructInfo(pcc.Game, className);

        //    //backup some package state so we can undo changes if something goes wrong
        //    int exportCount = pcc.ExportCount;
        //    int importCount = pcc.ImportCount;
        //    List<string> nameListBackup = pcc.Names.ToList();
        //    try
        //    {
        //        if (EntryImporter.IsSafeToImportFrom(info.pccPath, pcc.Game))
        //        {
        //            string package = Path.GetFileNameWithoutExtension(info.pccPath);
        //            return pcc.getEntryOrAddImport($"{package}.{className}");
        //        }

        //        //It's a class that's defined locally in every file that uses it.
        //        Stream loadStream = null;
        //        if (info.pccPath == UnrealObjectInfo.Me3ExplorerCustomNativeAdditionsName)
        //        {
        //            loadStream = ME3ExplorerCoreUtilities.GetCustomAppResourceStream(pcc.Game);
        //            //string resourceFilePath = App.CustomResourceFilePath(pcc.Game);
        //            //if (File.Exists(resourceFilePath))
        //            //{
        //            //    sourceFilePath = resourceFilePath;
        //            //}
        //        }
        //        else
        //        {
        //            string testPath = Path.Combine(MEDirectories.GetBioGamePath(pcc.Game), info.pccPath);
        //            if (File.Exists(testPath))
        //            {
        //                loadStream = new MemoryStream(File.ReadAllBytes(testPath));
        //            }
        //            else if (pcc.Game == MEGame.ME1)
        //            {
        //                testPath = Path.Combine(ME1Directory.DefaultGamePath, info.pccPath);
        //                if (File.Exists(testPath))
        //                {
        //                    loadStream = new MemoryStream(File.ReadAllBytes(testPath));
        //                }
        //            }
        //        }

        //        if (loadStream == null)
        //        {
        //            //can't find file to import from. This may occur if user does not have game or neccesary dlc installed 
        //            return null;
        //        }

        //        using IMEPackage sourcePackage = MEPackageHandler.OpenMEPackageFromStream(loadStream);

        //        if (!sourcePackage.IsUExport(info.exportIndex))
        //        {
        //            return null; //not sure how this would happen
        //        }

        //        ExportEntry sourceClassExport = sourcePackage.GetUExport(info.exportIndex);

        //        if (sourceClassExport.ObjectName != className)
        //        {
        //            return null;
        //        }

        //        //Will make sure that, if the class is in a package, that package will exist in pcc
        //        IEntry parent = EntryImporter.GetOrAddCrossImportOrPackage(sourceClassExport.ParentFullPath, sourcePackage, pcc);

        //        var relinkResults = EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneAllDependencies, sourceClassExport, pcc, parent, true, out IEntry result);
        //        if (relinkResults?.Count > 0)
        //        {
        //            ListDialog ld = new ListDialog(relinkResults, "Relink report", "The following items failed to relink.", null);
        //            ld.Show();
        //        }
        //        return result;
        //    }
        //    catch (Exception e)
        //    {
        //        //remove added entries
        //        var entriesToRemove = new List<IEntry>();
        //        for (int i = exportCount; i < pcc.Exports.Count; i++)
        //        {
        //            entriesToRemove.Add(pcc.Exports[i]);
        //        }
        //        for (int i = importCount; i < pcc.Imports.Count; i++)
        //        {
        //            entriesToRemove.Add(pcc.Imports[i]);
        //        }
        //        EntryPruner.TrashEntries(pcc, entriesToRemove);
        //        pcc.restoreNames(nameListBackup);
        //        return null;
        //    }
        //}

        public static void ShowRelinkResults(List<EntryStringPair> results)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ListDialog ld = new ListDialog(results, "Relink report", "The following items failed to relink.", null);
                ld.Show();
            });
        }
    }
}
