using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ME3ExplorerCore.MEDirectories;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3ExplorerCore.Unreal.Classes
{
    public class MaterialInstanceConstant
    {
        public ExportEntry Export;
        public List<IEntry> Textures = new List<IEntry>();

        public StaticParameterSet StaticParameterSet;
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
                StaticParameterSet = (StaticParameterSet)parsedMaterial.SM3MaterialResource.ID;
                foreach (var v in parsedMaterial.SM3MaterialResource.UniformExpressionTextures)
                {
                    IEntry tex = export.FileRef.GetEntry(v.value);
                    if (tex != null)
                    {
                        Textures.Add(tex);
                    }
                }
            }
            else if (export.ClassName == "RvrEffectsMaterialUser")
            {
                var props = export.GetProperties();
                if (export.GetProperty<ObjectProperty>("m_pBaseMaterial") is ObjectProperty baseProp)
                {
                    // This is an instance... maybe?
                    if (baseProp.Value > 0)
                    {
                        // Local export
                        ReadMaterial(export.FileRef.GetUExport(baseProp.Value));
                    }
                    else
                    {
                        ImportEntry ie = export.FileRef.GetImport(baseProp.Value);
                        var externalEntry = ModelPreview.FindExternalAsset(ie, null);
                        if (externalEntry != null)
                        {
                            ReadMaterial(externalEntry);
                        }
                    }
                }
            }
            else if (export.ClassName == "MaterialInstanceConstant")
            {
                if (ObjectBinary.From(export) is MaterialInstance matInstBin)
                {
                    StaticParameterSet = matInstBin.SM3StaticParameterSet;
                }
                //Read Local
                if (export.GetProperty<ArrayProperty<StructProperty>>("TextureParameterValues") is ArrayProperty<StructProperty> textureparams)
                {
                    foreach (var param in textureparams)
                    {
                        var paramValue = param.GetProp<ObjectProperty>("ParameterValue");
                        var texntry = export.FileRef.GetEntry(paramValue.Value);
                        if (texntry?.ClassName == "Texture2D" && !Textures.Contains(texntry))
                        {
                            Textures.Add(texntry);
                        }
                    }
                }

                if (export.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedTextures") is ArrayProperty<ObjectProperty> textures)
                {
                    foreach (var obj in textures)
                    {
                        var texntry = export.FileRef.GetEntry(obj.Value);
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
                        ReadMaterial(export.FileRef.GetUExport(parentObjProp.Value));
                    }
                    else
                    {
                        ImportEntry ie = export.FileRef.GetImport(parentObjProp.Value);
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
        public void GetShaders(string vertexFactory = "FLocalVertexFactory")
        {
            Shaders = new List<Shader>();
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

            if (shaderCache.MaterialShaderMaps.TryGetValue(StaticParameterSet, out ShaderMap))
            {
                IEnumerable<Guid> shaderGuids;
                if (ShaderMap.MeshShaderMaps.FirstOrDefault(msm => msm.VertexFactoryType == vertexFactory) is MeshShaderMap meshShaderMap)
                {
                    shaderGuids = meshShaderMap.Shaders.Values().Select(shaderRef => shaderRef.Id);
                }
                else
                {
                    //Can't find the vertex factory we want, so just grab the first one? I have no idea what I'm doing
                    shaderGuids = ShaderMap.MeshShaderMaps.First().Shaders.Values().Select(shaderRef => shaderRef.Id);
                }

                foreach (Guid id in shaderGuids)
                {
                    if (shaderCache.Shaders.TryGetValue(id, out Shader shader))
                    {
                        Shaders.Add(shader);
                    }
                }

            }
        }

        public TreeNode ToTree()
        {
            TreeNode res = new TreeNode($"#{Export.UIndex} \"{Export.ObjectName.Instanced}\"");
            for (int i = 0; i < Textures.Count; i++)
            {
                string s = $"{Textures[i].FullPath} = #{Textures[i].UIndex}";
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
