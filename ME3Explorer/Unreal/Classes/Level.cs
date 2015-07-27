using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using ME3Explorer.Unreal;
using ME3Explorer;
using lib3ds.Net;
using KFreonLib.Debugging;

namespace ME3Explorer.Unreal.Classes
{
    public class Level
    {
        public PCCObject pcc;
        public byte[] memory;
        public int memlength;
#region Levelcontent
        public List<int> Objects;
        public List<StaticMeshCollectionActor> STM_CA;
        public List<StaticMeshActor> STM_A;
        public List<DecalActor> DA;
        public List<InterpActor> IA;
        public List<BlockingVolume> BV;
        public List<SplineActor> SPA;
        public List<TargetPoint> TP;
        public List<LightVolume> LV;
        public List<MantleMarker> MM;
        public List<PathNode> PN;
        public List<CoverLink> CL;
        public List<CoverSlotMarker> CSM;
        public List<Emitter> EM;
        public List<BioPlaypenVolumeAdditive> BPVA;
        public List<BioTriggerVolume> BTV;
        public List<BioPathPoint> BPP;
        public List<WwiseAmbientSound> WAS;
        public List<WwiseAudioVolume> WAV;
        public List<WwiseEnvironmentVolume> WEV;
#endregion

        public Level()
        {
        }

        public Level(PCCObject Pcc, int index, bool SimpleRead = false)
        {
            memory = Pcc.Exports[index].Data;
            memlength = memory.Length;
            pcc = Pcc;
            Deserialize(SimpleRead);
        }

        public void Deserialize(bool SimpleRead)
        {
            List<PropertyReader.Property> props = PropertyReader.getPropList(pcc, memory);
            int off = props[props.Count() - 1].offend + 4;
            if (SimpleRead)
                ReadObjectsSimple(off);
            else
                ReadObjects(off);
        }

        public void ReadObjects(int off)
        {
            BitConverter.IsLittleEndian = true;
            int pos = off;
            int count = BitConverter.ToInt32(memory, pos);
            pos += 4;
            Objects = new List<int>();
            STM_CA = new List<StaticMeshCollectionActor>();
            STM_A = new List<StaticMeshActor>();
            DA = new List<DecalActor>();
            BV = new List<BlockingVolume>();
            IA = new List<InterpActor>();
            SPA = new List<SplineActor>();
            TP = new List<TargetPoint>();
            LV = new List<LightVolume>();
            MM = new List<MantleMarker>();
            PN = new List<PathNode>();
            CL = new List<CoverLink>();
            CSM = new List<CoverSlotMarker>();
            EM = new List<Emitter>();
            BPVA = new List<BioPlaypenVolumeAdditive>();
            BTV = new List<BioTriggerVolume>();
            BPP = new List<BioPathPoint>();
            WAS = new List<WwiseAmbientSound>();
            WAV = new List<WwiseAudioVolume>();
            WEV = new List<WwiseEnvironmentVolume>();
            for (int i = 0; i < count; i++)
            {
                int idx = BitConverter.ToInt32(memory, pos) - 1;
                if(pcc.isExport(idx))
                {
                    Objects.Add(idx);
                    PCCObject.ExportEntry e = pcc.Exports[idx];
                    switch (e.ClassName)
                    {
                        case "SplineActor":
                            SPA.Add(new SplineActor(pcc, idx));
                            break;
                        case "Emitter":
                            EM.Add(new Emitter(pcc, idx));
                            break;
                        case "MantleMarker":
                            MM.Add(new MantleMarker(pcc, idx));
                            break;
                        case "PathNode":
                            PN.Add(new PathNode(pcc, idx));
                            break;
                        case "CoverLink":
                            CL.Add(new CoverLink(pcc, idx));
                            break;
                        case "CoverSlotMarker":
                            CSM.Add(new CoverSlotMarker(pcc, idx));
                            break;
                        case "TargetPoint":
                            TP.Add(new TargetPoint(pcc, idx));
                            break;
                        case "InterpActor":
                            IA.Add(new InterpActor(pcc, idx));
                            break;
                        case "DecalActor":
                            DA.Add(new DecalActor(pcc, idx));
                            break;
                        case "StaticMeshCollectionActor":
                            STM_CA.Add(new StaticMeshCollectionActor(pcc, idx));
                            break;
                        case "StaticMeshActor":
                            STM_A.Add(new StaticMeshActor(pcc, idx));
                            break;
                        case "BlockingVolume":
                            BV.Add(new BlockingVolume(pcc, idx));
                            break;
                        case "LightVolume":
                            LV.Add(new LightVolume(pcc, idx));
                            break;
                        case "BioPlaypenVolumeAdditive":
                            BPVA.Add(new BioPlaypenVolumeAdditive(pcc, idx));
                            break;
                        case "BioTriggerVolume":
                            BTV.Add(new BioTriggerVolume(pcc, idx));
                            break;
                        case "BioPathPoint":
                            BPP.Add(new BioPathPoint(pcc, idx));
                            break;
                        case "WwiseAmbientSound":
                            WAS.Add(new WwiseAmbientSound(pcc, idx));
                            break;
                        case "WwiseAudioVolume":
                            WAV.Add(new WwiseAudioVolume(pcc, idx));
                            break;
                        case "WwiseEnvironmentVolume":
                            WEV.Add(new WwiseEnvironmentVolume(pcc, idx));
                            break;
                    }
                }
                pos += 4;
            }
        }

