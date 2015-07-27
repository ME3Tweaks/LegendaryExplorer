using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX.Direct3D;
using KFreonLib.MEDirectories;

namespace ME3Explorer.Unreal.Classes
{
    public class MaterialInstanceConstant
    {
        public PCCObject pcc;
        public byte[] memory;
        public int memsize;
        public int index;
        public List<PropertyReader.Property> props;
        public List<TextureParam> Textures;

        public struct TextureParam
        {
            public int TexIndex;
            public string Desc;
            public Texture Texture;
        }

        public MaterialInstanceConstant(PCCObject Pcc,int Idx)
        {
            BitConverter.IsLittleEndian = true;
            pcc = Pcc;
            index = Idx;
            memory = pcc.Exports[index].Data;
            memsize = memory.Length;
            ReadProperties();         
        }

        public void ReadProperties()
        {
            props = PropertyReader.getPropList(pcc, memory);
            Textures = new List<TextureParam>();
            for (int i = 0; i < props.Count(); i++)
            {
                string name = pcc.getNameEntry(props[i].Name);
                switch (name)
                {
                    case "TextureParameterValues":
                        ReadTextureParams(props[i].raw);
                        break;
                }
            }
        }

        public void ReadTextureParams(byte[] raw)
        {
            int count = BitConverter.ToInt32(raw, 24);            
            int pos = 28;
            for (int i = 0; i < count; i++)
            {
                List<PropertyReader.Property> tp = PropertyReader.ReadProp(pcc, raw, pos);
                string name = pcc.getNameEntry(tp[1].Value.IntValue);
                int Idx = tp[2].Value.IntValue;
                TextureParam t = new TextureParam();
                t.Desc = name;
                t.TexIndex = Idx;
                if (name.ToLower().Contains("diffuse") && Idx >0)
                {
                    Texture2D tex = new Texture2D(pcc, Idx - 1);
                    string loc = Path.GetDirectoryName(Application.ExecutablePath);
                    Texture2D.ImageInfo inf = new Texture2D.ImageInfo();
                    for (int j = 0; j < tex.imgList.Count(); j++)
                        if (tex.imgList[j].storageType != Texture2D.storage.empty)
                        {
                            inf = tex.imgList[j];
                            break;
                        }
                    if (File.Exists(loc + "\\exec\\TempTex.dds"))
                        File.Delete(loc + "\\exec\\TempTex.dds");
                    tex.extractImage(inf, ME3Directory.cookedPath, loc + "\\exec\\TempTex.dds");
                    if (File.Exists(loc + "\\exec\\TempTex.dds"))
                        try
                        {
                            t.Texture = TextureLoader.FromFile(Meshplorer.Preview3D.device, loc + "\\exec\\TempTex.dds");
                        }
                        catch (Direct3DXException e)
                        {
                        }
                    else
                        t.Texture = null;
                }
                else
                    t.Texture = null;
                Textures.Add(t);
                pos = tp[tp.Count -1].offend;
            }
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode(pcc.Exports[index].ObjectName);
            for (int i = 0; i < Textures.Count(); i++)
            {
                string s = Textures[i].Desc + " :# " + (Textures[i].TexIndex - 1).ToString();
                s += " " + pcc.getObjectName(Textures[i].TexIndex);
                res.Nodes.Add(s);
            }
            return res;
        }
    }
}
