using System.Runtime.InteropServices;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{
    class ClassScanner : AssetScanner
    {
        public ClassScanner() : base()
        {
        }

        public override void ScanExport(ExportScanInfo e, ConcurrentAssetDB db, AssetDBScanOptions options)
        {
            if (e.ClassName != "Class")
            {
                IMEPackage pcc = e.Export.FileRef;
                var data = e.Export.DataReadOnly;
                int pos = e.Export.GetPropertyStart();
                bool modernEngineVersion = pcc.Game >= MEGame.ME3 || pcc.Platform == MEPackage.GamePlatform.PS3;
                var classUsage = new ClassUsage(e.FileKey, e.Export.UIndex, e.IsDefault, e.IsMod);

                //providing a lambda that returns a new object prevents 100s of MBs of allocations
                //since a static lambda is allocated once, then cached
                lock (db.ClassLocks.GetOrAdd(e.ClassName, static _ => new object()))
                {
                    if (!db.GeneratedClasses.TryGetValue(e.ClassName, out ConcurrentAssetDB.ScanTimeClassRecord classRecord))
                    {
                        classRecord = new ConcurrentAssetDB.ScanTimeClassRecord { Class = e.ClassName, IsModOnly = e.IsMod };
                        db.GeneratedClasses[e.ClassName] = classRecord;
                    }
                    classRecord.Usages.Add(classUsage);

                    /* ExportScanInfo:Properties is lazy-loaded, since it is one of the most expensive parts of AssetDatabase generation.
                     * But lazy-loading is pointless if we get properties here, since this is run on every non-Class export.
                     * Luckily, we don't actually need the full PropertyCollection here, just the name and type of the top-level properties.
                     * Those can be read with a simple manual parse.
                     * This could be done outside the lock, but that would create a LOT of garbage,
                     * since more than 99% of PropertyRecords will be repeats, and only within the lock can we check if they already exist.
                     * This is a pretty fast loop, so avoiding the allocations is a greater performance boost than spending less time in the lock
                     */
                    while (pos + 8 < data.Length)
                    {
                        string propName = pcc.GetNameEntry(MemoryMarshal.Read<int>(data[pos..]));
                        pos += 4;
                        if (propName == "")
                        {
                            //broken properties
                            break;
                        }
                        if (propName == "None")
                        {
                            break;
                        }
                        int num = MemoryMarshal.Read<int>(data[pos..]);
                        pos += 4;
                        if (pos + 12 >= data.Length)
                        {
                            //broken properties
                            break;
                        }
                        string propType = pcc.GetNameEntry(MemoryMarshal.Read<int>(data[pos..]));
                        pos += 8;
                        int size = MemoryMarshal.Read<int>(data[pos..]);
                        pos += 8 + size;
                        switch (propType)
                        {
                            case "StructProperty":
                                pos += 8;
                                break;
                            case "BoolProperty":
                                pos += modernEngineVersion ? 1 : 4;
                                break;
                            case "ByteProperty":
                                if (modernEngineVersion)
                                {
                                    pos += 8;
                                }
                                break;
                        }
                        string instancedPropName = new NameReference(propName, num).Instanced;
                        if (!classRecord.PropertyRecords.ContainsKey(instancedPropName))
                        {
                            classRecord.PropertyRecords.Add(instancedPropName, new PropertyRecord(instancedPropName, propType));
                        }
                    }
                }
            }
            else
            {
                var newClassRecord = new ConcurrentAssetDB.ScanTimeClassRecord(e.Export.ObjectName, e.FileKey, e.Export.UIndex, e.Export.SuperClassName) { IsModOnly = e.IsMod };
                var classUsage = new ClassUsage(e.FileKey, e.Export.UIndex, false, e.IsMod);
                var objectNameInstanced = e.ObjectNameInstanced;

                lock (db.ClassLocks.GetOrAdd(objectNameInstanced, new object()))
                {
                    if (db.GeneratedClasses.TryGetValue(objectNameInstanced, out ConcurrentAssetDB.ScanTimeClassRecord oldVal))
                    {
                        if (oldVal.DefinitionFile < 0) //fake classrecord, created when a usage was found
                        {
                            newClassRecord.Usages.AddRange(oldVal.Usages);
                            newClassRecord.Usages.Add(classUsage);
                            newClassRecord.PropertyRecords.AddRange(oldVal.PropertyRecords);
                            newClassRecord.IsModOnly = e.IsMod & oldVal.IsModOnly;
                            db.GeneratedClasses[objectNameInstanced] = newClassRecord;
                        }
                        else
                        {
                            oldVal.Usages.Add(classUsage);
                            oldVal.IsModOnly &= e.IsMod;
                        }
                    }
                    else
                    {
                        newClassRecord.Usages.Add(classUsage);
                        db.GeneratedClasses[objectNameInstanced] = newClassRecord;
                    }
                }
            }
        }
    }
}
