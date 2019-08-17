using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.Classes
{
    public class MaterialInstanceConstant
    {
        public ExportEntry export;
        public byte[] memory;
        public int memsize;
        public List<TextureParam> Textures = new List<TextureParam>();

        public struct TextureParam
        {
            public int TexIndex;
            public string Desc;
        }

        public MaterialInstanceConstant(ExportEntry export)
        {
            this.export = export;
            memory = export.Data;
            memsize = memory.Length;
            var properties = export.GetProperties();

            if (export.Game == MEGame.ME1) //todo: maybe check to see if textureparametervalues exists first, but in testing me1 didn't seem to have this
            {
                if (export.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedTextures") is ArrayProperty<ObjectProperty> textures)
                {
                    foreach (var obj in textures)
                    {
                        Textures.Add(new TextureParam
                        {
                            TexIndex = obj.Value,
                            Desc = export.FileRef.getEntry(obj.Value).GetFullPath
                        });
                    }
                }
            }
            else
            {
                if (export.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> paramVals)
                {
                    foreach (StructProperty paramVal in paramVals)
                    {
                        Textures.Add(new TextureParam
                        {
                            TexIndex = paramVal.GetProp<ObjectProperty>("ParameterValue").Value,
                            Desc = paramVal.GetProp<NameProperty>("ParameterName").Value
                        });
                    }
                }
            }
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode($"#{export.UIndex} \"{export.ObjectName}\"");
            for (int i = 0; i < Textures.Count; i++)
            {
                string s = $"{Textures[i].Desc} = #{Textures[i].TexIndex - 1}";
                s += $" \"{export.FileRef.getObjectName(Textures[i].TexIndex)}\"";
                res.Nodes.Add(s);
            }
            TreeNode propsnode = new TreeNode("Properties");
            res.Nodes.Add(propsnode);

            foreach (var prop in export.GetProperties())
            {
                propsnode.Nodes.Add(new TreeNode($"{prop.Name} | {prop.PropType}"));
            }

            return res;
        }
    }
}
