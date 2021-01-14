using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Misc;

namespace ME3ExplorerCore.Packages
{
    public abstract partial class UnrealPackageFile
    {
        // This class holds the package comparison methods.
        /// <summary>
        /// Compares this package to a package stored in a stream. The calling package must implement IMEPackage or this will throw an exception.
        /// </summary>
        /// <param name="compareFile"></param>
        /// <returns></returns>
        public List<EntryStringPair> CompareToPackage(Stream stream)
        {
            using var compareFile = MEPackageHandler.OpenMEPackageFromStream(stream);
            return CompareToPackage(compareFile);
        }

        /// <summary>
        /// Compares this package to a package stored on disk. The calling package must implement IMEPackage or this will throw an exception.
        /// </summary>
        /// <param name="compareFile"></param>
        /// <returns></returns>
        public List<EntryStringPair> CompareToPackage(string packagePath)
        {
            using var compareFile = MEPackageHandler.OpenMEPackage(packagePath);
            return CompareToPackage(compareFile);
        }

        /// <summary>
        /// Compares this package to another IMEPackage. The calling package must implement IMEPackage or this will throw an exception.
        /// </summary>
        /// <param name="compareFile"></param>
        /// <returns></returns>
        public List<EntryStringPair> CompareToPackage(IMEPackage compareFile)
        {
            if (this is IMEPackage thisPackage)
            {
                if (thisPackage.Game != compareFile.Game)
                {
                    throw new Exception("Can't compare files, they're for different games!");
                }

                if (thisPackage.Platform != compareFile.Platform)
                {
                    throw new Exception("Cannot compare packages across platforms!");
                }

                var changedImports = new List<EntryStringPair>();
                var changedNames = new List<EntryStringPair>();
                var changedExports = new List<EntryStringPair>();

                #region Exports Comparison

                {
                    // these brackets are here to scope the variables so same named ones can be used in a later chunk
                    int numExportsToEnumerate = Math.Min(ExportCount, compareFile.ExportCount);

                    for (int i = 0; i < numExportsToEnumerate; i++)
                    {
                        ExportEntry exp1 = Exports[i];
                        ExportEntry exp2 = compareFile.Exports[i];

                        //make data offset and data size the same, as the exports could be the same even if it was appended later.
                        //The datasize being different is a data difference not a true header difference so we won't list it here.
                        byte[] header1 = exp1.Header.TypedClone();
                        byte[] header2 = exp2.Header.TypedClone();
                        Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, header1, 32, sizeof(long));
                        Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, header2, 32, sizeof(long));

                        if (!header1.SequenceEqual(header2))
                        {
                            changedExports.Add(new EntryStringPair(exp1,
                                $"Export header has changed: {exp1.UIndex} {exp1.InstancedFullPath} ({exp1.ClassName})"));
                        }

                        if (!exp1.Data.SequenceEqual(exp2.Data))
                        {
                            changedExports.Add(new EntryStringPair(exp1,
                                $"Export data has changed: {exp1.UIndex} {exp1.InstancedFullPath} ({exp1.ClassName})"));
                        }
                    }

                    IMEPackage enumerateExtras = thisPackage;
                    string file = "this file";
                    if (compareFile.ExportCount > numExportsToEnumerate)
                    {
                        file = "other file";
                        enumerateExtras = compareFile;
                    }

                    for (int i = numExportsToEnumerate; i < enumerateExtras.ExportCount; i++)
                    {
                        Debug.WriteLine(
                            $"Export only exists in {file}: {i + 1} {enumerateExtras.Exports[i].InstancedFullPath}");
                        changedExports.Add(new EntryStringPair(
                            enumerateExtras.Exports[i].FileRef == this ? enumerateExtras.Exports[i] : null,
                            $"Export only exists in {file}: {i + 1} {enumerateExtras.Exports[i].InstancedFullPath}"));
                    }
                }

                #endregion

                #region Imports Comparison

                {
                    int numImportsToEnumerate = Math.Min(ImportCount, compareFile.ImportCount);

                    for (int i = 0; i < numImportsToEnumerate; i++)
                    {
                        ImportEntry imp1 = Imports[i];
                        ImportEntry imp2 = compareFile.Imports[i];
                        if (!imp1.Header.SequenceEqual(imp2.Header))
                        {
                            changedImports.Add(new EntryStringPair(imp1,
                                $"Import header has changed: {imp1.UIndex} {imp1.InstancedFullPath}"));
                        }
                    }

                    IMEPackage enumerateExtras = thisPackage;
                    string file = "this file";
                    if (compareFile.ImportCount > numImportsToEnumerate)
                    {
                        file = "other file";
                        enumerateExtras = compareFile;
                    }

                    for (int i = numImportsToEnumerate; i < enumerateExtras.ImportCount; i++)
                    {
                        Debug.WriteLine(
                            $"Import only exists in {file}: {-i - 1} {enumerateExtras.Imports[i].InstancedFullPath}");
                        changedImports.Add(new EntryStringPair(
                            enumerateExtras.Imports[i].FileRef == this ? enumerateExtras.Imports[i] : null,
                            $"Import only exists in {file}: {-i - 1} {enumerateExtras.Imports[i].InstancedFullPath}"));
                    }
                }

                #endregion

                #region Names Comparison

                {
                    int numNamesToEnumerate = Math.Min(NameCount, compareFile.NameCount);
                    for (int i = 0; i < numNamesToEnumerate; i++)
                    {
                        var name1 = Names[i];
                        var name2 = compareFile.Names[i];

                        //if (!StructuralComparisons.StructuralEqualityComparer.Equals(header1, header2))
                        if (!name1.Equals(name2, StringComparison.InvariantCultureIgnoreCase))

                        {
                            changedNames.Add(new EntryStringPair(null, $"Name {i} is different: {name1} |vs| {name2}"));
                        }
                    }

                    IMEPackage enumerateExtras = thisPackage;
                    string file = "this file";
                    if (compareFile.NameCount > numNamesToEnumerate)
                    {
                        file = "other file";
                        enumerateExtras = compareFile;
                    }

                    for (int i = numNamesToEnumerate; i < enumerateExtras.NameCount; i++)
                    {
                        Debug.WriteLine($"Name only exists in {file}: {i} {enumerateExtras.Names[i]}");
                        changedNames.Add(new EntryStringPair(null,
                            $"Name only exists in {file}: {i} {enumerateExtras.Names[i]}"));
                    }
                }

                #endregion

                var fullList = new List<EntryStringPair>();
                fullList.AddRange(changedExports);
                fullList.AddRange(changedImports);
                fullList.AddRange(changedNames);
                return fullList;
            }
         
            throw new Exception("Source object must implement IMEPackage to support package comparisons");
        }
    }
}