        public void ReadObjectsSimple(int off)
        {
            BitConverter.IsLittleEndian = true;
            int pos = off;
            int count = BitConverter.ToInt32(memory, pos);
            pos += 4;
            Objects = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int idx = BitConverter.ToInt32(memory, pos) - 1;
                if (pcc.isExport(idx))
                    Objects.Add(idx);
                pos += 4;
            }
        }

        public void SaveChanges()
        {
            foreach (StaticMeshCollectionActor stmca in STM_CA)
                stmca.SaveChanges();
            foreach (StaticMeshActor stma in STM_A)
                stma.SaveChanges();
            foreach (InterpActor ia in IA)
                ia.SaveChanges();
            foreach (BlockingVolume bv in BV)
                bv.SaveChanges();
            foreach (SplineActor sp in SPA)
                sp.SaveChanges();
            foreach (TargetPoint tp in TP)
                tp.SaveChanges();
            foreach (LightVolume lv in LV)
                lv.SaveChanges();
            foreach (MantleMarker mm in MM)
                mm.SaveChanges();
            foreach (PathNode pn in PN)
                pn.SaveChanges();
            foreach (CoverLink cl in CL)
                cl.SaveChanges();
            foreach (CoverSlotMarker csm in CSM)
                csm.SaveChanges();
            foreach (Emitter em in EM)
                em.SaveChanges();
            foreach (BioPlaypenVolumeAdditive bpva in BPVA)
                bpva.SaveChanges();
            foreach (BioTriggerVolume btv in BTV)
                btv.SaveChanges();
            foreach (BioPathPoint bpp in BPP)
                bpp.SaveChanges();
            foreach (WwiseAmbientSound was in WAS)
                was.SaveChanges();
            foreach (WwiseAudioVolume wav in WAV)
                wav.SaveChanges();
            foreach (WwiseEnvironmentVolume wev in WEV)
                wev.SaveChanges();
            DebugOutput.PrintLn("Saving \"" + Path.GetFileName(pcc.pccFileName) + "\" ..."); 
            pcc.saveToFile();
        }

        public void CreateModJobs()
        {
            foreach (StaticMeshCollectionActor stmca in STM_CA)
                stmca.CreateModJobs();
            foreach (StaticMeshActor stma in STM_A)
                stma.CreateModJobs();
            foreach (InterpActor ia in IA)
                ia.CreateModJobs();
            foreach (BlockingVolume bv in BV)
                bv.CreateModJobs();
            foreach (SplineActor sp in SPA)
                sp.CreateModJobs();
            foreach (TargetPoint tp in TP)
                tp.CreateModJobs();
            foreach (LightVolume lv in LV)
                lv.CreateModJobs();
            foreach (MantleMarker mm in MM)
                mm.CreateModJobs();
            foreach (PathNode pn in PN)
                pn.CreateModJobs();
            foreach (CoverLink cl in CL)
                cl.CreateModJobs();
            foreach (CoverSlotMarker csm in CSM)
                csm.CreateModJobs();
            foreach (Emitter em in EM)
                em.CreateModJobs();
            foreach (BioPlaypenVolumeAdditive bpva in BPVA)
                bpva.CreateModJobs();
            foreach (BioTriggerVolume btv in BTV)
                btv.CreateModJobs();
            foreach (BioPathPoint bpp in BPP)
                bpp.CreateModJobs();
            foreach (WwiseAmbientSound was in WAS)
                was.CreateModJobs();
            foreach (WwiseAudioVolume wav in WAV)
                wav.CreateModJobs();
            foreach (WwiseEnvironmentVolume wev in WEV)
                wev.CreateModJobs();
        }

