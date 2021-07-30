using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using LegendaryExplorer.Tools.AssetDatabase.Scanners;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.ME1;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.Classes;

namespace LegendaryExplorer.Tools.AssetDatabase
{
    /// <summary>
    /// Scans a single package file, adding all found records into a generated database
    /// </summary>
    public class ClassScanSingleFileTask
    {
        public string ShortFileName { get; }

        public ClassScanSingleFileTask(string file, int filekey, bool scanCRC, bool scanLines, bool scanPlotUsages)
        {
            File = file;
            ShortFileName = Path.GetFileNameWithoutExtension(file);
            FileKey = filekey;
            ScanCRC = scanCRC;
            ScanLines = scanLines;
            ScanPlotUsages = scanPlotUsages;
        }

        public bool DumpCanceled;
        private readonly int FileKey;
        private readonly string File;
        private readonly bool ScanCRC;
        private readonly bool ScanLines;
        private readonly bool ScanPlotUsages;

        /// <summary>
        /// Dumps Property data to concurrent dictionary
        /// </summary>
        public void dumpPackageFile(MEGame GameBeingDumped, ConcurrentAssetDB dbScanner)
        {
            PlotUsageScanner puScanner = new PlotUsageScanner(dbScanner);

            try
            {
                using IMEPackage pcc = MEPackageHandler.OpenMEPackage(File);
                if (pcc.Game != GameBeingDumped)
                {
                    return; //rogue file from other game or UDK
                }

                bool IsDLC = pcc.IsInOfficialDLC();
                bool IsMod = !pcc.IsInBasegame() && !IsDLC;
                //foreach (IEntry entry in pcc.Exports.Concat<IEntry>(pcc.Imports))
                foreach (ExportEntry entry in pcc.Exports)
                {
                    if (DumpCanceled || pcc.FilePath.Contains("_LOC_") && !pcc.FilePath.Contains("INT")
                    ) //TEMP NEED BETTER WAY TO HANDLE LANGUAGES
                    {
                        return;
                    }

                    try
                    {
                        string className = entry.ClassName; //Handle basic class record
                        string objectNameInstanced = entry.ObjectName.Instanced;
                        int uindex = entry.UIndex;
                        var export = entry as ExportEntry;
                        if (className != "Class")
                        {
                            bool isDefault = export?.IsDefaultObject == true;

                            var pList = new List<PropertyRecord>();
                            var mSets = new List<MatSetting>();
                            PropertyCollection props = null;
                            if (export is not null)
                            {
                                props = export.GetProperties(false, false);
                                foreach (var p in props)
                                {
                                    string pName = p.Name;
                                    string pType = p.PropType.ToString();
                                    string pValue = "null";
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
                                            if (pdelg.Value != null)
                                            {
                                                var pscrdel = pdelg.Value.Object;
                                                if (pscrdel != 0)
                                                {
                                                    pValue = pcc.GetEntry(pscrdel).ClassName;
                                                }
                                            }

                                            break;
                                        default:
                                            pValue = p.ToString();
                                            break;
                                    }

                                    var NewPropertyRecord = new PropertyRecord(pName, pType);
                                    pList.Add(NewPropertyRecord);

                                    if (entry.ClassName == "Material" && !dbScanner.GeneratedMats.ContainsKey(objectNameInstanced) &&
                                        !isDefault) //Run material settings
                                    {
                                        MatSetting pSet;
                                        var matSet_name = p.Name;
                                        if (matSet_name == "Expressions")
                                        {
                                            foreach (var param in p as ArrayProperty<ObjectProperty>)
                                            {
                                                if (param.Value > 0)
                                                {
                                                    var exprsn = pcc.GetUExport(param.Value);
                                                    var paramName = "n/a";
                                                    var paramNameProp = exprsn.GetProperty<NameProperty>("ParameterName");
                                                    if (paramNameProp != null)
                                                    {
                                                        paramName = paramNameProp.Value;
                                                    }

                                                    string exprsnName =
                                                        exprsn.ClassName.Replace("MaterialExpression", string.Empty);
                                                    switch (exprsn.ClassName)
                                                    {
                                                        case "MaterialExpressionScalarParameter":
                                                            var sValue = exprsn.GetProperty<FloatProperty>("DefaultValue");
                                                            string defscalar = "n/a";
                                                            if (sValue != null)
                                                            {
                                                                defscalar = sValue.Value.ToString();
                                                            }

                                                            pSet = new MatSetting(exprsnName, paramName, defscalar);
                                                            break;
                                                        case "MaterialExpressionVectorParameter":
                                                            string linearColor = "n/a";
                                                            var vValue = exprsn.GetProperty<StructProperty>("DefaultValue");
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
                                            pSet = new MatSetting(matSet_name, pType, pValue);
                                            mSets.Add(pSet);
                                        }
                                    }
                                }

                            }

                            var classUsage = new ClassUsage(FileKey, uindex, isDefault, IsMod);
                            lock (dbScanner.ClassLocks.GetOrAdd(className, new object()))
                            {
                                if (dbScanner.GeneratedClasses.TryGetValue(className, out var oldVal))
                                {
                                    oldVal.Usages.Add(classUsage);
                                    foreach (PropertyRecord propRecord in pList)
                                    {
                                        if (!oldVal.PropertyRecords.Contains(propRecord))
                                        {
                                            oldVal.PropertyRecords.Add(propRecord);
                                        }
                                    }
                                }
                                else
                                {
                                    var newVal = new ClassRecord { Class = className, IsModOnly = IsMod };
                                    newVal.Usages.Add(classUsage);
                                    newVal.PropertyRecords.AddRange(pList);
                                    dbScanner.GeneratedClasses[className] = newVal;
                                }
                            }

                            if (isDefault)
                            {
                                continue;
                            }

                            string assetKey = entry.InstancedFullPath.ToLower();

                            if (className == "Material" || className == "DecalMaterial")
                            {
                                var matUsage = new MatUsage(FileKey, uindex, IsDLC);
                                if (dbScanner.GeneratedMats.TryGetValue(assetKey, out var eMat))
                                {
                                    lock (eMat)
                                    {
                                        eMat.Usages.Add(matUsage);
                                    }
                                }
                                else
                                {

                                    string parent;
                                    if (GameBeingDumped == MEGame.ME1 && File.EndsWith(".upk"))
                                    {
                                        parent = ShortFileName;
                                    }
                                    else
                                    {
                                        parent = GetTopParentPackage(entry);
                                    }

                                    if (className == "DecalMaterial" && !objectNameInstanced.Contains("Decal"))
                                    {
                                        objectNameInstanced += "_Decal";
                                    }

                                    var NewMat = new MaterialRecord(objectNameInstanced, parent, IsDLC, mSets);
                                    NewMat.Usages.Add(matUsage);
                                    if (!dbScanner.GeneratedMats.TryAdd(assetKey, NewMat))
                                    {
                                        var mat = dbScanner.GeneratedMats[assetKey];
                                        lock (mat)
                                        {
                                            mat.Usages.Add(matUsage);
                                        }
                                    }
                                }
                            }
                            else if (className == "AnimSequence" || className == "SFXAmbPerfGameData")
                            {
                                var animUsage = new AnimUsage(FileKey, uindex, IsMod);
                                if (dbScanner.GeneratedAnims.TryGetValue(assetKey, out var anim))
                                {
                                    lock (anim)
                                    {
                                        anim.Usages.Add(animUsage);
                                    }
                                }
                                else
                                {
                                    string aSeq = null;
                                    string aGrp = "None";
                                    float aLength = 0;
                                    int aFrames = 0;
                                    string aComp = "None";
                                    string aKeyF = "None";
                                    bool IsAmbPerf = false;
                                    if (className == "AnimSequence")
                                    {
                                        var pSeq = props.GetProp<NameProperty>("SequenceName");
                                        if (pSeq != null)
                                        {
                                            aSeq = pSeq.Value.Instanced;
                                            aGrp = objectNameInstanced.Replace($"{aSeq}_", null);
                                        }

                                        var pLength = props.GetProp<FloatProperty>("SequenceLength");
                                        aLength = pLength?.Value ?? 0;

                                        var pFrames = props.GetProp<IntProperty>("NumFrames");
                                        aFrames = pFrames?.Value ?? 0;

                                        var pComp = props.GetProp<EnumProperty>("RotationCompressionFormat");
                                        aComp = pComp?.Value.ToString() ?? "None";

                                        var pKeyF = props.GetProp<EnumProperty>("KeyEncodingFormat");
                                        aKeyF = pKeyF?.Value.ToString() ?? "None";
                                    }
                                    else //is ambient performance
                                    {
                                        IsAmbPerf = true;
                                        aSeq = "Multiple";
                                        var pAnimsets = props.GetProp<ArrayProperty<StructProperty>>("m_aAnimsets");
                                        aFrames = pAnimsets?.Count ?? 0;
                                    }

                                    var NewAnim = new AnimationRecord(objectNameInstanced, aSeq, aGrp, aLength, aFrames, aComp, aKeyF, IsAmbPerf, IsMod);
                                    NewAnim.Usages.Add(animUsage);
                                    if (!dbScanner.GeneratedAnims.TryAdd(assetKey, NewAnim))
                                    {
                                        var a = dbScanner.GeneratedAnims[assetKey];
                                        lock (a)
                                        {
                                            a.Usages.Add(animUsage);
                                        }
                                    }
                                }
                            }
                            else if (className == "SkeletalMesh" || className == "StaticMesh")
                            {
                                var meshUsage = new MeshUsage(FileKey, uindex, IsMod);
                                if (dbScanner.GeneratedMeshes.ContainsKey(assetKey))
                                {
                                    var mr = dbScanner.GeneratedMeshes[assetKey];
                                    lock (mr)
                                    {
                                        mr.Usages.Add(meshUsage);
                                    }
                                }
                                else
                                {
                                    bool IsSkel = className == "SkeletalMesh";
                                    int bones = 0;
                                    if (IsSkel)
                                    {
                                        var bin = ObjectBinary.From<SkeletalMesh>(entry);
                                        bones = bin?.RefSkeleton.Length ?? 0;
                                    }

                                    var NewMeshRec = new MeshRecord(objectNameInstanced, IsSkel, IsMod, bones);
                                    NewMeshRec.Usages.Add(meshUsage);
                                    if (!dbScanner.GeneratedMeshes.TryAdd(assetKey, NewMeshRec))
                                    {
                                        var mr = dbScanner.GeneratedMeshes[assetKey];
                                        lock (mr)
                                        {
                                            mr.Usages.Add(meshUsage);
                                        }
                                    }
                                }
                            }
                            else if (className == "ParticleSystem" || className == "RvrClientEffect" || className == "BioVFXTemplate")
                            {
                                var particleSysUsage = new ParticleSysUsage(FileKey, uindex, IsDLC, IsMod);
                                if (dbScanner.GeneratedPS.ContainsKey(assetKey))
                                {
                                    var ePS = dbScanner.GeneratedPS[assetKey];
                                    lock (ePS)
                                    {
                                        ePS.Usages.Add(particleSysUsage);
                                    }
                                }
                                else
                                {
                                    string parent = null;
                                    if (GameBeingDumped == MEGame.ME1 && File.EndsWith(".upk"))
                                    {
                                        parent = ShortFileName;
                                    }
                                    else
                                    {
                                        parent = GetTopParentPackage(entry);
                                    }

                                    var vfxtype = ParticleSysRecord.VFXClass.BioVFXTemplate;
                                    int EmCnt = 0;
                                    if (className == "ParticleSystem")
                                    {
                                        var EmtProp = props.GetProp<ArrayProperty<ObjectProperty>>("Emitters");
                                        EmCnt = EmtProp?.Count ?? 0;
                                        vfxtype = ParticleSysRecord.VFXClass.ParticleSystem;
                                    }
                                    else if (className == "RvrClientEffect")
                                    {
                                        var RvrProp = props.GetProp<ArrayProperty<ObjectProperty>>("m_lstModules");
                                        EmCnt = RvrProp?.Count ?? 0;
                                        vfxtype = ParticleSysRecord.VFXClass.RvrClientEffect;
                                    }

                                    var NewPS = new ParticleSysRecord(objectNameInstanced, parent, IsDLC, IsMod, EmCnt, vfxtype);
                                    NewPS.Usages.Add(particleSysUsage);
                                    if (!dbScanner.GeneratedPS.TryAdd(assetKey, NewPS))
                                    {
                                        var ePS = dbScanner.GeneratedPS[assetKey];
                                        lock (ePS)
                                        {
                                            ePS.Usages.Add(particleSysUsage);
                                        }
                                    }
                                }
                            }
                            else if (className == "Texture2D" || className == "TextureCube" || className == "TextureMovie")
                            {
                                var textureUsage = new TextureUsage(FileKey, uindex, IsDLC, IsMod);
                                if (dbScanner.GeneratedText.ContainsKey(assetKey))
                                {
                                    var t = dbScanner.GeneratedText[assetKey];
                                    lock (t)
                                    {
                                        t.Usages.Add(textureUsage);
                                    }
                                }
                                else
                                {
                                    string parent;
                                    if (GameBeingDumped == MEGame.ME1 && File.EndsWith(".upk"))
                                    {
                                        parent = ShortFileName;
                                    }
                                    else
                                    {
                                        parent = GetTopParentPackage(entry);
                                    }

                                    string pformat = "TextureCube";
                                    int psizeX = 0;
                                    int psizeY = 0;
                                    string cRC = "n/a";
                                    string texgrp = "n/a";
                                    if (className != "TextureCube")
                                    {
                                        pformat = "TextureMovie";
                                        if (className != "TextureMovie")
                                        {
                                            var formp = props.GetProp<EnumProperty>("Format");
                                            pformat = formp?.Value.Name ?? "n/a";
                                            pformat = pformat.Replace("PF_", string.Empty);
                                            var tgrp = props.GetProp<EnumProperty>("LODGroup");
                                            texgrp = tgrp?.Value.Instanced ?? "n/a";
                                            texgrp = texgrp.Replace("TEXTUREGROUP_", string.Empty);
                                            texgrp = texgrp.Replace("_", string.Empty);
                                            if (ScanCRC)
                                            {
                                                cRC = Texture2D.GetTextureCRC(entry).ToString("X8");
                                            }
                                        }

                                        var propX = props.GetProp<IntProperty>("SizeX");
                                        psizeX = propX?.Value ?? 0;
                                        var propY = props.GetProp<IntProperty>("SizeY");
                                        psizeY = propY?.Value ?? 0;
                                    }

                                    if (entry.Parent?.ClassName == "TextureCube")
                                    {
                                        objectNameInstanced = $"{entry.Parent.ObjectName}_{objectNameInstanced}";
                                    }

                                    var NewTex = new TextureRecord(objectNameInstanced, parent, IsDLC, IsMod, pformat, texgrp, psizeX, psizeY, cRC);
                                    NewTex.Usages.Add(textureUsage);
                                    if (dbScanner.GeneratedText.TryAdd(assetKey, NewTex))
                                    {
                                        var t = dbScanner.GeneratedText[assetKey];
                                        lock (t)
                                        {
                                            t.Usages.Add(textureUsage);
                                        }
                                    }
                                }
                            }
                            else if (className == "GFxMovieInfo" || className == "BioSWF")
                            {
                                if (dbScanner.GeneratedGUI.ContainsKey(assetKey))
                                {
                                    var eGUI = dbScanner.GeneratedGUI[assetKey];
                                    lock (eGUI)
                                    {
                                        eGUI.Usages.Add(new GUIUsage(FileKey, uindex, IsMod));
                                    }
                                }
                                else
                                {
                                    string dataPropName = className == "GFxMovieInfo" ? "RawData" : "Data";
                                    var rawData = props.GetProp<ImmutableByteArrayProperty>(dataPropName);
                                    int datasize = rawData?.Count ?? 0;
                                    var NewGUI = new GUIElement(objectNameInstanced, datasize, IsMod);
                                    NewGUI.Usages.Add(new GUIUsage(FileKey, uindex, IsMod));
                                    if (dbScanner.GeneratedGUI.TryAdd(assetKey, NewGUI))
                                    {
                                        var eGUI = dbScanner.GeneratedGUI[assetKey];
                                        lock (eGUI)
                                        {
                                            eGUI.Usages.Add(new GUIUsage(FileKey, uindex, IsMod));
                                        }
                                    }
                                }
                            }
                            else if (ScanLines && className == "BioConversation")
                            {
                                if (!dbScanner.GeneratedConvo.ContainsKey(objectNameInstanced))
                                {
                                    bool IsAmbient = true;
                                    var speakers = new List<string> { "Shepard", "Owner" };
                                    if (!entry.Game.IsGame3())
                                    {
                                        var s_speakers = props.GetProp<ArrayProperty<StructProperty>>("m_SpeakerList");
                                        if (s_speakers != null)
                                        {
                                            speakers.AddRange(s_speakers.Select(t => t.GetProp<NameProperty>("sSpeakerTag").ToString()));
                                        }
                                    }
                                    else
                                    {
                                        var a_speakers = props.GetProp<ArrayProperty<NameProperty>>("m_aSpeakerList");
                                        if (a_speakers != null)
                                        {
                                            foreach (NameProperty n in a_speakers)
                                            {
                                                speakers.Add(n.ToString());
                                            }
                                        }
                                    }

                                    var entryprop = props.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
                                    foreach (StructProperty Node in entryprop)
                                    {
                                        int speakerindex = Node.GetProp<IntProperty>("nSpeakerIndex");
                                        speakerindex = speakerindex + 2;
                                        if (speakerindex < 0 || speakerindex >= speakers.Count)
                                            continue;
                                        int linestrref = 0;
                                        var linestrrefprop = Node.GetProp<StringRefProperty>("srText");
                                        if (linestrrefprop != null)
                                        {
                                            linestrref = linestrrefprop.Value;
                                        }

                                        var ambientLine = Node.GetProp<BoolProperty>("IsAmbient");
                                        if (IsAmbient)
                                            IsAmbient = ambientLine;

                                        var newLine = new ConvoLine(linestrref, speakers[speakerindex], objectNameInstanced);
                                        if (GameBeingDumped == MEGame.ME1)
                                        {
                                            newLine.Line = ME1TalkFiles.findDataById(linestrref, pcc);
                                            if (newLine.Line == "No Data" || newLine.Line == "\"\"" ||
                                                newLine.Line == "\" \"" || newLine.Line == " ")
                                                continue;
                                        }
                                        else if (GameBeingDumped == MEGame.LE1)
                                        {
                                            newLine.Line = LE1TalkFiles.findDataById(linestrref, pcc);
                                            if (newLine.Line == "No Data" || newLine.Line == "\"\"" ||
                                                newLine.Line == "\" \"" || newLine.Line == " ")
                                                continue;
                                        }

                                        dbScanner.GeneratedLines.TryAdd(linestrref.ToString(), newLine);
                                    }

                                    var replyprop = props.GetProp<ArrayProperty<StructProperty>>("m_ReplyList");
                                    if (replyprop != null)
                                    {
                                        foreach (StructProperty Node in replyprop)
                                        {
                                            int linestrref = 0;
                                            var linestrrefprop = Node.GetProp<StringRefProperty>("srText");
                                            if (linestrrefprop != null)
                                            {
                                                linestrref = linestrrefprop.Value;
                                            }

                                            var ambientLine = Node.GetProp<BoolProperty>("IsAmbient");
                                            if (IsAmbient)
                                                IsAmbient = ambientLine;

                                            ConvoLine newLine = new(linestrref, "Shepard", objectNameInstanced);
                                            if (GameBeingDumped == MEGame.ME1)
                                            {
                                                newLine.Line = ME1TalkFiles.findDataById(linestrref, pcc);
                                                if (newLine.Line == "No Data" || newLine.Line == "\"\"" ||
                                                    newLine.Line == "\" \"" || newLine.Line == " ")
                                                    continue;
                                            }
                                            else if (GameBeingDumped == MEGame.LE1)
                                            {
                                                newLine.Line = LE1TalkFiles.findDataById(linestrref, pcc);
                                                if (newLine.Line == "No Data" || newLine.Line == "\"\"" ||
                                                    newLine.Line == "\" \"" || newLine.Line == " ")
                                                    continue;
                                            }

                                            dbScanner.GeneratedLines.TryAdd(linestrref.ToString(), newLine);
                                        }
                                    }

                                    var NewConv = new Conversation(objectNameInstanced, IsAmbient, new(FileKey, uindex));
                                    dbScanner.GeneratedConvo.TryAdd(assetKey, NewConv);
                                }
                            }
                            else if (ScanPlotUsages)
                            {
                                puScanner.ScanExport(export, FileKey, IsMod);
                            }
                        }
                        else if (export is not null)
                        {
                            var newClassRecord = new ClassRecord(export.ObjectName, ShortFileName, uindex, export.SuperClassName) { IsModOnly = IsMod };
                            var classUsage = new ClassUsage(FileKey, uindex, false, IsMod);

                            lock (dbScanner.ClassLocks.GetOrAdd(objectNameInstanced, new object()))
                            {
                                if (dbScanner.GeneratedClasses.TryGetValue(objectNameInstanced, out ClassRecord oldVal))
                                {
                                    if (oldVal.Definition_package is null) //fake classrecord, created when a usage was found
                                    {
                                        newClassRecord.Usages.AddRange(oldVal.Usages);
                                        newClassRecord.Usages.Add(classUsage);
                                        newClassRecord.PropertyRecords.AddRange(oldVal.PropertyRecords);
                                        newClassRecord.IsModOnly = IsMod & oldVal.IsModOnly;
                                        dbScanner.GeneratedClasses[objectNameInstanced] = newClassRecord;
                                    }
                                    else
                                    {
                                        oldVal.Usages.Add(classUsage);
                                        oldVal.IsModOnly &= IsMod;
                                    }
                                }
                                else
                                {
                                    newClassRecord.Usages.Add(classUsage);
                                    dbScanner.GeneratedClasses[objectNameInstanced] = newClassRecord;
                                }
                            }

                            if (export.ObjectNameString == "BioAutoConditionals")
                            {
                                // Get absolutely fucked
                            }
                        }
                    }
                    catch (Exception e) when (!App.IsDebug)
                    {
                        MessageBox.Show(
                            $"Exception Bug detected in single file: {entry.FileRef.FilePath} Export:{entry.UIndex}");
                    }
                }
            }
            catch (Exception e) when (!App.IsDebug)
            {
                throw new Exception($"Error dumping package file {File}. See the inner exception for details.", e);
            }
        }


        private static string GetTopParentPackage(IEntry entry)
        {
            while (true)
            {
                if (entry.HasParent)
                {
                    entry = entry.Parent;
                }
                else
                {
                    return entry.ObjectName;
                }
            }
        }
    }
}