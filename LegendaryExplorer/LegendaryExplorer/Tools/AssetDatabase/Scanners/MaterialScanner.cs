using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorer.Tools.AssetDatabase.Filters;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{
    class MaterialScanner : AssetScanner
    {
        public MaterialScanner() : base()
        {
        }

        public override void ScanExport(ExportScanInfo e, ConcurrentAssetDB db, AssetDBScanOptions options)
        {
            if (e.IsDefault) return;
            
            if (e.ClassName is "Material" or "DecalMaterial")
            {
                var matUsage = new MatUsage(e.FileKey, e.Export.UIndex, e.IsDlc);
                if (db.GeneratedMats.TryGetValue(e.AssetKey, out MaterialRecord eMat))
                {
                    lock (eMat)
                    {
                        eMat.Usages.Add(matUsage);
                    }
                }
                else
                {
                    var mSets = GetMaterialSettings(e, db);
                    string parent;
                    if (e.Export.Game == MEGame.ME1 && e.FileName.EndsWith(".upk"))
                    {
                        parent = Path.GetFileNameWithoutExtension(e.FileName);
                    }
                    else
                    {
                        parent = GetTopParentPackage(e.Export);
                    }

                    var objectNameInstanced = e.ObjectNameInstanced;
                    if (e.ClassName == "DecalMaterial" && !objectNameInstanced.Contains("Decal"))
                    {
                        objectNameInstanced += "_Decal";
                    }

                    var NewMat = new MaterialRecord(objectNameInstanced, parent, e.IsDlc, mSets);
                    NewMat.Usages.Add(matUsage);
                    if (!db.GeneratedMats.TryAdd(e.AssetKey, NewMat))
                    {
                        var mat = db.GeneratedMats[e.AssetKey];
                        lock (mat)
                        {
                            mat.Usages.Add(matUsage);
                        }
                    }
                }
            }
        }

        private List<MatSetting> GetMaterialSettings(ExportScanInfo e, ConcurrentAssetDB db)
        {
            var mSets = new List<MatSetting>();
            if (e.ClassName == "Material" && !db.GeneratedMats.ContainsKey(e.ObjectNameInstanced) &&
                !e.IsDefault) //Run material settings
            {
                foreach (var p in e.Properties)
                {
                    TryCreateMatFilter(p, e, db);
                    MatSetting pSet;
                    var matSet_name = p.Name;
                    if (matSet_name == "Expressions")
                    {
                        foreach (var param in p as ArrayProperty<ObjectProperty>)
                        {
                            if (param.Value > 0)
                            {
                                var exprsn = e.Export.FileRef.GetUExport(param.Value);
                                var exprsnProps = exprsn.GetProperties();
                                var paramName = "n/a";
                                var paramNameProp = exprsnProps.GetProp<NameProperty>("ParameterName");
                                if (paramNameProp != null)
                                {
                                    paramName = paramNameProp.Value;
                                }

                                string exprsnName =
                                    exprsn.ClassName.Replace("MaterialExpression", string.Empty);
                                switch (exprsn.ClassName)
                                {
                                    case "MaterialExpressionScalarParameter":
                                        var sValue = exprsnProps.GetProp<FloatProperty>("DefaultValue");
                                        string defscalar = "n/a";
                                        if (sValue != null)
                                        {
                                            defscalar = sValue.Value.ToString();
                                        }

                                        pSet = new MatSetting(exprsnName, paramName, defscalar);
                                        break;
                                    case "MaterialExpressionVectorParameter":
                                        string linearColor = "n/a";
                                        var vValue = exprsnProps.GetProp<StructProperty>("DefaultValue");
                                        if (vValue != null)
                                        {
                                            var r = vValue.GetProp<FloatProperty>("R");
                                            var g = vValue.GetProp<FloatProperty>("G");
                                            var b = vValue.GetProp<FloatProperty>("B");
                                            var a = vValue.GetProp<FloatProperty>("A");
                                            if (r != null && g != null && b != null && a != null)
                                            {
                                                linearColor =
                                                    $"R:{r.Value} G:{g.Value} B:{b.Value} A:{a.Value}";
                                            }
                                        }

                                        pSet = new MatSetting(exprsnName, paramName, linearColor);
                                        break;
                                    default:
                                        pSet = new MatSetting(exprsnName, paramName, null);
                                        break;
                                }

                                mSets.Add(pSet);
                            }
                        }
                    }
                    else
                    {
                        pSet = new MatSetting(matSet_name, p.PropType.ToString(), GetPropertyValue(p, e.IsDefault, e.Export.FileRef));
                        mSets.Add(pSet);
                    }
                }
            }

            return mSets;
        }

        private void TryCreateMatFilter(Property p, ExportScanInfo e, ConcurrentAssetDB db)
        {
            if (p is BoolProperty bp)
            {
                if (!db.GeneratedMaterialSpecifications.ContainsKey(bp.Name))
                {
                    var filter = new MaterialBoolSpec(bp);
                    db.GeneratedMaterialSpecifications.TryAdd(bp.Name, filter);
                }
            }
        }
        private static string GetPropertyValue(Property p, bool isDefault, IMEPackage pcc)
        {
            string pValue = null;
            switch (p)
            {
                case ArrayPropertyBase parray:
                    pValue = "Array";
                    break;
                case StructProperty pstruct:
                    pValue = "Struct";
                    break;
                case NoneProperty pnone:
                    pValue = "None";
                    break;
                case ObjectProperty pobj:
                    if (pcc.IsEntry(pobj.Value))
                    {
                        pValue = pcc.GetEntry(pobj.Value).ClassName;
                    }

                    break;
                case BoolProperty pbool:
                    pValue = pbool.Value.ToString();
                    break;
                case IntProperty pint:
                    if (isDefault)
                    {
                        pValue = pint.Value.ToString();
                    }
                    else
                    {
                        pValue = "int"; //Keep DB size down
                    }

                    break;
                case FloatProperty pflt:
                    if (isDefault)
                    {
                        pValue = pflt.Value.ToString();
                    }
                    else
                    {
                        pValue = "float"; //Keep DB size down
                    }

                    break;
                case NameProperty pnme:
                    pValue = pnme.Value.ToString();
                    break;
                case ByteProperty pbte:
                    pValue = pbte.Value.ToString();
                    break;
                case EnumProperty penum:
                    pValue = penum.Value.ToString();
                    break;
                case StrProperty pstr:
                    if (isDefault)
                    {
                        pValue = pstr;
                    }
                    else
                    {
                        pValue = "string";
                    }

                    break;
                case StringRefProperty pstrref:
                    if (isDefault)
                    {
                        pValue = pstrref.Value.ToString();
                    }
                    else
                    {
                        pValue = "TLK StringRef";
                    }

                    break;
                case DelegateProperty pdelg:
                    var pscrdel = pdelg.Value.ContainingObjectUIndex;
                    if (pscrdel != 0)
                    {
                        pValue = pcc.GetEntry(pscrdel).ClassName;
                    }
                    break;
                default:
                    pValue = p.ToString();
                    break;
            }
            return pValue;
        }
    }
}