        public void ProcessTreeClick(int[] path, bool AutoFocus)
        {
            int idx = path[2];//get selected levelobject
            if(pcc.isExport(Objects[idx]))
                switch (pcc.Exports[Objects[idx]].ClassName)
                {
                    case "StaticMeshCollectionActor":
                        foreach (StaticMeshCollectionActor stmca in STM_CA)
                            if (stmca.MyIndex == Objects[idx])
                                stmca.ProcessTreeClick(path, AutoFocus);
                        break;                    
                    case "StaticMeshActor":
                        foreach (StaticMeshActor stma in STM_A)
                            if (stma.MyIndex == Objects[idx])
                                stma.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "DecalActor":
                        foreach (DecalActor da in DA)
                            if (da.MyIndex == Objects[idx])
                                da.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "BlockingVolume":
                        foreach (BlockingVolume bv in BV)
                            if (bv.MyIndex == Objects[idx])
                                bv.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "InterpActor":
                        foreach (InterpActor ia in IA)
                            if (ia.MyIndex == Objects[idx])
                                ia.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "SplineActor":
                        foreach (SplineActor sp in SPA)
                            if (sp.MyIndex == Objects[idx])
                                sp.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "TargetPoint":
                        foreach (TargetPoint tp in TP)
                            if (tp.MyIndex == Objects[idx])
                                tp.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "LightVolume":
                        foreach (LightVolume lv in LV)
                            if (lv.MyIndex == Objects[idx])
                                lv.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "MantleMarker":
                        foreach (MantleMarker mm in MM)
                            if (mm.MyIndex == Objects[idx])
                                mm.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "PathNode":
                        foreach (PathNode pn in PN) 
                            if (pn.MyIndex == Objects[idx])
                                pn.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "CoverLink":
                        foreach (CoverLink cl in CL)
                            if (cl.MyIndex == Objects[idx])
                                cl.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "CoverSlotMarker":
                        foreach (CoverSlotMarker csm in CSM)
                            if (csm.MyIndex == Objects[idx])
                                csm.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "Emitter":
                        foreach (Emitter em in EM)
                            if (em.MyIndex == Objects[idx])
                                em.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "BioPlaypenVolumeAdditive":
                        foreach (BioPlaypenVolumeAdditive bpva in BPVA)
                            if (bpva.MyIndex == Objects[idx])
                                bpva.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "BioTriggerVolume":
                        foreach (BioTriggerVolume btv in BTV)
                            if (btv.MyIndex == Objects[idx])
                                btv.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "BioPathPoint":
                        foreach (BioPathPoint bpp in BPP)
                            if (bpp.MyIndex == Objects[idx])
                                bpp.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "WwiseAmbientSound":
                        foreach (WwiseAmbientSound was in WAS)
                            if (was.MyIndex == Objects[idx])
                                was.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "WwiseAudioVolume":
                        foreach (WwiseAudioVolume wav in WAV)
                            if (wav.MyIndex == Objects[idx])
                                wav.ProcessTreeClick(path, AutoFocus);
                        break;
                    case "WwiseEnvironmentVolume":
                        foreach (WwiseEnvironmentVolume wev in WEV)
                            if (wev.MyIndex == Objects[idx])
                                wev.ProcessTreeClick(path, AutoFocus);
                        break;
                }
        }

