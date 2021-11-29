using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.Classes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LegendaryExplorerCore.Tests
{
    /// <summary>
    /// Tests Bio2DA operations
    /// </summary>
    [TestClass]
    public class Bio2DATests
    {
        [TestMethod]
        public void TestBasicReserializationOf2DAs()
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
                    var package = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);
                    //if (package.Platform != MEPackage.GamePlatform.PC)
                    //    continue;
                    //if (package.Game != MEGame.ME2)
                    //    continue;
                    foreach (var twoDAExp in package.Exports.Where(x => !x.IsDefaultObject && (x.ClassName == "Bio2DA" || x.ClassName == "Bio2DANumberedRows")))
                    {
                        Console.WriteLine($"Test Bio2DA reserialization {twoDAExp.InstancedFullPath}");

                        // Tests reserialization of data via the Bio2DA WriteToExport method.
                        var data = twoDAExp.Data;
                        var twoDA = new Bio2DA(twoDAExp);
                        twoDA.Write2DAToExport();
                        var newData = twoDAExp.Data;
                        if (!data.SequenceEqual(newData))
                        {
                            //package.Save(@"C:\users\mgame\desktop\2da.pcc");
#if DEBUG
                            DebugUtilities.CompareByteArrays(data, newData);
#endif
                            Assert.Fail($"Reserialization of 2DA {twoDA.Export.InstancedFullPath} in {p} failed via Bio2DA.Write2DAToExport(): Before and after data was different");
                        }
                    }
                }
            }
        }

        [TestMethod]
        public void TestModifying2DA()
        {
            GlobalTest.Init();
            Random r = new Random(); // This is probably not good test case design...
            // Loads compressed packages and attempts to enumerate every object's properties.
            var packagesPath = GlobalTest.GetTestPackagesDirectory();
            var packages = Directory.GetFiles(packagesPath, "*.*", SearchOption.AllDirectories);
            foreach (var p in packages)
            {
                if (p.RepresentsPackageFilePath())
                {
                    // Do not use package caching in tests
                    Console.WriteLine($"Opening package {p}");
                    var package = MEPackageHandler.OpenMEPackage(p, forceLoadFromDisk: true);
                    foreach (var twoDAExp in package.Exports.Where(x => !x.IsDefaultObject && (x.ClassName == "Bio2DA" || x.ClassName == "Bio2DANumberedRows")))
                    {
                        Console.WriteLine($"Test modifying {twoDAExp.InstancedFullPath}");
                        // Tests reserialization of data via the Bio2DA WriteToExport method.
                        var twoDA = new Bio2DA(twoDAExp);

                        // Modify stuff
                        for (var i = 0; i < twoDA.RowCount; i++)
                        {
                            for (int j = 0; j < twoDA.ColumnCount; j++)
                            {
                                if (r.Next(2) == 0)
                                {
                                    // Change value and type
                                    switch (r.Next(4))
                                    {
                                        case 0:
                                            twoDA[i, j].IntValue = r.Next();
                                            break;
                                        case 1:
                                            twoDA[i, j].NameValue = new NameReference(package.Names[r.Next(package.NameCount)], r.Next(int.MaxValue));
                                            break;
                                        case 2:
                                            twoDA[i, j].FloatValue = (float)r.NextDouble();
                                            break;
                                        case 3:
                                            twoDA[i, j].Type = Bio2DACell.Bio2DADataType.TYPE_NULL;
                                            break;
                                    }
                                }
                            }
                        }

                        // Write back to export
                        twoDA.Write2DAToExport();

                        // Test re-reading the 2DA to make sure it still works
                        var twoExpansion = new Bio2DA(twoDAExp);
                        Console.WriteLine($"Test expansion {twoDAExp.InstancedFullPath}");
                        var expectedCellCount = (twoExpansion.ColumnCount + 1) * (twoExpansion.RowCount + 1);
                        twoExpansion.AddRow("LECTESTROW");
                        twoExpansion.AddColumn("LECTESTCOL");

                        // Enumerate the amount of cells to check they are not null.
                        int actualCellCount = 0;
                        for (var i = 0; i < twoExpansion.RowCount; i++)
                        {
                            for (int j = 0; j < twoExpansion.ColumnCount; j++)
                            {
                                if (twoExpansion.Cells[i, j] != null)
                                    actualCellCount++;
                            }
                        }

                        Assert.AreEqual(expectedCellCount, actualCellCount, "Bio2DA row and column expansion failed! The amount of non-null (C#, not Bio2DACell) cells does not match the expected value");

                        // Attempt to re-add same-named columns and rows. The count should not change
                        twoExpansion.AddRow("LECTESTROW");
                        twoExpansion.AddColumn("LECTESTCOL");

                        // Enumerate the amount of cells to check they are not null.
                        actualCellCount = 0;
                        for (var i = 0; i < twoExpansion.RowCount; i++)
                        {
                            for (int j = 0; j < twoExpansion.ColumnCount; j++)
                            {
                                if (twoExpansion.Cells[i, j] != null)
                                    actualCellCount++;
                            }
                        }
                        Assert.AreEqual(expectedCellCount, actualCellCount, "Bio2DA row and column expansion expanded when it shouldn't have! It should not have added duplicate row/columns");
                    }
                }
            }
        }
    }
}
