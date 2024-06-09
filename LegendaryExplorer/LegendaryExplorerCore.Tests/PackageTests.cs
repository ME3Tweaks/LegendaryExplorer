using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.ME1.Unreal.UnhoodBytecode;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Tests
{
    [TestClass]
    public class PackageTests
    {
        [TestMethod]
        public void TestPlatformsGamesAndProperties()
        {
            GlobalTest.Init();
            // Loads compressed packages and attempts to enumerate every object's properties.
            var packagesPath = GlobalTest.GetTestPackagesDirectory();
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Do not use package caching in tests
                    Console.WriteLine($"Opening package {p}");

                    (MEGame expectedGame, MEPackage.GamePlatform expectedPlatform) = GlobalTest.GetExpectedTypes(p);

                    var package = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);

                    Assert.AreEqual(expectedGame, package.Game,
                        "The expected game and the resolved game do not match!");
                    Assert.AreEqual(expectedPlatform, package.Platform,
                        "The expected platform and the resolved platform do not match!");
                    Console.WriteLine($" > Enumerating all exports for properties");

                    foreach (var exp in package.Exports)
                    {
                        if (exp.ClassName != "Class")
                        {
                            var props = exp.GetProperties(forceReload: true, includeNoneProperties: true);
                            Assert.IsInstanceOfType(props.LastOrDefault(), typeof(NoneProperty),
                                $"Error parsing properties on export {exp.UIndex} {exp.InstancedFullPath} in file {exp.FileRef.FilePath}");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void TestOpeningMethods()
        {
            GlobalTest.Init();

            var packagesPath = GlobalTest.GetTestPackageSerializationsDirectory();
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Open from stream
                    var binStream = new MemoryStream(File.ReadAllBytes(p));
                    var sourcePackageStream = MEPackageHandler.OpenMEPackageFromStream(binStream, Path.GetFileName(p));
                    var sourcePackageDisk = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);

                    for(int i = 0; i < sourcePackageStream.ExportCount; i++)
                    {
                        var sourceExportD = sourcePackageDisk.Exports[i];
                        var sourceExportS = sourcePackageStream.Exports[i];
                        Assert.IsTrue(sourceExportD.Data.SequenceEqual(sourceExportS.Data),
                            $"Export {i+1} {sourceExportD.InstancedFullPath} in {p} did not deserialize the same between load from disk and load from stream!");
                    }
                }
            }
        }

        [TestMethod]
        public void TestCompression()
        {
            GlobalTest.Init();

            // Loads compressed packages, save them uncompressed. Load package, save re-compressed, compare results
            var packagesPath = GlobalTest.GetTestPackagesDirectory();
            //var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Do not use package caching in tests
                    Console.WriteLine($"Opening package {p}");
                    var originalLoadedPackage = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);
                    if (originalLoadedPackage.Platform != MEPackage.GamePlatform.PC)
                    {
                        Assert.ThrowsException<Exception>(() => { originalLoadedPackage.SaveToStream(true); },
                            "Non-PC platform package should not be saveable. An exception should have been thrown to stop this!");
                        continue;
                    }

                    // Is PC
                    var uncompressedPS = originalLoadedPackage.SaveToStream(false);
                    var compressedPS = originalLoadedPackage.SaveToStream(true);

                    uncompressedPS.Position = compressedPS.Position = 0;

                    var reopenedUCP = MEPackageHandler.OpenMEPackageFromStream(uncompressedPS);
                    var reopenedCCP = MEPackageHandler.OpenMEPackageFromStream(compressedPS);

                    Assert.AreEqual(reopenedCCP.NameCount, reopenedUCP.NameCount,
                        $"Name count is not identical between compressed/uncompressed packages");
                    Assert.AreEqual(reopenedCCP.ImportCount, reopenedUCP.ImportCount,
                        $"Import count is not identical between compressed/uncompressed packages");
                    Assert.AreEqual(reopenedCCP.ExportCount, reopenedUCP.ExportCount,
                        $"Export count is not identical between compressed/uncompressed packages");

                    for (int i = 0; i < reopenedCCP.NameCount; i++)
                    {
                        var nameCCP = reopenedCCP.Names[i];
                        var nameUCP = reopenedUCP.Names[i];
                        Assert.AreEqual(nameCCP, nameUCP,
                            $"Names are not identical between compressed/uncompressed packages, name index {i}");
                    }

                    for (int i = 0; i < reopenedCCP.ImportCount; i++)
                    {
                        var importCCP = reopenedCCP.Imports[i];
                        var importUCP = reopenedUCP.Imports[i];
                        Assert.IsTrue(importCCP.GenerateHeader().AsSpan().SequenceEqual(importUCP.GenerateHeader()),
                            $"Header data for import {-(i + 1)} are not identical between compressed/uncompressed packages");
                    }

                    for (int i = 0; i < reopenedCCP.ExportCount; i++)
                    {
                        var exportCCP = reopenedCCP.Exports[i];
                        var exportUCP = reopenedUCP.Exports[i];
                        Assert.IsTrue(exportCCP.GenerateHeader().AsSpan().SequenceEqual(exportUCP.GenerateHeader()),
                            $"Header data for xport {i + 1} are not identical between compressed/uncompressed packages");
                    }
                }
            }
        }

        [TestMethod]
        public void TestBinaryConverters()
        {
            GlobalTest.Init();

            var packagesPath = GlobalTest.GetTestPackagesDirectory();
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Do not use package caching in tests
                    Console.WriteLine($"Opening package {p}");
                    var (game, platform) = GlobalTest.GetExpectedTypes(p);
                    if (platform == MEPackage.GamePlatform.PC) // Will expand in future, but not now.
                    {
                        var originalLoadedPackage = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);
                        foreach (var export in originalLoadedPackage.Exports)
                        {
                            PropertyCollection props = export.GetProperties();
                            ObjectBinary bin = ObjectBinary.From(export) ?? export.GetBinaryData();

                            if (game == MEGame.UDK)
                                continue; // No point testing converting things to UDK in this fashion

                            byte[] original = export.Data;
                            export.WritePropertiesAndBinary(props, bin);
                            byte[] changed = export.Data;
                            Assert.AreEqual(original.Length, changed.Length,
                                $"Reserialization of export {export.UIndex} {export.InstancedFullPath} produced a different sized byte array than the input. Original size: {original.Length}, reserialized: {changed.Length}, difference: 0x{(changed.Length - original.Length):X8} bytes. File: {p}");
                            Assert.IsTrue(original.AsSpan().SequenceEqual(changed),
                                $"Reserialization of export {export.UIndex} {export.InstancedFullPath} produced a different byte array than the input. File: {p}");

                            bin.GetNames(game);
                            bin.ForEachUIndex(game, new UIndexValidityChecker(originalLoadedPackage, export));
                        }
                    }
                }
            }
        }

        private readonly struct UIndexValidityChecker : IUIndexAction
        {
            private readonly IMEPackage Pcc;
            private readonly ExportEntry Export;

            public UIndexValidityChecker(IMEPackage pcc, ExportEntry export)
            {
                Pcc = pcc;
                Export = export;
            }

            public void Invoke(ref int uIndex, string propName)
            {
                if (uIndex is not 0)
                {
                    Assert.IsNotNull(Pcc.GetEntry(uIndex), $"Invalid UIndex at Binary property '{propName}' of export #{Export.UIndex} {Export.InstancedFullPath} in File: {Pcc.FilePath}");
                }
            }
        }

        [TestMethod]
        public void TestFunctionParsing()
        {
            // This method is essentially test of the BytecodeEditor parser with the actual ui logic all removed
            GlobalTest.Init();

            // Loads compressed packages, save them uncompressed. Load package, save re-compressed, compare results
            var packagesPath = GlobalTest.GetTestPackagesDirectory();
            //var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    (var game, var platform) = GlobalTest.GetExpectedTypes(p);

                    // Use to skip
                    //if (platform != MEPackage.GamePlatform.Xenon) continue;
                    //if (game != MEGame.ME1) continue;

                    Console.WriteLine($"Opening package {p}");

                    // Do not use package caching in tests
                    var originalLoadedPackage = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);
                    foreach (var export in originalLoadedPackage.Exports.Where(x => x.ClassName == "Function" || x.ClassName == "State"))
                    {
                        //Console.WriteLine($" >> Decompiling {export.InstancedFullPath}");
                        var data = export.Data;
                        var funcBin = ObjectBinary.From<UFunction>(export); //parse it out 
                        if (export.FileRef.Game == MEGame.ME3 || export.FileRef.Platform == MEPackage.GamePlatform.PS3)
                        {
                            var func = new Function(data, export);
                            func.ParseFunction();
                            func.GetSignature();
                            if (export.ClassName == "Function")
                            {
                                var nativeBackOffset = export.FileRef.Game < MEGame.ME3 ? 7 : 6;
                                var pos = data.Length - nativeBackOffset;
                                string flagStr = func.GetFlags();
                                var nativeIndex = EndianReader.ToInt16(data, pos, export.FileRef.Endian);
                                pos += 2;
                                var flags = EndianReader.ToInt16(data, pos, export.FileRef.Endian);
                            }
                            else
                            {
                                //State
                                //parse remaining
                                var footerstartpos = 0x20 + funcBin.ScriptStorageSize;
                                var footerdata = data.Slice(footerstartpos, (int)data.Length - (footerstartpos));
                                var fpos = 0;
                                //ScriptFooterBlocks.Add(new ScriptHeaderItem("Probemask?", "??", fpos + footerstartpos) { length = 8 });
                                fpos += 0x8;

                                //ScriptFooterBlocks.Add(new ScriptHeaderItem("Unknown 8 FF's", "??", fpos + footerstartpos) { length = 8 });
                                fpos += 0x8;

                                //ScriptFooterBlocks.Add(new ScriptHeaderItem("Label Table Offset", "??", fpos + footerstartpos) { length = 2 });
                                fpos += 0x2;

                                var stateFlagsBytes = footerdata.Slice(fpos, 0x4);
                                var stateFlags = (EStateFlags)EndianReader.ToInt32(stateFlagsBytes, 0, export.FileRef.Endian);
                                var names = stateFlags.ToString().Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                fpos += 0x4;

                                var numMappedFunctions = EndianReader.ToInt32(footerdata, fpos, export.FileRef.Endian);
                                fpos += 4;
                                for (int i = 0; i < numMappedFunctions; i++)
                                {
                                    var name = EndianReader.ToInt32(footerdata, fpos, export.FileRef.Endian);
                                    var uindex = EndianReader.ToInt32(footerdata, fpos + 8, export.FileRef.Endian);
                                    var funcMapText = $"{export.FileRef.GetNameEntry(name)} => {export.FileRef.GetEntry(uindex)?.FullPath}()";
                                    fpos += 12;
                                }
                            }
                        }
                        else if (export.FileRef.Game == MEGame.ME1 || export.FileRef.Game == MEGame.ME2)
                        {
                            //Header
                            int pos = 16;

                            var nextItemCompilingChain = EndianReader.ToInt32(data, pos, export.FileRef.Endian);
                            //ScriptHeaderBlocks.Add(new ScriptHeaderItem("Next item in loading chain", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? export : null));

                            pos += 8;
                            nextItemCompilingChain = EndianReader.ToInt32(data, pos, export.FileRef.Endian);
                            //ScriptHeaderBlocks.Add(new ScriptHeaderItem("Children Probe Start", nextItemCompilingChain, pos, nextItemCompilingChain > 0 ? export : null));

                            pos += 8;
                            var line = EndianReader.ToInt32(data, pos, export.FileRef.Endian);
                            //ScriptHeaderBlocks.Add(new ScriptHeaderItem("Line", EndianReader.ToInt32(data, pos, export.FileRef.Endian), pos));

                            pos += 4;
                            //EndianReader.ToInt32(data, pos, export.FileRef.Endian)
                            //ScriptHeaderBlocks.Add(new ScriptHeaderItem("TextPos", EndianReader.ToInt32(data, pos, export.FileRef.Endian), pos));

                            pos += 4;
                            int scriptSize = EndianReader.ToInt32(data, pos, export.FileRef.Endian);
                            //ScriptHeaderBlocks.Add(new ScriptHeaderItem("Script Size", scriptSize, pos));
                            pos += 4;
                            var BytecodeStart = pos;
                            var func = export.ClassName == "State" ? UE3FunctionReader.ReadState(export, data) : UE3FunctionReader.ReadFunction(export, data);
                            func.Decompile(new TextBuilder(), false); //parse bytecode

                            bool defined = func.HasFlag("Defined");
                            //if (defined)
                            //{
                            //    DecompiledScriptBlocks.Add(func.FunctionSignature + " {");
                            //}
                            //else
                            //{
                            //    //DecompiledScriptBlocks.Add(func.FunctionSignature);
                            //}
                            for (int i = 0; i < func.Statements.statements.Count; i++)
                            {
                                Statement s = func.Statements.statements[i];
                                s.SetPaddingForScriptSize(scriptSize);
                                if (s.Reader != null && i == 0)
                                {
                                    //Add tokens read from statement. All of them point to the same reader, so just do only the first one.
                                    s.Reader.ReadTokens.Select(x => x.ToBytecodeSingularToken(pos)).OrderBy(x => x.StartPos);
                                }
                            }

                            //if (defined)
                            //{
                            //    DecompiledScriptBlocks.Add("}");
                            //}

                            //Footer
                            pos = data.Length - (func.HasFlag("Net") ? 17 : 15);
                            string flagStr = func.GetFlags();
                            //ScriptFooterBlocks.Add(new ScriptHeaderItem("Native Index", EndianReader.ToInt16(data, pos, export.FileRef.Endian), pos));
                            pos += 2;

                            //ScriptFooterBlocks.Add(new ScriptHeaderItem("Operator Precedence", data[pos], pos));
                            pos++;

                            int functionFlags = EndianReader.ToInt32(data, pos, export.FileRef.Endian);
                            //ScriptFooterBlocks.Add(new ScriptHeaderItem("Flags", $"0x{functionFlags:X8} {flagStr}", pos));
                            pos += 4;

                            //if ((functionFlags & func._flagSet.GetMask("Net")) != 0)
                            //{
                            //ScriptFooterBlocks.Add(new ScriptHeaderItem("Unknown 1 (RepOffset?)", EndianReader.ToInt16(data, pos, export.FileRef.Endian), pos));
                            //pos += 2;
                            //}

                            int friendlyNameIndex = EndianReader.ToInt32(data, pos, export.FileRef.Endian);
                            var friendlyName = export.FileRef.GetNameEntry(friendlyNameIndex);
                            //ScriptFooterBlocks.Add(new ScriptHeaderItem("Friendly Name", Pcc.GetNameEntry(friendlyNameIndex), pos) { length = 8 });
                            pos += 8;

                            //ME1Explorer.Unreal.Classes.Function func = new ME1Explorer.Unreal.Classes.Function(data, export.FileRef as ME1Package);
                            //try
                            //{
                            //    Function_TextBox.Text = func.ToRawText();
                            //}
                            //catch (Exception e)
                            //{
                            //    Function_TextBox.Text = "Error parsing function: " + e.Message;
                            //}
                        }
                        else
                        {
                            //Function_TextBox.Text = "Parsing UnrealScript Functions for this game is not supported.";
                        }
                    }
                    //}
                }
            }
        }

        public static string RandomString(Random random, int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [TestMethod]
        public void TestNameOperations()
        {
            GlobalTest.Init();
            Random random = new Random();
            // Loads compressed packages, save them uncompressed. Load package, save re-compressed, compare results
            var packagesPath = GlobalTest.GetTestPackagesDirectory();
            //var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Do not use package caching in tests
                    Console.WriteLine($"Opening package {p}");
                    (var game, var platform) = GlobalTest.GetExpectedTypes(p);
                    if (platform == MEPackage.GamePlatform.PC) // Will expand in future, but not now.
                    {
                        var loadedPackage = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);
                        var afterLoadNameCount = loadedPackage.NameCount;
                        for (int i = 0; i < afterLoadNameCount; i++)
                        {
                            var existingName = loadedPackage.Names[i];
                            var existingNameIndex = loadedPackage.FindNameOrAdd(existingName);
                            Assert.IsTrue(existingNameIndex == i,
                                "An existing name was added when it shouldn't have been!");
                        }

                        // Test adding
                        for (int i = 0; i < 20; i++)
                        {
                            var expectedNameIndex = loadedPackage.NameCount;
                            var newName =
                                RandomString(random,
                                    35); // If we have a same-collision on 35 char random strings, let Mgamerz know he should buy a lottery ticket
                            var newNameIndex = loadedPackage.FindNameOrAdd(newName);
                            Assert.AreEqual(expectedNameIndex, newNameIndex,
                                "A name was added, but the index lookup was wrong!");
                        }

                        // Test changing
                        for (int i = 0; i < 20; i++)
                        {
                            var existingIndex = random.Next(loadedPackage.NameCount);
                            var newName = RandomString(random, 38); //even more entropy
                            loadedPackage.replaceName(existingIndex, newName);

                            // Check it's correct
                            var calculatedIndex = loadedPackage.FindNameOrAdd(newName);
                            Assert.AreEqual(existingIndex, calculatedIndex,
                                "A name was replaced, but the index of the replaced name was wrong when looked up via FindNameOrAdd()!");

                            var checkedNameGet = loadedPackage.GetNameEntry(calculatedIndex);
                            var checkedNameAccessor = loadedPackage.GetNameEntry(calculatedIndex);
                            Assert.AreEqual(newName, checkedNameGet,
                                "A name was replaced, but the GetNameEntry() for the replaced name returned the wrong name!");
                            Assert.AreEqual(newName, checkedNameAccessor,
                                "A name was replaced, but the Names[] array accessor for the replaced name returned the wrong name!");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void TestPartialPackageLoad()
        {
            bool ExportPredicate(ExportEntry exp) => exp.IsA("Texture") && exp.ObjectNameString.StartsWith("Holomod", StringComparison.OrdinalIgnoreCase);

            GlobalTest.Init();

            var le2StartupPackagePath = Path.Combine(GlobalTest.GetTestMiniGamePath(MEGame.LE2), @"BioGame\CookedPCConsole\Startup_INT.pcc");
            var partialPackage = MEPackageHandler.UnsafePartialLoad(le2StartupPackagePath, ExportPredicate);
            int numExportsLoaded = 0;
            foreach (ExportEntry export in partialPackage.Exports)
            {
                if (ExportPredicate(export))
                {
                    Assert.IsNotNull(export.Data);
                    numExportsLoaded++;
                }
                else
                {
                    Assert.ThrowsException<NullReferenceException>(() =>
                    {
                        byte[] _ = export.Data;
                    });
                }
            }
            Assert.AreEqual(2, numExportsLoaded);
        }
    }
}
