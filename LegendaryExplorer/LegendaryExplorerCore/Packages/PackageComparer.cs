using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorerCore.Packages
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
                        byte[] header1 = exp1.GenerateHeader();
                        byte[] header2 = exp2.GenerateHeader();
                        Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, header1, ExportEntry.OFFSET_DataSize, sizeof(long));
                        Buffer.BlockCopy(BitConverter.GetBytes((long)0), 0, header2, ExportEntry.OFFSET_DataSize, sizeof(long));

                        if (!header1.AsSpan().SequenceEqual(header2))
                        {
                            string oldclass = (exp1.ClassName != exp2.ClassName) ? $"[{exp2.ClassName}]" : "";
                            changedExports.Add(new EntryStringPair(exp1,
                                $"Export header has changed: {exp1.UIndex} {exp1.InstancedFullPath} ({exp1.ClassName}) {oldclass}"));
                        }

                        if (!exp1.DataReadOnly.SequenceEqual(exp2.DataReadOnly))
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
                        if (!imp1.GenerateHeader().AsSpan().SequenceEqual(imp2.GenerateHeader()))
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
                            changedNames.Add(new EntryStringPair((IEntry)null, $"Name {i} is different: {name1} |vs| {name2}"));
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
                        changedNames.Add(new EntryStringPair((IEntry)null,
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

        #region DETAILED COMPARISON
        // Ported from Mass Effect 2 Randomizer

        /// <summary>
        /// Compares this package to another package by doing a detailed property comparison as well as a binary data scrub comparison. Only exports that exist in both packages will be compared,
        /// so you can compare a porting source to a porting target.
        /// </summary>
        /// <param name="otherPackage"></param>
        public void CompareToPackageDetailed(IMEPackage otherPackage, bool considerIdentical = false)
        {
            // Imports - TEST
            foreach (var imp in Imports)
            {
                var otherImp = otherPackage.FindImport(imp.InstancedFullPath);
                if (otherImp != null)
                {
                    if (otherImp.ClassName != imp.ClassName)
                        Debugger.Break();
                    if (otherImp.PackageFile != imp.PackageFile)
                        Debugger.Break();
                    // Obj name and number are already identical if we found it via FindImport()
                }
                else if (considerIdentical)
                {
                    Debug.WriteLine("Could not find import in otherPackage!");
                }
            }

            // Exports - TEST
            if (considerIdentical && Exports.Count != otherPackage.Exports.Count)
                Debug.WriteLine("Mismatched export count!");
            foreach (var exp1 in Exports)
            {
                var exp2 = otherPackage.FindExport(exp1.InstancedFullPath);
                if (exp2 != null)
                {
                    if (exp2.DataSize != exp1.DataSize)
                    {
                        Debug.WriteLine($@"EXPORT DATA SIZE DIFFERENCE: {exp1.UIndex} {exp1.InstancedFullPath} Left: {exp1.DataSize} Right {exp2.DataSize}");
                    }

                    if (exp2.HeaderLength != exp1.HeaderLength)
                    {
                        Debug.WriteLine($@"EXPORT HEADER SIZE DIFFERENCE: {exp1.UIndex} {exp1.InstancedFullPath} Left: {exp1.DataSize} Right {exp2.DataSize}");
                    }

                    #region PREPROP
                    PrePropCheck(exp1, exp2);
                    #endregion

                    #region BINARY REFS AND SCRUB

                    ObjectBinary objBin1 = ObjectBinary.From(exp1);
                    ObjectBinary objBin2 = ObjectBinary.From(exp2);

                    if (objBin1 != null && objBin2 != null)
                    {
                        var ui1 = new List<(int uIndex, string propName)>(); 
                        objBin1.ForEachUIndex(otherPackage.Game, new UIndexAndPropNameCollector(ui1)); // Compare with same-game

                        var ui2 = new List<(int uIndex, string propName)>();
                        objBin2.ForEachUIndex(otherPackage.Game, new UIndexAndPropNameCollector(ui2));

                        if (ui1.Count != ui2.Count)
                        {
                            Debug.WriteLine($"Different number of UIndexes on {exp1.UIndex} {exp1.InstancedFullPath}! Left: {ui1.Count} Right: {ui2.Count}");
                        }

                        for (int i = 0; i < ui1.Count; i++)
                        {
                            if (i >= ui2.Count)
                                break; // Prevent exception
                            var u1 = ui1[i];
                            var u2 = ui2[i];

                            // Convert UIndex to variable
                            if (u1.uIndex != 0 && u2.uIndex != 0)
                            {
                                IEntry i1 = exp1.FileRef.GetEntry(u1.uIndex);
                                IEntry i2 = exp2.FileRef.GetEntry(u2.uIndex);

                                if (i1.InstancedFullPath != i2.InstancedFullPath)
                                {
                                    Debug.WriteLine($@"Binary entry reference differs on {exp1.UIndex} {exp1.InstancedFullPath} ({u1.propName})! Left: {i1.InstancedFullPath} Right: {i2.InstancedFullPath}");
                                    //Debugger.Break();
                                }
                            }
                            else if (u1.uIndex != u2.uIndex)
                            {
                                Debug.WriteLine(@"SET TO NULL DIFF!");
                            }
                        }

                        var ni1 = objBin1.GetNames(otherPackage.Game);
                        var ni2 = objBin2.GetNames(otherPackage.Game);

                        if (ni1.Count != ni2.Count)
                            Debug.WriteLine("Wrong number of names in binary!!");

                        for (int i = 0; i < ni1.Count && i < ni2.Count; i++)
                        {
                            if (ni1[i].Item1 != ni2[i].Item1)
                            {
                                Debug.WriteLine("NAME DIFFERENCES IN BINARY");
                            }
                        }

                        // SCRUB TO FIND OTHER CHANGES
                        BinScrubCheck(objBin1, objBin2, exp1, exp2);
                    }

                    #endregion

                    #region PROPERTIES

                    var dProps = exp1.GetProperties();
                    var pProps = exp2.GetProperties();

                    MatchPropertyCollections(dProps, pProps, exp1, exp2);

                    #endregion
                }
                else if (considerIdentical)
                {
                    Debug.WriteLine($"Export {exp1.InstancedFullPath} not found in other package!");
                }
            }
        }

        //Todo put this somewhere more appropriate
        public static void DebugByteArrayComparison(byte[] arr1, byte[] arr2)
        {
            var maxLenToCheck = Math.Min(arr1.Length, arr2.Length);
            for (int i = 0; i < maxLenToCheck; i++)
            {
                if (arr1[i] != arr2[i])
                {
                    Debug.WriteLine($"Bytes differ at 0x{i:X8}: First{arr1[i]}, Second: {arr2[i]}");
                    return;
                }
            }

            if (arr1.Length != arr2.Length)
            {
                Debug.WriteLine("Arrays are of different lengths");
                return;
            }

            Debug.WriteLine("Arrays are identical");
        }

        private void PrePropCheck(ExportEntry exp1, ExportEntry exp2)
        {
            var pp1 = exp1.GetPrePropBinary();
            var pp2 = exp2.GetPrePropBinary();

            // test and scrub pre-prop
            if (exp1.HasStack)
            {
                // TODO: CHECK THIS? OH BOY
                /*
                 *Game switch
            {
                MEGame.UDK => 26,
                MEGame.ME3 => 30,
                MEGame.ME1 when FileRef.Platform == MEPackage.GamePlatform.PS3 => 30,
                MEGame.ME2 when FileRef.Platform == MEPackage.GamePlatform.PS3 => 30,
                _ => 32
            };
                 *
                 */
                return; // ???
            }

            int start = 0;

            if (exp1.Game >= MEGame.ME3 && exp1.ClassName == "DominantDirectionalLightComponent" || exp1.ClassName == "DominantSpotLightComponent")
            {
                //DominantLightShadowMap, which goes before everything for some reason

                // TODO: IMPLEMENT THIS CHECK
                // looks like name list?
                int count1 = EndianReader.ToInt32(pp1, 0, exp1.FileRef.Endian);
                start += count1 * 2 + 4;
            }

            if (!exp1.IsDefaultObject && exp1.IsA("Component") || (exp1.Game == MEGame.UDK && exp1.ClassName.EndsWith("Component")))
            {
                int toc1 = EndianReader.ToInt32(pp1, start, exp1.FileRef.Endian);
                int toc2 = EndianReader.ToInt32(pp2, start, exp2.FileRef.Endian);
                if (toc1 == 0 && toc2 == 0)
                {
                    // nothing to compare
                }
                else
                {
                    if (exp1.FileRef.GetEntry(toc1).InstancedFullPath != exp2.FileRef.GetEntry(toc2).InstancedFullPath)
                    {
                        Debug.WriteLine($"PREPROP DIFFERENCE ON {exp1.UIndex} {exp1.InstancedFullPath}: TEMPLATE OWNER DIFFERENCE!");
                    }
                }
                start += 4; //TemplateOwnerClass
                if (exp1.ParentFullPath.Contains("Default__"))
                {
                    int nameIdx1 = EndianReader.ToInt32(pp1, start, exp1.FileRef.Endian);
                    int nameInst1 = EndianReader.ToInt32(pp1, start + 4, exp1.FileRef.Endian);
                    int nameIdx2 = EndianReader.ToInt32(pp2, start, exp2.FileRef.Endian);
                    int nameInst2 = EndianReader.ToInt32(pp2, start + 4, exp2.FileRef.Endian);

                    if (exp1.FileRef.GetNameEntry(nameIdx1) != exp2.FileRef.GetNameEntry(nameIdx2))
                    {
                        Debug.WriteLine($"PREPROP DIFFERENCE ON {exp1.UIndex} {exp1.InstancedFullPath}: TEMPLATE NAME DIFFERENCE!");
                    }
                    if (nameInst1 != nameInst2)
                    {
                        Debug.WriteLine($"PREPROP DIFFERENCE ON {exp1.UIndex} {exp1.InstancedFullPath}: TEMPLATE NAME INSTANCE DIFFERENCE!");
                    }
                    start += 8; //TemplateName
                }
            }

            start += 4; //NetIndex, don't care if these match
        }

        private void MatchPropertyCollections(PropertyCollection props1, PropertyCollection props2, ExportEntry exp1, ExportEntry exp2)
        {
            for (int i = 0; i < props1.Count && i < props2.Count; i++)
            {
                var dProp = props1[i];
                var pProp = props2[i];
                MatchProperty(dProp, pProp, exp1, exp2);
            }
        }

        /// <summary>
        /// Scrubs object and name references from the binary data array, then compares if the data is identical. Use on exports that have been ported across files to identify possible relink misses
        /// If the data is not identical, then not all data has been parsed or relinked
        /// </summary>
        /// <param name="objBin1"></param>
        /// <param name="objBin2"></param>
        /// <param name="exp1"></param>
        /// <param name="exp2"></param>
        private void BinScrubCheck2(ObjectBinary objBin1, ObjectBinary objBin2, ExportEntry exp1, ExportEntry exp2)
        {
            bool isDebug = false; //exp1.UIndex == 237;
            //if (!isDebug) return;

            // Get package-references
            var names1 = objBin1.GetNames(exp1.FileRef.Game);
            var names2 = objBin2.GetNames(exp2.FileRef.Game);

            // Scrub the references

            // Name scrubbing doesn't work as we don't have proper position data and it's written from vars in the
            // objbin class that are not passed by reference
            //for (int i = 0; i < names1.Count; i++)
            //{
            //    names1[i] = (exp1.FileRef.GetNameEntry(0), names1[i].Item2);
            //    names2[i] = (exp2.FileRef.GetNameEntry(0), names2[i].Item2);
            //}

            objBin1.ForEachUIndex(exp1.FileRef.Game, new UIndexZeroer());
            objBin2.ForEachUIndex(exp2.FileRef.Game, new UIndexZeroer());

            // Write new binary with scrubbed entry refs
            EndianReader er1 = new EndianReader(new MemoryStream());
            objBin1.WriteTo(er1.Writer, exp1.FileRef);
            var originalWrittenBin1 = er1.ToArray();

            EndianReader er2 = new EndianReader(new MemoryStream());
            objBin2.WriteTo(er2.Writer, exp2.FileRef);
            var originalWrittenBin2 = er2.ToArray();

            // Find differences
            int binStart = exp1.propsEnd();
            for (int i = 0; i < originalWrittenBin1.Length; i++)
            {
                if (originalWrittenBin1[i] != originalWrittenBin2[i])
                {
                    if (isDebug)
                    {
                        Debug.WriteLine($"BINARY DIFF ON {exp1.InstancedFullPath} at 0x{(binStart + i):X5}: Left: {originalWrittenBin1[i]:X2} Right {originalWrittenBin2[i]:X2}");
                    }
                    else
                    {
                        Debug.WriteLine($"BINARY DIFF ON {exp1.UIndex} {exp1.InstancedFullPath}! Starting at 0x{(binStart + i):X5}");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Scrubs object and name references from the binary data array, then compares if the data is identical. Use on exports that have been ported across files to identify possible relink misses
        /// If the data is not identical, then not all data has been parsed or relinked
        /// </summary>
        /// <param name="objBin1"></param>
        /// <param name="objBin2"></param>
        /// <param name="exp1"></param>
        /// <param name="exp2"></param>
        private void BinScrubCheck(ObjectBinary objBin1, ObjectBinary objBin2, ExportEntry exp1, ExportEntry exp2)
        {
            bool isDebug = false;
            //bool isDebug = exp1.UIndex == 1682;
            //if (!isDebug) return;
            EndianReader er1 = new EndianReader(new MemoryStream());
            objBin1.WriteTo(er1.Writer, exp1.FileRef);
            var originalWrittenBin1 = er1.ToArray();

            EndianReader er2 = new EndianReader(new MemoryStream());
            objBin2.WriteTo(er2.Writer, exp2.FileRef);
            var originalWrittenBin2 = er2.ToArray();

            // Package-Specific Variables
            var names1 = objBin1.GetNames(exp1.FileRef.Game);
            var names2 = objBin2.GetNames(exp2.FileRef.Game);
            var uindices1 = objBin1.GetUIndexes(exp1.FileRef.Game);
            var uindices2 = objBin2.GetUIndexes(exp2.FileRef.Game);

            // Now we iterate through written binary looking for values
            for (int pos = 0; pos < originalWrittenBin1.Length; pos++)
            {
                if (isDebug && pos == 0x171)
                    Debug.Write("");

                if (pos <= originalWrittenBin1.Length - 8 && pos <= originalWrittenBin2.Length - 8)
                {
                    // Check name
                    var nameIdx = BitConverter.ToInt32(originalWrittenBin1, pos);
                    if (nameIdx != 0 && exp1.FileRef.IsName(nameIdx))
                    {
                        var name1 = exp1.FileRef.GetNameEntry(nameIdx);
                        //if (isDebug)
                        //    Debugger.Break();
                        //var name2 = exp2.FileRef.GetNameEntry(BitConverter.ToInt32(originalWrittenBin2, pos));

                        var index1 = BitConverter.ToInt32(originalWrittenBin1, pos + 4);
                        //var index2 = BitConverter.ToInt32(originalWrittenBin1, pos + 4);

                        var generatedName = new NameReference(name1, index1);
                        if (isDebug)
                            Debug.Write("");
                        if (names1.Any(x => x.Item1 == generatedName))
                        {
                            // SCRUB
                            if (isDebug)
                                Debug.WriteLine($"Scrubbing N {generatedName.Instanced} at 0x{pos:X4}");
                            originalWrittenBin1.OverwriteRange(pos, new byte[8]);
                            originalWrittenBin2.OverwriteRange(pos, new byte[8]);
                            pos += 7; // will +1 on loop
                            continue;
                        }
                    }
                }

                if (pos <= originalWrittenBin1.Length - 4 && pos <= originalWrittenBin2.Length - 4)
                {
                    // Check UIndex
                    var uindex1 = BitConverter.ToInt32(originalWrittenBin1, pos);
                    //var name2 = exp2.FileRef.GetNameEntry(BitConverter.ToInt32(originalWrittenBin2, pos));

                    if (uindex1 != 0 && uindices1.Any(x => x == uindex1))
                    {
                        // SCRUB
                        //if (isDebug)
                        //    Debug.WriteLine($"Scrubbing U {uindex1} at 0x{pos:X4}");
                        originalWrittenBin1.OverwriteRange(pos, new byte[4]);
                        originalWrittenBin2.OverwriteRange(pos, new byte[4]);
                        pos += 3; // will +1 on loop
                    }
                }
            }

            long binStopPosition = -1;
            if (objBin1 is UScriptStruct uss1 && objBin2 is UScriptStruct uss2)
            {
                MatchPropertyCollections(uss1.Defaults, uss2.Defaults, exp1, exp2);
                binStopPosition = uss1.DefaultsStartPosition;
            }

            // Find difference
            int binStart = exp1.propsEnd();
            for (int i = 0; i < originalWrittenBin1.Length; i++)
            {
                if (binStopPosition != -1 && i >= binStopPosition)
                    return;
                if (originalWrittenBin1[i] != originalWrittenBin2[i])
                {
                    Debug.WriteLine($"BINARY DIFF ON {exp1.UIndex} {exp1.InstancedFullPath} at 0x{(binStart + i):X5} ({i:X5}): Left: {originalWrittenBin1[i]:X2} Right {originalWrittenBin2[i]:X2}");
                    if (!isDebug)
                        break;
                }
            }
        }

        /// <summary>
        /// Compares properties recursively to see if they resolve to identical values (name and object refs, property names and primitive values)
        /// </summary>
        /// <param name="dProp"></param>
        /// <param name="pProp"></param>
        /// <param name="exp1"></param>
        /// <param name="exp2"></param>
        private void MatchProperty(Property dProp, Property pProp, ExportEntry exp1, ExportEntry exp2)
        {
            if (dProp.GetType() != pProp.GetType())
            {
                Debug.WriteLine("NON-MATCHING PROPERTY TYPES");
                return;
            }

            if (dProp.Name != pProp.Name)
            {
                Debug.WriteLine(@"NON-MATCHING PROPERTY NAMES!");
            }

            if (dProp is ObjectProperty dOp && pProp is ObjectProperty pOp)
            {
                if (dOp.Value == 0 && pOp.Value == dOp.Value)
                {
                    // Zero, same
                }
                else
                {
                    // Ensure they resolve to same variable
                    var d = dOp.ResolveToEntry(exp1.FileRef);
                    var p = pOp.ResolveToEntry(exp2.FileRef);

                    if (d.GetType() != p.GetType())
                    {
                        Debug.WriteLine($@"ObjectProp referenced object types differ on {exp1.UIndex} {exp1.InstancedFullPath}! Left: {d.GetType()} Right: {p.GetType()}");
                    }

                    if (d.InstancedFullPath != p.InstancedFullPath)
                    {
                        Debug.WriteLine($@"Referenced ObjectProperty value differs on {exp1.UIndex} {exp1.InstancedFullPath}! Left: {d.InstancedFullPath} Right: {p.InstancedFullPath}");
                    }
                }
            }
            else if (dProp is ArrayPropertyBase dArrayP && pProp is ArrayPropertyBase pArrayP)
            {
                if (dArrayP.Count != pArrayP.Count)
                {
                    Debug.WriteLine($"Different sized arrays on {exp1.UIndex} {exp1.InstancedFullPath}!");
                }

                for (int i = 0; i < dArrayP.Properties.Count && i < pArrayP.Properties.Count; i++)
                {
                    MatchProperty(dArrayP.Properties[i], pArrayP.Properties[i], exp1, exp2);
                }
            }
            else if (dProp is StructProperty dStructP && pProp is StructProperty pStructP)
            {
                if (dStructP.Properties.Count != pStructP.Properties.Count)
                {
                    Debug.WriteLine($"Different sized Structs on {exp1.UIndex} {exp1.InstancedFullPath}!");
                }

                for (int i = 0; i < dStructP.Properties.Count && i < pStructP.Properties.Count; i++)
                {
                    MatchProperty(dStructP.Properties[i], pStructP.Properties[i], exp1, exp2);
                }
            }
            else if (dProp is DelegateProperty dDelP && pProp is DelegateProperty pDelP)
            {
                // Ensure they resolve to same variable
                var d = exp1.FileRef.GetEntry(dDelP.Value.ContainingObjectUIndex);
                var p = exp2.FileRef.GetEntry(pDelP.Value.ContainingObjectUIndex);

                if (d != null && p != null)
                {
                    if (d.GetType() != p.GetType())
                    {
                        Debug.WriteLine(@"DelegateProperty ObjectProp types differ!");
                    }

                    if (d.InstancedFullPath != p.InstancedFullPath)
                    {
                        Debug.WriteLine(@"Referenced DelegateProperty value differs!");
                    }
                }
                else if (d != p)
                {
                    Debug.WriteLine("XOR'd script delegates!");
                }
            }
            else if (dProp is BoolProperty dBool && pProp is BoolProperty pBool)
            {
                if (dBool.Value != pBool.Value)
                {
                    Debug.WriteLine($"Bool difference on {exp1.UIndex} {exp1.InstancedFullPath}: Left: {dBool.Value} Right {pBool.Value}");
                }
            }
            else if (dProp is StrProperty dStr && pProp is StrProperty pStr)
            {
                if (dStr.Value != pStr.Value)
                {
                    Debug.WriteLine($"Str difference on {exp1.UIndex} {exp1.InstancedFullPath}: Left: {dStr.Value} Right {pStr.Value}");
                }
            }
            else if (dProp is FloatProperty dFloat && pProp is FloatProperty pFloat)
            {
                if (dFloat.Value != pFloat.Value)
                {
                    Debug.WriteLine($"Float difference on {exp1.UIndex} {exp1.InstancedFullPath}: Left: {dFloat.Value} Right {pFloat.Value}");
                }
            }
            else if (dProp is IntProperty dInt && pProp is IntProperty pInt)
            {
                // DEBUG: Ignore GUIDs as they seem to differ across files
                if (dProp.Name == "A" || dProp.Name == "B" || dProp.Name == "C" || dProp.Name == "D")
                    return;
                if (dInt.Value != pInt.Value)
                {
                    Debug.WriteLine($"Int difference in {exp1.UIndex} {exp1.InstancedFullPath}: Left: {dInt.Value} Right {pInt.Value}");
                }
            }
            else if (dProp is NameProperty dName && pProp is NameProperty pName)
            {
                if (dName.Value.Name != pName.Value.Name || dName.Value.Number != pName.Value.Number)
                {
                    Debug.WriteLine($"Name difference on {exp1.UIndex} {exp1.InstancedFullPath}: Left: {dName.Value.Instanced} Right {pName.Value.Instanced}");
                }
            }
            else if (dProp is EnumProperty dEnum && pProp is EnumProperty pEnum)
            {
                if (dEnum.EnumType != pEnum.EnumType)
                {
                    Debug.WriteLine($"Enum type difference on {exp1.UIndex} {exp1.InstancedFullPath}: Left: {dEnum.EnumType.Instanced} Right {pEnum.EnumType.Instanced}");
                }
                if (dEnum.Value != pEnum.Value)
                {
                    Debug.WriteLine($"Enum value difference on {exp1.UIndex} {exp1.InstancedFullPath}: Left: {dEnum.Value.Instanced} Right {pEnum.Value.Instanced}");
                }
            }
            else if (dProp is StringRefProperty dStringRef && pProp is StringRefProperty pStringRef)
            {
                if (dStringRef.Value != pStringRef.Value)
                {
                    Debug.WriteLine($"StringRef value difference on {exp1.UIndex} {exp1.InstancedFullPath}: Left: {dStringRef.Value} Right: {pStringRef.Value}");
                }
            }
            else if (dProp is ByteProperty dByte && pProp is ByteProperty pByte)
            {
                if (dByte.Value != pByte.Value)
                {
                    Debug.WriteLine($"Byte value difference on {exp1.UIndex} {exp1.InstancedFullPath}: Left: {dByte.Value} Right {pByte.Value}");
                }
            }
            else if (dProp is NoneProperty && pProp is NoneProperty)
            {
                // Do nothing. It's handled
            }
            else
            {
                Debug.WriteLine($"Property type not checked: {dProp.GetType()}");
                Debugger.Break();
            }
        }

        #endregion

    }
}
