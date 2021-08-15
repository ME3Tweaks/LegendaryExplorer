using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

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
                var pList = new List<PropertyRecord>();
                foreach (var p in e.Properties)
                {
                    string pName = p.Name;
                    string pType = p.PropType.ToString();

                    var NewPropertyRecord = new PropertyRecord(pName, pType);
                    pList.Add(NewPropertyRecord);
                }

                var classUsage = new ClassUsage(e.FileKey, e.Export.UIndex, e.IsDefault, e.IsMod);
                lock (db.ClassLocks.GetOrAdd(e.ClassName, new object()))
                {
                    if (db.GeneratedClasses.TryGetValue(e.ClassName, out var oldVal))
                    {
                        oldVal.Usages.Add(classUsage);
                        foreach (var p in pList)
                        {
                            oldVal.PropertyRecords.TryAdd(p.Property, p);
                        }
                    }
                    else
                    {
                        var newVal = new ClassRecord { Class = e.ClassName, IsModOnly = e.IsMod };
                        newVal.Usages.Add(classUsage);
                        foreach (var p in pList)
                        {
                            newVal.PropertyRecords.TryAdd(p.Property, p);
                        }
                        db.GeneratedClasses[e.ClassName] = newVal;
                    }
                }
            }
            else
            {
                var newClassRecord = new ClassRecord(e.Export.ObjectName, e.FileKey, e.Export.UIndex, e.Export.SuperClassName) { IsModOnly = e.IsMod };
                var classUsage = new ClassUsage(e.FileKey, e.Export.UIndex, false, e.IsMod);
                var objectNameInstanced = e.ObjectNameInstanced;

                lock (db.ClassLocks.GetOrAdd(objectNameInstanced, new object()))
                {
                    if (db.GeneratedClasses.TryGetValue(objectNameInstanced, out ClassRecord oldVal))
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
