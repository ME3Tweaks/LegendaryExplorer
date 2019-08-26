using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Gammtek.Conduit.Extensions.Collections.Generic;
using ME3Explorer.Packages;
using ME3Explorer.Scene3D;
using ME3Explorer.Unreal.BinaryConverters;

namespace ME3Explorer.Unreal.Classes
{
    public class MaterialInstanceConstant
    {
        public ExportEntry Export;
        public List<IEntry> Textures = new List<IEntry>();

        public Guid MaterialShaderMapID;
        public MaterialShaderMap ShaderMap;
        public List<Shader> Shaders;

        //public List<TextureParam> Textures = new List<TextureParam>();

        //public struct TextureParam
        //{
        //    public int TexIndex;
        //    public string Desc;
        //}

        public MaterialInstanceConstant(ExportEntry export)
        {
            this.Export = export;
            ReadMaterial(export);

            //bool me1Parsed = false;
            //if (export.Game == MEGame.ME1 || export.Game == MEGame.ME2) //todo: maybe check to see if textureparametervalues exists first, but in testing me1 didn't seem to have this
            //{
            //    try
            //    {
            //        me1Parsed = true;
            //    }
            //    catch (Exception e)
            //    {

            //    }
            //}


            //if (export.Game == MEGame.ME3 || !me1Parsed)
            //{
            //    if (export.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> paramVals)
            //    {
            //        foreach (StructProperty paramVal in paramVals)
            //        {
            //            Textures.Add(export.FileRef.getEntry(paramVal.GetProp<ObjectProperty>("ParameterValue").Value));
            //        }
            //    }
            //}
        }

        private void ReadMaterial(ExportEntry export)
        {
            if (export.ClassName == "Material")
            {
                var parsedMaterial = ObjectBinary.From<Material>(export);
                MaterialShaderMapID = parsedMaterial.SM3MaterialResource.ID;
                if (export.Game >= MEGame.ME3)
                {
                    foreach (var v in parsedMaterial.SM3MaterialResource.UniformExpressionTextures)
                    {
                        Textures.Add(export.FileRef.getEntry(v.value));
                    }
                }
                else
                {
                    foreach (var v in parsedMaterial.SM3MaterialResource.Uniform2DTextureExpressions)
                    {
                        Textures.Add(export.FileRef.getEntry(v.TextureIndex.value));
                    }
                }
            }
            else if (export.ClassName == "MaterialInstanceConstant")
            {
                
                //Read Local
                if (export.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> textureparams)
                {
                    foreach (var param in textureparams)
                    {
                        var paramValue = param.GetProp<ObjectProperty>("ParameterValue");
                        var texntry = export.FileRef.getEntry(paramValue.Value);
                        if (texntry.ClassName == "Texture2D" && !Textures.Contains(texntry))
                        {
                            Textures.Add(texntry);
                        }
                    }
                }

                if (export.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedTextures") is ArrayProperty<ObjectProperty> textures)
                {
                    foreach (var obj in textures)
                    {
                        var texntry = export.FileRef.getEntry(obj.Value);
                        if (texntry.ClassName == "Texture2D" && !Textures.Contains(texntry))
                        {
                            Textures.Add(texntry);
                        }
                    }
                }

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
                        var externalEntry = ModelPreview.FindExternalAsset(ie, null);
                        if (externalEntry != null)
                        {
                            ReadMaterial(externalEntry);
                        }
                    }
                }
            }
        }

        //very slow for basegame files. find a way to stick shader info in a database
        public void GetShaders()
        {
            ShaderCache shaderCache;
            if (Export.FileRef.Exports.FirstOrDefault(exp => exp.ClassName == "ShaderCache") is ExportEntry shaderCacheEntry)
            {
                shaderCache = ObjectBinary.From<ShaderCache>(shaderCacheEntry);
            }
            else
            {
                //Hardcode ME3 path for now.
                string globalShaderCachPath = Path.Combine(ME3Directory.cookedPath, "RefShaderCache-PC-D3D-SM3.upk");
                if (File.Exists(globalShaderCachPath))
                {
                    using (var shaderUPK = MEPackageHandler.OpenMEPackage(globalShaderCachPath))
                    {
                        shaderCache = ObjectBinary.From<ShaderCache>(shaderUPK.Exports.First(exp => exp.ClassName == "ShaderCache"));
                    }
                }
                else
                {
                    return;
                }
            }

            if (shaderCache.MaterialShaderMaps.TryGetValue(MaterialShaderMapID, out ShaderMap))
            {
                var shaderGuids = new HashSet<Guid>(ShaderMap.Shaders);
                foreach ((NameReference vertexFactory, List<Guid> guids) in ShaderMap.MeshShaderMaps)
                {
                    //No idea what all the vertex factory stuff means, so I'm just going to shove all the shaders into one big list
                    shaderGuids.AddRange(guids);
                }

                foreach (Guid shaderGuid in shaderGuids)
                {
                    if (shaderCache.Shaders.TryGetValue(shaderGuid, out Shader shader))
                    {
                        Shaders.Add(shader);
                    }
                }
            }
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode($"#{Export.UIndex} \"{Export.ObjectName}\"");
            for (int i = 0; i < Textures.Count; i++)
            {
                string s = $"{Textures[i].GetFullPath} = #{Textures[i].UIndex}";
                s += $" \"{Export.FileRef.getObjectName(Textures[i].UIndex)}\"";
                res.Nodes.Add(s);
            }
            TreeNode propsnode = new TreeNode("Properties");
            res.Nodes.Add(propsnode);

            foreach (var prop in Export.GetProperties())
            {
                propsnode.Nodes.Add(new TreeNode($"{prop.Name} | {prop.PropType}"));
            }

            return res;
        }
    }
}
