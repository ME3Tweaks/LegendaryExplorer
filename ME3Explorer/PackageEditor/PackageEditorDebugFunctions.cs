using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ME3Explorer
{
    public partial class PackageEditor
    {
        private void dEBUGCopyAllBIOGItemsToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> consts = new List<string>();
            StringBuilder sb = new StringBuilder();

            foreach (IExportEntry exp in pcc.Exports)
            {
                if (exp.ClassName == "Const")
                {
                    consts.Add(exp.Index + " " + exp.GetFullPath);
                }
            }
            foreach (string str in consts)
            {
                sb.AppendLine(str);
            }
            try
            {
                string value = sb.ToString();
                if (value != null && value != "")
                {
                    Clipboard.SetText(value);
                    MessageBox.Show("Finished");
                }
                else
                {
                    MessageBox.Show("No results.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private List<string> ScanForBioG(string file)
        {
            //Console.WriteLine(file);
            try
            {
                List<string> biopawnscaled = new List<string>();
                IMEPackage pack = MEPackageHandler.OpenMEPackage(file);
                foreach (IExportEntry exp in pack.Exports)
                {
                    if (exp.ClassName == "BioPawnChallengeScaledType")
                    {
                        biopawnscaled.Add(exp.GetFullPath);
                    }
                }
                pack.Release();
                return biopawnscaled;
            }
            catch (Exception e)
            {
                Debugger.Break();
            }
            return null;
        }

        private void dEBUGExport2DAToExcelFileToolStripMenuItem_Click(object sender, EventArgs e)
        {




        }

        private void findExportsWithSerialSizeMismatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> serialexportsbad = new List<string>();
            foreach (IExportEntry entry in pcc.Exports)
            {
                Console.WriteLine(entry.Index + " " + entry.Data.Length + " " + entry.DataSize);
                if (entry.Data.Length != entry.DataSize)
                {
                    serialexportsbad.Add(entry.GetFullPath + " Header lists: " + entry.DataSize + ", Actual data size: " + entry.Data.Length);
                }
            }

            if (serialexportsbad.Count > 0)
            {
                ListWindow lw = new ListWindow(serialexportsbad, "Serial Size Mismatches", "The following exports had serial size mismatches.");
            }
            else
            {
                MessageBox.Show("No exports have serial size mismatches.");
            }
        }

        private void dEBUGAccessME3AppendsTableToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ME3Package me3 = (ME3Package)pcc;
            var offset = me3.DependsOffset;
        }

        private void dEBUGEnumerateAllClassesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (pcc != null)
            {
                foreach (IExportEntry exp in pcc.Exports)
                {
                    if (exp.ClassName == "Class")
                    {
                        Debug.WriteLine("Testing " + exp.Index + " " + exp.GetFullPath);
                        binaryInterpreterControl.export = exp;
                        binaryInterpreterControl.InitInterpreter();
                    }
                }
            }
        }

        private void dEBUGCopyConfigurablePropsToClipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string fpath = @"X:\Mass Effect Games HDD\Mass Effect";
            var ext = new List<string> { "u", "upk", "sfm" };
            var files = Directory.GetFiles(fpath, "*.*", SearchOption.AllDirectories)
              .Where(file => new string[] { ".sfm", ".upk", ".u" }
              .Contains(Path.GetExtension(file).ToLower()))
              .ToList();
            StringBuilder sb = new StringBuilder();

            int threads = Environment.ProcessorCount;
            string[] results = files.AsParallel().WithDegreeOfParallelism(threads).WithExecutionMode(ParallelExecutionMode.ForceParallelism).Select(ScanForConfigValues).ToArray();


            foreach (string res in results)
            {
                sb.Append(res);
            }
            try
            {
                Clipboard.SetText(sb.ToString());
                MessageBox.Show("Finished");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


        private void dEBUGCallReadPropertiesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int n;
            if (!GetSelected(out n))
            {
                return;
            }
            if (n >= 0)
            {
                try
                {
                    IExportEntry exp = pcc.Exports[n];
                    exp.GetProperties(true); //force properties to reload
                }
                catch (Exception ex)
                {

                }
            }
        }

        /// <summary>
        /// Attempts to relink unreal binary data to object pointers if they are part of the clone tree.
        /// It's gonna be an ugly mess.
        /// </summary>
        /// <param name="importpcc">PCC being imported from</param>
        private List<string> relinkBinaryObjects(IMEPackage importpcc)
        {
            List<string> relinkFailedReport = new List<string>();
            foreach (KeyValuePair<int, int> entry in crossPCCObjectMap)
            {
                if (entry.Key > 0)
                {
                    IExportEntry exp = pcc.getExport(entry.Value);
                    byte[] binarydata = exp.getBinaryData();
                    if (binarydata.Length > 0)
                    {
                        //has binary data
                        //This is temporary until I find a more permanent style for relinking binary
                        try
                        {
                            switch (exp.ClassName)
                            {
                                case "WwiseEvent":
                                    {
                                        int count = BitConverter.ToInt32(binarydata, 0);
                                        for (int i = 0; i < count; i++)
                                        {
                                            int originalValue = BitConverter.ToInt32(binarydata, 4 + (i * 4));
                                            int currentObjectRef = unrealIndexToME3ExpIndexing(originalValue); //count + i * intsize
                                            int mapped;
                                            bool isMapped = crossPCCObjectMap.TryGetValue(currentObjectRef, out mapped);
                                            if (isMapped)
                                            {
                                                mapped = me3ExpIndexingToUnreal(mapped, originalValue < 0); //if the value is 0, it would have an error anyways.
                                                Debug.WriteLine("Binary relink hit for WwiseEvent Export " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + originalValue + " -> " + (mapped + 1));
                                                WriteMem(4 + (i * 4), binarydata, BitConverter.GetBytes(mapped));
                                                int newValue = BitConverter.ToInt32(binarydata, 4 + (i * 4));
                                                Debug.WriteLine(originalValue + " -> " + newValue);
                                            }
                                            else
                                            {
                                                Debug.WriteLine("Binary relink missed WwiseEvent Export " + exp.UIndex + " 0x" + (4 + (i * 4)).ToString("X6") + " " + originalValue);
                                            }
                                        }
                                        exp.setBinaryData(binarydata);
                                    }
                                    break;
                                case "Class":
                                    {
                                        if (exp.FileRef.Game != importpcc.Game)
                                        {
                                            //Cannot relink against a different game.
                                            continue;
                                        }
                                        IExportEntry importingExp = importpcc.getExport(entry.Key);
                                        if (importingExp.ClassName != "Class")
                                        {
                                            continue; //the class was not actually set, so this is not really class.
                                        }

                                        //This is going to be pretty ugly
                                        try
                                        {
                                            byte[] newdata = importpcc.Exports[entry.Key].Data; //may need to rewrite first unreal header
                                            byte[] data = importpcc.Exports[entry.Key].Data;

                                            int offset = 0;
                                            int unrealExportIndex = BitConverter.ToInt32(data, offset);
                                            offset += 4;


                                            int superclassIndex = BitConverter.ToInt32(data, offset);
                                            if (superclassIndex < 0)
                                            {
                                                //its an import
                                                ImportEntry superclassImportEntry = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(superclassIndex))];
                                                ImportEntry newSuperclassValue = getOrAddCrossImport(superclassImportEntry.GetFullPath, importpcc, exp.FileRef);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(newSuperclassValue.UIndex));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Superclass is an export in the source package and was not relinked.");
                                            }


                                            int unknown1 = BitConverter.ToInt32(data, offset);
                                            offset += 4;

                                            int classObjTree = BitConverter.ToInt32(data, offset); //Don't know if I need to do this.
                                            offset += 4;


                                            //I am not sure what these mean. However if Pt1&2 are 33/25, the following bytes that follow are extended.
                                            int headerUnknown1 = BitConverter.ToInt32(data, offset);
                                            Int64 ignoreMask = BitConverter.ToInt64(data, offset);
                                            offset += 8;

                                            Int16 labelOffset = BitConverter.ToInt16(data, offset);
                                            offset += 2;
                                            int skipAmount = 0x6;
                                            //Find end of script block. Seems to be 10 FF's.
                                            while (offset + skipAmount + 10 < data.Length)
                                            {
                                                //Debug.WriteLine("Cheecking at 0x"+(offset + skipAmount + 10).ToString("X4"));
                                                bool isEnd = true;
                                                for (int i = 0; i < 10; i++)
                                                {
                                                    byte b = data[offset + skipAmount + i];
                                                    if (b != 0xFF)
                                                    {
                                                        isEnd = false;
                                                        break;
                                                    }
                                                }
                                                if (isEnd)
                                                {
                                                    break;
                                                }
                                                else
                                                {
                                                    skipAmount++;
                                                }
                                            }

                                            int offsetEnd = offset + skipAmount + 10;
                                            offset += skipAmount + 10; //heuristic to find end of script
                                            uint stateMask = BitConverter.ToUInt32(data, offset);
                                            offset += 4;

                                            int localFunctionsTableCount = BitConverter.ToInt32(data, offset);
                                            offset += 4;
                                            bool isMapped;
                                            for (int i = 0; i < localFunctionsTableCount; i++)
                                            {
                                                int nameTableIndex = BitConverter.ToInt32(data, offset);
                                                int nameIndex = BitConverter.ToInt32(data, offset + 4);
                                                NameReference importingName = importpcc.getNameEntry(nameTableIndex);
                                                int newFuncName = exp.FileRef.FindNameOrAdd(importingName);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(newFuncName));
                                                offset += 8;

                                                int functionObjectIndex = unrealIndexToME3ExpIndexing(BitConverter.ToInt32(data, offset));
                                                int mapped;
                                                isMapped = crossPCCObjectMap.TryGetValue(functionObjectIndex, out mapped);
                                                if (isMapped)
                                                {
                                                    mapped = me3ExpIndexingToUnreal(mapped, functionObjectIndex < 0); //if the value is 0, it would have an error anyways.
                                                    WriteMem(offset, newdata, BitConverter.GetBytes(mapped));
                                                }
                                                else
                                                {
                                                    relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Local function[" + i + "] could not be remapped during porting: " + functionObjectIndex + " is not in the mapping tree and could not be relinked");
                                                }
                                                offset += 4;
                                            }

                                            int classMask = BitConverter.ToInt32(data, offset);
                                            offset += 4;
                                            if (importpcc.Game != MEGame.ME3)
                                            {
                                                offset += 1; //seems to be a blank byte here
                                            }

                                            int coreReference = BitConverter.ToInt32(data, offset);
                                            if (coreReference < 0)
                                            {
                                                //its an import
                                                ImportEntry outerclassReferenceImport = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(coreReference))];
                                                ImportEntry outerclassNewImport = getOrAddCrossImport(outerclassReferenceImport.GetFullPath, importpcc, exp.FileRef);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(outerclassNewImport.UIndex));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Outerclass is an export in the original package, not relinked.");
                                            }
                                            offset += 4;


                                            if (importpcc.Game == MEGame.ME3)
                                            {
                                                offset = ClassParser_RelinkComponentsTable(importpcc, exp, relinkFailedReport, ref newdata, offset);
                                                offset = ClassParser_ReadImplementsTable(importpcc, exp, relinkFailedReport, ref newdata, offset);
                                                int postComponentsNoneNameIndex = BitConverter.ToInt32(data, offset);
                                                int postComponentNoneIndex = BitConverter.ToInt32(data, offset + 4);
                                                string postCompName = importpcc.getNameEntry(postComponentsNoneNameIndex); //This appears to be unused in ME3, it is always None it seems.
                                                int newFuncName = exp.FileRef.FindNameOrAdd(postCompName);
                                                WriteMem(offset, newdata, BitConverter.GetBytes(newFuncName));
                                                offset += 8;

                                                int unknown4 = BitConverter.ToInt32(data, offset);
                                                offset += 4;
                                            }
                                            else
                                            {
                                                offset = ClassParser_ReadImplementsTable(importpcc, exp, relinkFailedReport, ref data, offset);
                                                offset = ClassParser_RelinkComponentsTable(importpcc, exp, relinkFailedReport, ref data, offset);

                                                int me12unknownend1 = BitConverter.ToInt32(data, offset);
                                                offset += 4;

                                                int me12unknownend2 = BitConverter.ToInt32(data, offset);
                                                offset += 4;
                                            }

                                            int defaultsClassLink = unrealIndexToME3ExpIndexing(BitConverter.ToInt32(data, offset));
                                            int defClassLink;
                                            isMapped = crossPCCObjectMap.TryGetValue(defaultsClassLink, out defClassLink);
                                            if (isMapped)
                                            {
                                                defClassLink = me3ExpIndexingToUnreal(defClassLink, defaultsClassLink < 0); //if the value is 0, it would have an error anyways.
                                                WriteMem(offset, newdata, BitConverter.GetBytes(defClassLink));
                                            }
                                            else
                                            {
                                                relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: DefaultsClassLink cannot be currently automatically relinked by Binary Relinker. Please manually set this in Binary Editor");
                                            }
                                            offset += 4;

                                            if (importpcc.Game == MEGame.ME3)
                                            {
                                                int functionsTableCount = BitConverter.ToInt32(data, offset);
                                                offset += 4;

                                                for (int i = 0; i < functionsTableCount; i++)
                                                {
                                                    int functionsTableIndex = unrealIndexToME3ExpIndexing(BitConverter.ToInt32(data, offset));
                                                    int mapped;
                                                    isMapped = crossPCCObjectMap.TryGetValue(functionsTableIndex, out mapped);
                                                    if (isMapped)
                                                    {
                                                        mapped = me3ExpIndexingToUnreal(mapped, functionsTableIndex < 0); //if the value is 0, it would have an error anyways.
                                                        WriteMem(offset, newdata, BitConverter.GetBytes(mapped));
                                                    }
                                                    else
                                                    {
                                                        if (functionsTableIndex < 0)
                                                        {
                                                            ImportEntry functionObjIndex = importpcc.Imports[Math.Abs(functionsTableIndex)];
                                                            ImportEntry newFunctionObjIndex = getOrAddCrossImport(functionObjIndex.GetFullPath, importpcc, exp.FileRef);
                                                            WriteMem(offset, newdata, BitConverter.GetBytes(newFunctionObjIndex.UIndex));
                                                        }
                                                        else
                                                        {
                                                            relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Full Functions List function[" + i + "] could not be remapped during porting: " + functionsTableIndex + " is not in the mapping tree and could not be relinked");

                                                        }
                                                    }
                                                    offset += 4;
                                                }
                                            }
                                            exp.Data = newdata;
                                        }
                                        catch (Exception ex)
                                        {
                                            relinkFailedReport.Add(exp.Index + " " + exp.GetFullPath + " binary relink error: Exception relinking: " + ex.Message);
                                        }
                                    }
                                    break;
                                default:
                                    continue;
                            }
                        }
                        catch (Exception e)
                        {
                            relinkFailedReport.Add("Binary relinking failed for " + exp.Index + " " + exp.GetFullPath + ":" + e.Message);
                        }
                        //Run an interpreter pass over it - we will find objectleafnodes and attempt to update the same offset in the destination file.
                        //BinaryInterpreter binaryrelinkInterpreter = new ME3Explorer.BinaryInterpreter(importpcc, importpcc.Exports[entry.Key], pcc, pcc.Exports[entry.Value], crossPCCObjectMapping);
                    }
                }
            }
            return relinkFailedReport;
        }

        private int ClassParser_RelinkComponentsTable(IMEPackage importpcc, IExportEntry exp, List<string> relinkFailedReport, ref byte[] data, int offset)
        {
            if (importpcc.Game == MEGame.ME3)
            {
                int componentTableNameIndex = BitConverter.ToInt32(data, offset);
                int componentTableIndex = BitConverter.ToInt32(data, offset + 4);
                NameReference importingName = importpcc.getNameEntry(componentTableNameIndex);
                int newComponentTableName = exp.FileRef.FindNameOrAdd(importingName);
                WriteMem(offset, data, BitConverter.GetBytes(newComponentTableName));
                offset += 8;

                int componentTableCount = BitConverter.ToInt32(data, offset);
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    int nameIndex = BitConverter.ToInt32(data, offset + 4);
                    importingName = importpcc.getNameEntry(nameTableIndex);
                    int componentName = exp.FileRef.FindNameOrAdd(importingName);
                    WriteMem(offset, data, BitConverter.GetBytes(componentName));
                    offset += 8;

                    int componentObjectIndex = BitConverter.ToInt32(data, offset);
                    int mapped;
                    bool isMapped = crossPCCObjectMap.TryGetValue(componentObjectIndex, out mapped);
                    if (isMapped)
                    {
                        mapped = me3ExpIndexingToUnreal(mapped, componentObjectIndex < 0); //if the value is 0, it would have an error anyways.
                        WriteMem(offset, data, BitConverter.GetBytes(mapped));
                    }
                    else
                    {
                        if (componentObjectIndex < 0)
                        {
                            ImportEntry componentObjectImport = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(componentObjectIndex))];
                            ImportEntry newComponentObjectImport = getOrAddCrossImport(componentObjectImport.GetFullPath, importpcc, exp.FileRef);
                            WriteMem(offset, data, BitConverter.GetBytes(newComponentObjectImport.UIndex));
                        }
                        else
                        {
                            relinkFailedReport.Add("Binary Class Component[" + i + "] could not be remapped during porting: " + componentObjectIndex + " is not in the mapping tree");
                        }
                    }
                    offset += 4;
                }
            }
            else
            {
                int componentTableCount = BitConverter.ToInt32(data, offset);
                offset += 4;

                for (int i = 0; i < componentTableCount; i++)
                {
                    int nameTableIndex = BitConverter.ToInt32(data, offset);
                    int nameIndex = BitConverter.ToInt32(data, offset + 4);

                    offset += 8;

                    int componentObjectIndex = BitConverter.ToInt32(data, offset);
                    if (componentObjectIndex != 0)
                    {
                        int mapped;
                        bool isMapped = crossPCCObjectMap.TryGetValue(componentObjectIndex, out mapped);
                        if (isMapped)
                        {
                            mapped = me3ExpIndexingToUnreal(mapped, componentObjectIndex < 0); //if the value is 0, it would have an error anyways.
                            WriteMem(offset, data, BitConverter.GetBytes(mapped));
                        }
                        else
                        {
                            if (componentObjectIndex < 0)
                            {
                                ImportEntry componentObjectImport = importpcc.Imports[Math.Abs(unrealIndexToME3ExpIndexing(componentObjectIndex))];
                                ImportEntry newComponentObjectImport = getOrAddCrossImport(componentObjectImport.GetFullPath, importpcc, exp.FileRef);
                                WriteMem(offset, data, BitConverter.GetBytes(newComponentObjectImport.UIndex));
                            }
                            else
                            {
                                relinkFailedReport.Add("Binary Class Component[" + i + "] could not be remapped during porting: " + componentObjectIndex + " is not in the mapping tree");
                            }
                        }
                    }
                    offset += 4;
                }
            }
            return offset;
        }

        private int ClassParser_ReadImplementsTable(IMEPackage importpcc, IExportEntry exp, List<string> relinkFailedReport, ref byte[] data, int offset)
        {
            if (importpcc.Game == MEGame.ME3)
            {
                int interfaceCount = BitConverter.ToInt32(data, offset);
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceIndex = BitConverter.ToInt32(data, offset);
                    int mapped;
                    bool isMapped = crossPCCObjectMap.TryGetValue(interfaceIndex, out mapped);
                    if (isMapped)
                    {
                        mapped = me3ExpIndexingToUnreal(mapped, interfaceIndex < 0); //if the value is 0, it would have an error anyways.
                        WriteMem(offset, data, BitConverter.GetBytes(mapped));
                    }
                    else
                    {
                        relinkFailedReport.Add("Binary Class Interface Index[" + i + "] could not be remapped during porting: " + interfaceIndex + " is not in the mapping tree");
                    }
                    offset += 4;

                    //propertypointer
                    interfaceIndex = BitConverter.ToInt32(data, offset);
                    isMapped = crossPCCObjectMap.TryGetValue(interfaceIndex, out mapped);
                    if (isMapped)
                    {
                        mapped = me3ExpIndexingToUnreal(mapped, interfaceIndex < 0); //if the value is 0, it would have an error anyways.
                        WriteMem(offset, data, BitConverter.GetBytes(mapped));
                    }
                    else
                    {
                        relinkFailedReport.Add("Binary Class Interface Index[" + i + "] could not be remapped during porting: " + interfaceIndex + " is not in the mapping tree");
                    }
                    offset += 4;
                }
            }
            else
            {
                int interfaceTableName = BitConverter.ToInt32(data, offset); //????
                NameReference importingName = importpcc.getNameEntry(interfaceTableName);
                int interfaceName = exp.FileRef.FindNameOrAdd(importingName);
                WriteMem(offset, data, BitConverter.GetBytes(interfaceName));
                offset += 8;

                int interfaceCount = BitConverter.ToInt32(data, offset);
                offset += 4;
                for (int i = 0; i < interfaceCount; i++)
                {
                    int interfaceNameIndex = BitConverter.ToInt32(data, offset);
                    importingName = importpcc.getNameEntry(interfaceNameIndex);
                    interfaceName = exp.FileRef.FindNameOrAdd(importingName);
                    WriteMem(offset, data, BitConverter.GetBytes(interfaceName));
                    offset += 8;
                }
            }
            return offset;
        }
    }
}