        public void Process3DClick(Vector3 org, Vector3 dir)
        {
            int Idx = -1;
            float dist = -1;
            int SIdx = -1;
            int sel = -1;
            for (int i = 0; i < Objects.Count; i++) 
                if(pcc.isExport(Objects[i]))
                    switch (pcc.Exports[Objects[i]].ClassName)
                    {
                        case "StaticMeshCollectionActor":
                            foreach (StaticMeshCollectionActor stmca in STM_CA)
                                if (stmca.MyIndex == Objects[i])
                                {
                                    float d = stmca.Process3DClick(org, dir, out Idx);
                                    if ((d < dist && d > 0) || (dist == -1 && d > 0))
                                    {
                                        sel = i;
                                        dist = d;
                                        SIdx = Idx;
                                    }
                                }
                            break;
                        case "StaticMeshActor":
                            foreach (StaticMeshActor stma in STM_A)
                                if (stma.MyIndex == Objects[i])
                                {
                                    float d = stma.Process3DClick(org, dir);
                                    if ((d < dist && d > 0) || (dist == -1 && d > 0))
                                    {
                                        sel = i;
                                        dist = d;
                                    }
                                }
                            break;
                        case "InterpActor":
                            foreach (InterpActor ia in IA)
                                if (ia.MyIndex == Objects[i])
                                {
                                    float d = ia.Process3DClick(org, dir);
                                    if ((d < dist && d > 0) || (dist == -1 && d > 0))
                                    {
                                        sel = i;
                                        dist = d;
                                    }
                                }
                            break;
                    }
            if (sel == -1)
                return;
            if(pcc.isExport(Objects[sel]))
                switch (pcc.Exports[Objects[sel]].ClassName)
                {
                    case "StaticMeshCollectionActor":
                        foreach (StaticMeshCollectionActor stmca in STM_CA)
                            if (stmca.MyIndex == Objects[sel])
                                stmca.SetSelection(true, SIdx);
                        break;
                    case "StaticMeshActor":
                        foreach (StaticMeshActor stma in STM_A)
                            if (stma.MyIndex == Objects[sel])
                                stma.SetSelection(true);
                        break;
                    case "InterpActor":
                        foreach (InterpActor ia in IA)
                            if (ia.MyIndex == Objects[sel])
                                ia.SetSelection(true);
                        break;

                }
            
        }

        public void ApplyTransform(Matrix m)
        {
            foreach (StaticMeshCollectionActor stmca in STM_CA)
                stmca.ApplyTransform(m);
            foreach (StaticMeshActor stma in STM_A)
                stma.ApplyTransform(m);
            foreach (InterpActor ia in IA)
                ia.ApplyTransform(m);
            foreach (BlockingVolume bv in BV)
                bv.ApplyTransform(m);
            foreach (SplineActor sp in SPA)
                sp.ApplyTransform(m, SPA);
            foreach (TargetPoint tp in TP)
                tp.ApplyTransform(m);
            foreach (LightVolume lv in LV)
                lv.ApplyTransform(m);
            foreach (MantleMarker mm in MM)
                mm.ApplyTransform(m);
            foreach (PathNode pn in PN)
                pn.ApplyTransform(m);
            foreach (CoverLink cl in CL)
                cl.ApplyTransform(m);
            foreach (CoverSlotMarker csm in CSM)
                csm.ApplyTransform(m);
            foreach (Emitter em in EM)
                em.ApplyTransform(m);
            foreach (BioPlaypenVolumeAdditive bpva in BPVA)
                bpva.ApplyTransform(m);
            foreach (BioTriggerVolume btv in BTV)
                btv.ApplyTransform(m);
            foreach (BioPathPoint bpp in BPP)
                bpp.ApplyTransform(m);
            foreach (WwiseAmbientSound was in WAS)
                was.ApplyTransform(m);
            foreach (WwiseAudioVolume wav in WAV)
                wav.ApplyTransform(m);
            foreach (WwiseEnvironmentVolume wev in WEV)
                wev.ApplyTransform(m);
        }

        public void ApplyRotation(Vector3 v)
        {
            foreach (StaticMeshCollectionActor stmca in STM_CA)
                stmca.ApplyRotation(v);
            foreach (StaticMeshActor stma in STM_A)
                stma.ApplyRotation(v);
            foreach (InterpActor ia in IA)
                 ia.ApplyRotation(v);
        }

