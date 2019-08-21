using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.BinaryConverters;

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

            bool me1Parsed = false;
            if (export.Game == MEGame.ME1) //todo: maybe check to see if textureparametervalues exists first, but in testing me1 didn't seem to have this
            {
                try
                {
                    ReadMaterial(export);
                    me1Parsed = true;
                }
                catch (Exception e)
                {

                }
            }


            if (export.Game != MEGame.ME1 || !me1Parsed)
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

        private void ReadMaterial(ExportEntry export)
        {
            if (export.ClassName == "Material")
            {
                var parsedMaterial = ObjectBinary.From<Material>(export);
                foreach (var v in parsedMaterial.SM3MaterialResource.Uniform2DTextureExpressions)
                {
                    if (v is MaterialUniformExpressionTextureParameter muetp)
                    {
                        //TODO: CHANGE BACK ONCE SIRCXYRTYX FIXES PARSING
                        int expIndex = muetp.ParameterName.Number;
                        Textures.Add(new TextureParam
                        {
                            TexIndex = expIndex,
                            Desc = export.FileRef.getEntry(expIndex).GetFullPath
                        });
                    }
                    else
                    {
                        Textures.Add(new TextureParam
                        {
                            TexIndex = v.TextureIndex.value,
                            Desc = export.FileRef.getEntry(v.TextureIndex.value).GetFullPath
                        });
                    }
                    
                }
            }
            else if (export.ClassName == "MaterialInstanceConstant")
            {
                if (export.GetProperty<ObjectProperty>("Parent") is ObjectProperty parentObjProp)
                {
                    // This is an instance... maybe?
                    if (parentObjProp.Value > 0)
                    {
                        // Local export
                        ReadMaterial(export.FileRef.getUExport(parentObjProp.Value));
                    }
                    else
                    {
                        Debug.WriteLine("PARENT IS AN IMPORT MATERIAL... Not supported right now");
                    }

                }
                else if (export.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedTextures") is ArrayProperty<ObjectProperty> textures)
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
