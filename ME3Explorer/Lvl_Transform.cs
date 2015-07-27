using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using ME3Explorer.UnrealHelper;

namespace ME3Explorer
{
    public partial class Lvl_Transform : Form
    {
        public Leveleditor refO;
        public int refStat;
        public Vector3 loc, rot;
        public Vector3 dloc, drot;
        public Matrix original;
        public Matrix current;
        public Lvl_Transform()
        {
            InitializeComponent();
        }

        private void Lvl_Transform_FormClosing(object sender, FormClosingEventArgs e)
        {
            refO.treeView1.Enabled = true;
            refO.treeView1.Refresh();
        }

        private void Lvl_Transform_Load(object sender, EventArgs e)
        {
            loc = new Vector3(0, 0, 0);
            rot = new Vector3(0, 0, 0);
            dloc = new Vector3(0, 0, 0);
            drot = new Vector3(0, 0, 0);
            original = refO.Level.UStat[refStat].DirectX.m;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            dloc.Y += STF(textBox1.Text);
            ApplyChange();
        }

        private float STF(string s)
        {
            if (s == "")
                return 0;
            return Convert.ToSingle(s);
        }

        public void ApplyChange()
        {
            if (dloc != new Vector3(0, 0, 0))
            {
                refO.Level.UStat[refStat].DirectX.m.M41 += dloc.X;
                refO.Level.UStat[refStat].DirectX.m.M42 += dloc.Y;
                refO.Level.UStat[refStat].DirectX.m.M43 += dloc.Z;
                refO.Level.UStat[refStat].DirectX.tfpos += dloc;
            }
            if (drot != new Vector3(0, 0, 0))
            {
                Matrix m = refO.Level.UStat[refStat].DirectX.m;
                Vector3 v = new Vector3(m.M41, m.M42, m.M43);
                float f =(3.1415f * 2) / 65536f;
                m.M41 = 0;
                m.M42 = 0;
                m.M43 = 0;
                m *= Matrix.RotationYawPitchRoll(-drot.X * f, -drot.Y * f, drot.Z * f);
                m.M41 = v.X;
                m.M42 = v.Y;
                m.M43 = v.Z;
                refO.Level.UStat[refStat].DirectX.m = m;
            }
            current = refO.Level.UStat[refStat].DirectX.m;
            refO.Render();
            loc += dloc;
            rot += drot;
            dloc = new Vector3(0, 0, 0);
            drot = new Vector3(0, 0, 0);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            dloc.Y -= STF(textBox1.Text);
            ApplyChange();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            dloc.X -= STF(textBox1.Text);
            ApplyChange();
        }

        private void button4_Click(object sender, EventArgs e)
        {
            dloc.X += STF(textBox1.Text);
            ApplyChange();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            dloc.Z += STF(textBox1.Text);
            ApplyChange();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            dloc.Z -= STF(textBox1.Text);
            ApplyChange();
        }

        private void button12_Click(object sender, EventArgs e)
        {
            drot.Y += STF(textBox1.Text);
            ApplyChange();
        }

        private void button11_Click(object sender, EventArgs e)
        {
            drot.Y -= STF(textBox1.Text);
            ApplyChange();

        }

        private void button9_Click(object sender, EventArgs e)
        {
            drot.X += STF(textBox1.Text);
            ApplyChange();

        }

        private void button10_Click(object sender, EventArgs e)
        {
            drot.X -= STF(textBox1.Text);
            ApplyChange();

        }

        private void button8_Click(object sender, EventArgs e)
        {
            drot.Z += STF(textBox1.Text);
            ApplyChange();

        }

        private void button7_Click(object sender, EventArgs e)
        {
            drot.Z -= STF(textBox1.Text);
            ApplyChange();
        }

        private void button14_Click(object sender, EventArgs e)
        {
            refO.Level.UStat[refStat].DirectX.m = original;
            refO.Level.UStat[refStat].DirectX.tfpos -= loc;
        }