        public void SetSelection(bool Selected)
        {
            foreach (StaticMeshCollectionActor stmca in STM_CA)
                stmca.SetSelection(Selected);
            foreach (StaticMeshActor stma in STM_A)
                stma.SetSelection(Selected);
            foreach (InterpActor ia in IA)
                ia.SetSelection(Selected);
            foreach (SplineActor sp in SPA)
                sp.SetSelection(Selected);
            foreach (BlockingVolume bv in BV)
                bv.SetSelection(Selected);
            foreach (TargetPoint tp in TP)
                tp.SetSelection(Selected);
            foreach (LightVolume lv in LV)
                lv.SetSelection(Selected);
            foreach (MantleMarker mm in MM)
                mm.SetSelection(Selected);
            foreach (PathNode pn in PN)
                pn.SetSelection(Selected);
            foreach (CoverLink cl in CL)
                cl.SetSelection(Selected);
            foreach (CoverSlotMarker csm in CSM)
                csm.SetSelection(Selected);
            foreach (Emitter em in EM)
                em.SetSelection(Selected);
            foreach (BioPlaypenVolumeAdditive bpva in BPVA)
                bpva.SetSelection(Selected);
            foreach (BioTriggerVolume btv in BTV)
                btv.SetSelection(Selected);
            foreach (BioPathPoint bpp in BPP)
                bpp.SetSelection(Selected);
            foreach (WwiseAmbientSound was in WAS)
                was.SetSelection(Selected);
            foreach (WwiseAudioVolume wav in WAV)
                wav.SetSelection(Selected);
            foreach (WwiseEnvironmentVolume wev in WEV)
                wev.SetSelection(Selected);
        }

        public void Render(Device d)
        {
            try
            {
                foreach (StaticMeshCollectionActor stmca in STM_CA)
                    stmca.Render(d);
                foreach (StaticMeshActor stma in STM_A)
                    stma.Render(d);
                foreach (InterpActor ia in IA)
                    ia.Render(d);
                foreach (BlockingVolume bv in BV)
                    bv.Render(d);
                foreach (SplineActor s in SPA)
                    s.Render(d);
                foreach (TargetPoint tp in TP)
                    tp.Render(d);
                foreach (LightVolume lv in LV)
                    lv.Render(d);
                foreach (MantleMarker mm in MM)
                    mm.Render(d);
                foreach (PathNode pn in PN)
                    pn.Render(d);
                foreach (CoverLink cl in CL)
                    cl.Render(d);
                foreach (CoverSlotMarker csm in CSM)
                    csm.Render(d);
                foreach (Emitter em in EM)
                    em.Render(d);
                foreach (BioPlaypenVolumeAdditive bpva in BPVA)
                    bpva.Render(d);
                foreach (BioTriggerVolume btv in BTV)
                    btv.Render(d);
                foreach (BioPathPoint bpp in BPP)
                    bpp.Render(d);
                foreach (WwiseAmbientSound was in WAS)
                    was.Render(d);
                foreach (WwiseAudioVolume wav in WAV)
                    wav.Render(d);
                foreach (WwiseEnvironmentVolume wev in WEV)
                    wev.Render(d);
            }
            catch (Exception e)
            {
                DebugOutput.PrintLn("Level ERROR: " + e.Message);
            }
        }

        public void Export3DS(Lib3dsFile f)
        {
            foreach (StaticMeshCollectionActor stmca in STM_CA)
                stmca.Export3DS(f);
            foreach (StaticMeshActor stma in STM_A)
                stma.Export3DS(f);
            foreach (InterpActor ia in IA)
                ia.Export3DS(f);
        }

