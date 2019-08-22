using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using ME3Explorer.Packages;
using ME3Explorer.Scene3D;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Explorer.Unreal.Classes
{
    public class MaterialInstanceConstant
    {
        public ExportEntry export;
        public byte[] memory;
        public int memsize;
        public List<IEntry> Textures = new List<IEntry>();
        //public List<TextureParam> Textures = new List<TextureParam>();

        //public struct TextureParam
        //{
        //    public int TexIndex;
        //    public string Desc;
        //}

        public MaterialInstanceConstant(ExportEntry export)
        {
            this.export = export;
            memory = export.Data;
            memsize = memory.Length;
            var properties = export.GetProperties();

            bool me1Parsed = false;
            if (export.Game == MEGame.ME1 || export.Game == MEGame.ME2) //todo: maybe check to see if textureparametervalues exists first, but in testing me1 didn't seem to have this
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


            if (export.Game == MEGame.ME3 || !me1Parsed)
            {
                if (export.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> paramVals)
                {
                    foreach (StructProperty paramVal in paramVals)
                    {
                        Textures.Add(export.FileRef.getEntry(paramVal.GetProp<ObjectProperty>("ParameterValue").Value));
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
                    Textures.Add(export.FileRef.getEntry(v.TextureIndex.value));
                }
            }
            else if (export.ClassName == "MaterialInstanceConstant")
            {
                //Read parent
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
                        ImportEntry ie = export.FileRef.getUImport(parentObjProp.Value);
                        var externalEntry = ModelPreview.FindExternalAsset(ie);
                        if (externalEntry != null)
                        {
                            ReadMaterial(externalEntry);
                        }
                    }

                }

                //Read Local
                if (export.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedTextures") is ArrayProperty<ObjectProperty> textures)
                {
                    foreach (var obj in textures)
                    {
                        Textures.Add(export.FileRef.getEntry(obj.Value));
                    }
                }
            }
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode($"#{export.UIndex} \"{export.ObjectName}\"");
            for (int i = 0; i < Textures.Count; i++)
            {
                string s = $"{Textures[i].GetFullPath} = #{Textures[i].UIndex}";
                s += $" \"{export.FileRef.getObjectName(Textures[i].UIndex)}\"";
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
