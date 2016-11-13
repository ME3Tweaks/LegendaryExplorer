using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.DirectX.Direct3D;
using KFreonLib.MEDirectories;
using ME3Explorer.Packages;
using AmaroK86.ImageFormat;
using System.Drawing;

namespace ME3Explorer.Unreal.Classes
{
    public class MaterialInstanceConstant
    {
        public ME3Package pcc;
        public byte[] memory;
        public int memsize;
        public int index;
        public List<PropertyReader.Property> props;
        public List<TextureParam> Textures;

        public struct TextureParam
        {
            public int TexIndex;
            public string Desc;
            public Texture2D Texture;
        }

        public MaterialInstanceConstant(ME3Package Pcc,int Idx)
        {
            
            pcc = Pcc;
            index = Idx;
            memory = pcc.Exports[index].Data;
            memsize = memory.Length;
            ReadProperties();         
        }

        public void ReadProperties()
        {
            props = PropertyReader.getPropList(pcc.Exports[index]);
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
                if (name.ToLower().Contains("diff") || name.ToLower().Contains("tex"))
                {
                    if (Idx > 0)
                    {
                        t.Texture = new Texture2D(pcc, Idx - 1);
                    }
                    else if (Idx < 0)
                    {
                        ImportEntry imp = pcc.getImport(-Idx - 1);
                        // Load imported texture from the pcc it resides in.
                        // Right now, all we can do is a brute force search. =(
                        // Often there is more than result. Take the first result for now.
                        try
                        {
                            bool exit = false;
                            foreach (String path in Directory.GetFiles(ME3Directory.cookedPath, "*.pcc"))
                            {
                                using (ME3Package p = MEPackageHandler.OpenME3Package(path))
                                {
                                    foreach (IExportEntry export in p.Exports)
                                    {
                                        if (export.GetFullPath == imp.GetFullPath)
                                        {
                                            Console.WriteLine("Found texture " + export.GetFullPath + " in pcc " + Path.GetFileName(path) + " called " + p.FileName);
                                            t.Texture = new Texture2D(p, export.Index);
                                            if (t.Texture != null)
                                            {
                                                exit = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                if (exit) break;
                            }
                        }
                        catch
                        {

                        }
                    }
                }
                else
                    t.Texture = null;
                Textures.Add(t);
                pos = tp[tp.Count -1].offend;
            }
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode("#" + index + " \"" + pcc.Exports[index].ObjectName + "\"");
            for (int i = 0; i < Textures.Count(); i++)
            {
                string s = Textures[i].Desc + " = #" + (Textures[i].TexIndex - 1);
                s += " \"" + pcc.getObjectName(Textures[i].TexIndex) + "\"";
                res.Nodes.Add(s);
            }
            TreeNode propsnode = new TreeNode("Properties");
            res.Nodes.Add(propsnode);
            props = PropertyReader.getPropList(pcc.Exports[index]);
            for (int i = 0; i < props.Count(); i++) // Loop through props of export
            {
                string name = pcc.getNameEntry(props[i].Name);
                TreeNode propnode = new TreeNode(name + " | " + props[i].TypeVal.ToString());
                propsnode.Nodes.Add(propnode);
            }

            return res;
        }
    }
}