        public TreeNode ToTree(int nr)
        {
            TreeNode t = new TreeNode("#" + nr + " Level");
            DebugOutput.PrintLn("Generating Tree...");
            for (int i = 0; i < Objects.Count(); i++)
            {                
                int index = Objects[i];
                if (index > 0)
                {
                    PCCObject.ExportEntry e = pcc.Exports[index];
                    DebugOutput.PrintLn((i + 1) + " / " + Objects.Count + " : \"" + e.ObjectName + "\" - \"" + e.ClassName + "\"");
                    switch (e.ClassName)
                    {
                        case "WwiseEnvironmentVolume":
                            foreach (WwiseEnvironmentVolume wev in WEV)
                                if (wev.MyIndex == index)
                                    t.Nodes.Add(wev.ToTree());
                            break;
                        case "WwiseAudioVolume":
                            foreach (WwiseAudioVolume wav in WAV)
                                if (wav.MyIndex == index)
                                    t.Nodes.Add(wav.ToTree());
                            break;
                        case "WwiseAmbientSound":
                            foreach(WwiseAmbientSound was in WAS)
                                if (was.MyIndex == index)
                                    t.Nodes.Add(was.ToTree());
                            break;
                        case "BioPathPoint":
                            foreach (BioPathPoint bpp in BPP )
                                if (bpp.MyIndex == index)
                                    t.Nodes.Add(bpp.ToTree());
                            break;
                        case "BioTriggerVolume":
                            foreach (BioTriggerVolume btv in BTV)
                                if (btv.MyIndex == index)
                                    t.Nodes.Add(btv.ToTree());
                            break;
                        case "BioPlaypenVolumeAdditive":
                            foreach(BioPlaypenVolumeAdditive bpva in BPVA)
                                if (bpva.MyIndex == index)
                                    t.Nodes.Add(bpva.ToTree());
                            break;
                        case "BlockingVolume":
                            foreach (BlockingVolume bv in BV)
                                if (bv.MyIndex == index)
                                    t.Nodes.Add(bv.ToTree());
                            break;
                        case "MantleMarker":
                            foreach (MantleMarker mm in MM)
                                if (mm.MyIndex == index)
                                    t.Nodes.Add(mm.ToTree());
                            break;
                        case "PathNode":
                            foreach (PathNode pn in PN)
                                if (pn.MyIndex == index)
                                    t.Nodes.Add(pn.ToTree());
                            break;
                        case "SplineActor":
                            foreach (SplineActor sp in SPA)
                                if (sp.MyIndex == index)
                                    t.Nodes.Add(sp.ToTree());
                            break;
                        case "TargetPoint":
                            foreach (TargetPoint tp in TP)
                                if (tp.MyIndex == index)
                                    t.Nodes.Add(tp.ToTree());
                            break;
                        case "LightVolume":
                            foreach (LightVolume lv in LV)
                                if (lv.MyIndex == index)
                                    t.Nodes.Add(lv.ToTree());
                            break;
                        case "StaticMeshActor":
                            foreach (StaticMeshActor stma in STM_A)
                                if (stma.MyIndex == index)
                                    t.Nodes.Add(stma.ToTree());
                            break;
                        case "DecalActor":
                            foreach (DecalActor da in DA)
                                if (da.MyIndex == index)
                                    t.Nodes.Add(da.ToTree());
                            break;
                        case "InterpActor":
                            foreach (InterpActor ia in IA)
                                if (ia.MyIndex == index)
                                    t.Nodes.Add(ia.ToTree());
                            break;
                        case "StaticMeshCollectionActor":
                            foreach(StaticMeshCollectionActor stmca in STM_CA)
                                if(stmca.MyIndex==index)
                                    t.Nodes.Add(stmca.ToTree());
                            break;
                        case "CoverLink":
                            foreach (CoverLink cl in CL)
                                if (cl.MyIndex == index)
                                    t.Nodes.Add(cl.ToTree());
                            break;
                        case "CoverSlotMarker":
                            foreach (CoverSlotMarker csm in CSM)
                                if (csm.MyIndex == index)
                                    t.Nodes.Add(csm.ToTree());
                            break;
                        case "Emitter":
                            foreach (Emitter em in EM)
                                if (em.MyIndex == index)
                                    t.Nodes.Add(em.ToTree());
                            break;
                        default:
                            string s = "#" + index + " : \"";
                            s += e.ObjectName + "\" CLASS : \"";
                            s += e.ClassName + "\"";
                            TreeNode t1 = new TreeNode(s);
                            t.Nodes.Add(t1);
                            break;
                    }
                }
                else
                {
                    TreeNode t1 = new TreeNode("#" + index + " : NOT IMPLEMENTED");
                    t.Nodes.Add(t1);
                }
            }
            return t;
        }
        
        public TreeNode ToTreeSimple(int nr)
        {
            TreeNode t = new TreeNode("#" + nr + " Level");
            DebugOutput.PrintLn("Generating Tree...");
            for (int i = 0; i < Objects.Count(); i++)
            {
                int index = Objects[i];
                if (index > 0)
                {
                    PCCObject.ExportEntry e = pcc.Exports[index];
                    DebugOutput.PrintLn((i + 1) + " / " + Objects.Count + " : \"" + e.ObjectName + "\" - \"" + e.ClassName + "\"");
                    switch (e.ClassName)
                    {
                        default:
                            string s = "#" + index + " : \"";
                            s += e.ObjectName + "\" CLASS : \"";
                            s += e.ClassName + "\"";
                            TreeNode t1 = new TreeNode(s);
                            t.Nodes.Add(t1);
                            break;
                    }
                }
                else
                {
                    TreeNode t1 = new TreeNode("#" + index + " : NOT IMPLEMENTED");
                    t.Nodes.Add(t1);
                }
            }
            return t;
        }
    }
}