        private void button13_Click(object sender, EventArgs e)
        {
            int source = refO.Level.UStat[refStat].LP.source;
            int entry = refO.Level.UStat[refStat].LP.sourceentry;
            int parent = refO.Level.UStat[refStat].index2;
            int parentidx = refO.Level.UStat[refStat].index3;
            UPropertyReader UPR = refO.Level.UPR;
            PCCFile pcc = refO.Level.pcc;
            UMath math = new UMath();
            if (source == 0)//Static Actor
            {
                UStaticMeshActor u = new UStaticMeshActor(pcc.EntryToBuff(entry), pcc.names);
                u.UPR = UPR;
                u.Deserialize();
                int ent = UPR.FindProperty("location", u.props);
                if(ent!=-1)
                {
                    ent++;
                    int off1 = (int)pcc.Export[entry].DataOffset;
                    int off2 = u.props[ent].offset + u.props[ent].raw.Length - 12;
                    Vector3 v = UPR.PropToVector3(u.props[ent].raw);
                    v += loc;
                    byte[] buff = BitConverter.GetBytes(v.X);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i] = buff[i];
                    buff = BitConverter.GetBytes(v.Y);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i + 4] = buff[i];
                    buff = BitConverter.GetBytes(v.Z);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i + 8] = buff[i];
                }
                ent = UPR.FindProperty("Rotator", u.props);
                if (ent != -1)
                {
                    int off1 = (int)pcc.Export[entry].DataOffset;
                    int off2 = u.props[ent].offset + u.props[ent].raw.Length - 12;
                    UMath.Rotator r = math.PropToRotator(u.props[ent].raw);
                    UMath.Rotator r2 = math.IntVectorToRotator(rot);
                    r = r + r2;
                    byte[] buff = BitConverter.GetBytes(r.Pitch);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i] = buff[i];
                    buff = BitConverter.GetBytes(r.Roll);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i + 4] = buff[i];
                    buff = BitConverter.GetBytes(r.Yaw);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i + 8] = buff[i];
                }
            }
            if (source == 1)//Collection
            {
                UStaticMeshComponent uc = refO.Level.UStatComp[parentidx];
                UStaticMeshCollectionActor u = refO.Level.UStatColl[uc.index3];
                int ent = UPR.FindProperty("Scale3D", uc.props);
                if (ent != -1)
                {
                    Matrix m = current;
                    Vector3 v =  new Vector3(m.M41, m.M42, m.M43);
                    Vector3 v2 = UPR.PropToVector3(uc.props[ent+1].raw);
                    v2.X = 1 / v2.X;
                    v2.Y = 1 / v2.Y;
                    v2.Z = 1 / v2.Z;
                    m.M41 = 0;
                    m.M42 = 0;
                    m.M43 = 0;
                    m *= Matrix.Scaling(v2);
                    m.M41 = v.X;
                    m.M42 = v.Y;
                    m.M43 = v.Z;
                    current = m;
                }
                int ent2 = uc.index2;
                    if (ent2 != -1)
                    {
                        int off = u.props[u.props.Count - 1].offset + u.props[u.props.Count - 1].raw.Length + (int)pcc.Export[entry].DataOffset + ent2 * 16 * 4;
                        byte[] buff = BitConverter.GetBytes(current.M11);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M12);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M13);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M14);
                        WriteMemory(off, buff); off += 4;

                        buff = BitConverter.GetBytes(current.M21);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M22);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M23);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M24);
                        WriteMemory(off, buff); off += 4;

                        buff = BitConverter.GetBytes(current.M31);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M32);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M33);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M34);
                        WriteMemory(off, buff); off += 4;

                        buff = BitConverter.GetBytes(current.M41);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M42);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M43);
                        WriteMemory(off, buff); off += 4;
                        buff = BitConverter.GetBytes(current.M44);
                        WriteMemory(off, buff); off += 4;
                    }
            }
            if (source == 2)//Interp Actor
            {
                UInterpActor u = new UInterpActor(pcc.EntryToBuff(entry), pcc.names);
                u.UPR = UPR;
                u.Deserialize();
                int ent = UPR.FindProperty("location", u.props);
                if (ent != -1)
                {
                    ent++;
                    int off1 = (int)pcc.Export[entry].DataOffset;
                    int off2 = u.props[ent].offset + u.props[ent].raw.Length - 12;
                    Vector3 v = UPR.PropToVector3(u.props[ent].raw);
                    v += loc;
                    byte[] buff = BitConverter.GetBytes(v.X);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i] = buff[i];
                    buff = BitConverter.GetBytes(v.Y);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i + 4] = buff[i];
                    buff = BitConverter.GetBytes(v.Z);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i + 8] = buff[i];
                }
                ent = UPR.FindProperty("Rotator", u.props);
                if (ent != -1)
                {
                    int off1 = (int)pcc.Export[entry].DataOffset;
                    int off2 = u.props[ent].offset + u.props[ent].raw.Length - 12;
                    UMath.Rotator r = math.PropToRotator(u.props[ent].raw);
                    UMath.Rotator r2 = math.IntVectorToRotator(rot);
                    r = r + r2;
                    byte[] buff = BitConverter.GetBytes(r.Pitch);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i] = buff[i];
                    buff = BitConverter.GetBytes(r.Roll);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i + 4] = buff[i];
                    buff = BitConverter.GetBytes(r.Yaw);
                    for (int i = 0; i < 4; i++) pcc.memory[off1 + off2 + i + 8] = buff[i];
                }
            }
        }
        public void WriteMemory(int off, byte[] buff)
        {
            for (int i = 0; i < buff.Length; i++)
                refO.Level.pcc.memory[off + i] = buff[i];
        }
    }
}
