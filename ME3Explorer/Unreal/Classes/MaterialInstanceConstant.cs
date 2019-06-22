using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.Classes
{
    public class MaterialInstanceConstant
    {
        public IMEPackage pcc;
        public byte[] memory;
        public int memsize;
        public int index;
        public List<TextureParam> Textures;

        public struct TextureParam
        {
            public int TexIndex;
            public string Desc;
        }

        public MaterialInstanceConstant(IMEPackage Pcc,int Idx)
        {
            
            pcc = Pcc;
            index = Idx;
            memory = pcc.Exports[index].Data;
            memsize = memory.Length;
            if (pcc.getExport(index).GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> paramVals)
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

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode($"#{index} \"{pcc.Exports[index].ObjectName}\"");
            for (int i = 0; i < Textures.Count; i++)
            {
                string s = $"{Textures[i].Desc} = #{Textures[i].TexIndex - 1}";
                s += $" \"{pcc.getObjectName(Textures[i].TexIndex)}\"";
                res.Nodes.Add(s);
            }
            TreeNode propsnode = new TreeNode("Properties");
            res.Nodes.Add(propsnode);

            foreach (var prop in pcc.getExport(index).GetProperties())
            {
                propsnode.Nodes.Add(new TreeNode($"{prop.Name} | {prop.PropType}"));
            }

            return res;
        }
    }
}
