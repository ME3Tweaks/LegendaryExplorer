using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.Diagnostics;
using System.IO;
using LegendaryExplorer.SharedUI.Interfaces;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public partial class BinaryInterpreterWPF
{

    private List<ITreeItem> StartShaderCacheScanStream(ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            bool isRefShaderCache = Pcc.FileNameNoExtension.StartsWith("RefShaderCache");
            int dataOffset = CurrentLoadedExport.DataOffset;
            var bin = new EndianReader(CurrentLoadedExport.GetReadOnlyDataStream()) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            if (CurrentLoadedExport.Game == MEGame.UDK)
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"Shader Cache Priority: {bin.ReadInt32()}"));
            }

            if (CurrentLoadedExport.Game.IsLEGame())
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatformLE)bin.ReadByte()}")
                { Length = 1 });
            }
            else
            {
                subnodes.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatformOT)bin.ReadByte()}")
                { Length = 1 });
            }

            int mapCount = Pcc.Game is MEGame.ME3 || Pcc.Game.IsLEGame() ? 2 : 1;
            var nameMappings = new[] { "CompressedCacheMap", "ShaderTypeCRCMap" }; // hack...
            while (mapCount > 0)
            {
                mapCount--;
                int vertexMapCount = bin.ReadInt32();
                var mappingNode = new BinInterpNode(bin.Position - 4, $"{nameMappings[mapCount]}, {vertexMapCount} items");
                subnodes.Add(mappingNode);

                for (int i = 0; i < vertexMapCount; i++)
                {
                    //if (i > 1000)
                    //    continue;
                    NameReference shaderName = bin.ReadNameReference(Pcc);
                    int shaderCRC = bin.ReadInt32();
                    mappingNode.Items.Add(new BinInterpNode(bin.Position - 12, $"CRC:{shaderCRC:X8} {shaderName.Instanced}") { Length = 12 });
                }
            }

            if (Pcc.Game == MEGame.ME1)
            {
                ReadVertexFactoryMap();
            }

            int embeddedShaderFileCount = bin.ReadInt32();
            var embeddedShaderCount = new BinInterpNode(bin.Position - 4, $"Embedded Shader File Count: {embeddedShaderFileCount}");
            subnodes.Add(embeddedShaderCount);
            for (int i = 0; i < embeddedShaderFileCount; i++)
            {
                NameReference shaderName = bin.ReadNameReference(Pcc);
                var shaderNode = new BinInterpNode(bin.Position - 8, $"Shader {i} {shaderName.Instanced}");
                embeddedShaderCount.Items.Add(shaderNode);

                shaderNode.Items.Add(new BinInterpNode(bin.Position - 8, $"Shader Type: {shaderName.Instanced}")
                { Length = 8 });
                shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader GUID {bin.ReadGuid()}")
                { Length = 16 });
                if (Pcc.Game == MEGame.UDK)
                {
                    shaderNode.Items.Add(MakeSHANode(bin, "Shader source SHA", out _));
                }

                int shaderEndOffset = bin.ReadInt32();
                shaderNode.Items.Add(new BinInterpNode(bin.Position - 4, $"Shader End Offset: {shaderEndOffset}")
                { Length = 4 });

                if (CurrentLoadedExport.Game == MEGame.UDK)
                {
                    // UDK seems to serialize some sort of history on shaders, probably to speed up recompilation.

                    int udkCount = bin.ReadInt32();
                    var udkCountNode = new BinInterpNode(bin.Position - 4, $"UDK Serializations (versioning data): {udkCount}");
                    shaderNode.Items.Add(udkCountNode);

                    for (int j = 0; j < udkCount; j++)
                    {
                        udkCountNode.Items.Add(MakeUInt16Node(bin, $"Serialization[{j}]"));
                    }
                }

                if (CurrentLoadedExport.Game.IsLEGame())
                {
                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatformLE)bin.ReadByte()}") { Length = 1 });
                }
                else
                {
                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatformOT)bin.ReadByte()}") { Length = 1 });
                }

                shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Frequency: {(EShaderFrequency)bin.ReadByte()}") { Length = 1 });


                int shaderSize = bin.ReadInt32();
                shaderNode.Items.Add(new BinInterpNode(bin.Position - 4, $"Shader File Size: {shaderSize}")
                { Length = 4 });

                shaderNode.Items.Add(new BinInterpNode(bin.Position, "Shader File") { Length = shaderSize });
                bin.Skip(shaderSize);

                shaderNode.Items.Add(MakeInt32Node(bin, "ParameterMap CRC"));

                shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader End GUID: {bin.ReadGuid()}")
                { Length = 16 });

                string shaderType;
                shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader Type: {shaderType = bin.ReadNameReference(Pcc)}") { Length = 8 });

                if (Pcc.Game == MEGame.UDK)
                {
                    shaderNode.Items.Add(MakeSHANode(bin, "Shader SHA", out _));
                }

                shaderNode.Items.Add(MakeInt32Node(bin, "Number of Instructions"));

                //lazy loading the params reduces memory usage for the LE3 refshadercache by 7GB
                if (isRefShaderCache)
                {
                    shaderNode.Items.Add(new BinInterpNodeLazy(bin.Position, "ShaderParameters", pos =>
                    {
                        try
                        {
                            var bin = new EndianReader(CurrentLoadedExport.GetReadOnlyDataStream()) { Endian = CurrentLoadedExport.FileRef.Endian };
                            bin.JumpTo(pos);
                            if (ReadShaderParameters(bin, shaderType, out Exception e) is BinInterpNode paramsNode)
                            {
                                if (e is not null) throw e;
                                return paramsNode.Items;
                            }
                            if (e is not null) throw e;
                            return
                            [
                                new BinInterpNode(pos,
                                        $"Unparsed Shader Parameters ({shaderEndOffset - dataOffset - pos} bytes)")
                                    { Length = (shaderEndOffset - dataOffset) - pos }
                            ];
                        }
                        catch (Exception ex)
                        {
                            return [new BinInterpNode { Header = $"Error reading binary data: {ex}" }];
                        }
                    }));
                }
                else
                {
                    if (ReadShaderParameters(bin, shaderType, out Exception e) is BinInterpNode paramsNode)
                    {
                        shaderNode.Items.Add(paramsNode);
                    }
                    if (e is not null)
                    {
                        throw e;
                    }

                    if (bin.Position != shaderEndOffset - dataOffset)
                    {
                        var unparsedShaderParams =
                            new BinInterpNode(bin.Position,
                                    $"Unparsed Shader Parameters ({shaderEndOffset - dataOffset - bin.Position} bytes)")
                            { Length = (shaderEndOffset - dataOffset) - (int)bin.Position };
                        shaderNode.Items.Add(unparsedShaderParams);
                    }
                }

                bin.JumpTo(shaderEndOffset - dataOffset);
            }

            void ReadVertexFactoryMap()
            {
                int vertexFactoryMapCount = bin.ReadInt32();
                var factoryMapNode = new BinInterpNode(bin.Position - 4, $"Vertex Factory Name Mapping, {vertexFactoryMapCount} items");
                subnodes.Add(factoryMapNode);

                for (int i = 0; i < vertexFactoryMapCount; i++)
                {
                    NameReference shaderName = bin.ReadNameReference(Pcc);
                    int shaderCRC = bin.ReadInt32();
                    factoryMapNode.Items.Add(new BinInterpNode(bin.Position - 12, $"{shaderCRC:X8} {shaderName.Instanced}") { Length = 12 });
                }
            }

            if (Pcc.Game is MEGame.ME2 or MEGame.ME3 or MEGame.LE1 or MEGame.LE2 or MEGame.LE3)
            {
                ReadVertexFactoryMap();
            }

            int materialShaderMapcount = bin.ReadInt32();
            var materialShaderMaps = new BinInterpNode(bin.Position - 4, $"Material Shader Maps, {materialShaderMapcount} items");
            subnodes.Add(materialShaderMaps);
            for (int i = 0; i < materialShaderMapcount; i++)
            {
                var nodes = new List<ITreeItem>();
                materialShaderMaps.Items.Add(new BinInterpNode(bin.Position, $"Material Shader Map {i}") { Items = nodes });
                nodes.Add(ReadFStaticParameterSet(bin));

                if (Pcc.Game >= MEGame.ME3)
                {
                    nodes.Add(new BinInterpNode(bin.Position, $"Unreal Version {bin.ReadInt32()}") { Length = 4 });
                    nodes.Add(new BinInterpNode(bin.Position, $"Licensee Version {bin.ReadInt32()}") { Length = 4 });
                }

                int shaderMapEndOffset = bin.ReadInt32();
                nodes.Add(new BinInterpNode(bin.Position - 4, $"Material Shader Map end offset {shaderMapEndOffset}") { Length = 4 });

                int unkCount = bin.ReadInt32();
                var unkNodes = new List<ITreeItem>();
                nodes.Add(new BinInterpNode(bin.Position - 4, $"Shaders {unkCount}") { Length = 4, Items = unkNodes });
                for (int j = 0; j < unkCount; j++)
                {
                    unkNodes.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc).Instanced}") { Length = 8 });
                    unkNodes.Add(new BinInterpNode(bin.Position, $"GUID: {bin.ReadGuid()}") { Length = 16 });
                    unkNodes.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc).Instanced}") { Length = 8 });
                }

                int meshShaderMapsCount = bin.ReadInt32();
                var meshShaderMaps = new BinInterpNode(bin.Position - 4, $"Mesh Shader Maps, {meshShaderMapsCount} items") { Length = 4 };
                nodes.Add(meshShaderMaps);
                for (int j = 0; j < meshShaderMapsCount; j++)
                {
                    var nodes2 = new List<ITreeItem>();
                    meshShaderMaps.Items.Add(new BinInterpNode(bin.Position, $"Mesh Shader Map {j}") { Items = nodes2 });

                    int shaderCount = bin.ReadInt32();
                    var shaders = new BinInterpNode(bin.Position - 4, $"Shaders, {shaderCount} items") { Length = 4 };
                    nodes2.Add(shaders);
                    for (int k = 0; k < shaderCount; k++)
                    {
                        var nodes3 = new List<ITreeItem>();
                        shaders.Items.Add(new BinInterpNode(bin.Position, $"Shader {k}") { Items = nodes3 });

                        nodes3.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                        nodes3.Add(new BinInterpNode(bin.Position, $"GUID: {bin.ReadGuid()}") { Length = 16 });
                        nodes3.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                    }
                    nodes2.Add(new BinInterpNode(bin.Position, $"Vertex Factory Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                    if (Pcc.Game == MEGame.ME1)
                    {
                        nodes2.Add(MakeUInt32Node(bin, "Unk"));
                    }
                }

                nodes.Add(new BinInterpNode(bin.Position, $"MaterialId: {bin.ReadGuid()}") { Length = 16 });

                nodes.Add(MakeStringNode(bin, "Friendly Name"));

                nodes.Add(ReadFStaticParameterSet(bin));

                if (Pcc.Game >= MEGame.ME3)
                {
                    string[] uniformExpressionArrays =
                    [
                        "UniformPixelVectorExpressions",
                        "UniformPixelScalarExpressions",
                        "Uniform2DTextureExpressions",
                        "UniformCubeTextureExpressions",
                        "UniformVertexVectorExpressions",
                        "UniformVertexScalarExpressions"
                    ];

                    foreach (string uniformExpressionArrayName in uniformExpressionArrays)
                    {
                        int expressionCount = bin.ReadInt32();
                        nodes.Add(new BinInterpNode(bin.Position - 4, $"{uniformExpressionArrayName}, {expressionCount} expressions")
                        {
                            Items = ReadList(expressionCount, x => ReadMaterialUniformExpression(bin))
                        });
                    }

                    if (Pcc.Game == MEGame.UDK)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"UDK Unknown 0x1C bytes: {bin.ReadInt32()} {bin.ReadInt32()} {bin.ReadInt32()} {bin.ReadInt32()} {bin.ReadInt32()} {bin.ReadInt32()} {bin.ReadInt32()}") { Length = 0x1C });
                    }
                    if (Pcc.Game.IsLEGame())
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatformLE)bin.ReadInt32()}") { Length = 4 });
                    }
                    else if (Pcc.Game is not MEGame.ME1)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatformOT)bin.ReadInt32()}") { Length = 4 });
                    }
                }

                bin.JumpTo(shaderMapEndOffset - dataOffset);
            }

            if (CurrentLoadedExport.Game is MEGame.ME3 && CurrentLoadedExport.FileRef.Platform != MEPackage.GamePlatform.Xenon)
            {
                int numShaderCachePayloads = bin.ReadInt32();
                var shaderCachePayloads = new BinInterpNode(bin.Position - 4, $"Shader Cache Payloads, {numShaderCachePayloads} items");
                subnodes.Add(shaderCachePayloads);
                for (int i = 0; i < numShaderCachePayloads; i++)
                {
                    shaderCachePayloads.Items.Add(MakeEntryNode(bin, $"Payload {i}"));
                }
            }
            else if (CurrentLoadedExport.Game == MEGame.ME1 && CurrentLoadedExport.FileRef.Platform != MEPackage.GamePlatform.PS3)
            {
                int numSomething = bin.ReadInt32();
                var somethings = new BinInterpNode(bin.Position - 4, $"Something, {numSomething} items");
                subnodes.Add(somethings);
                for (int i = 0; i < numSomething; i++)
                {
                    var node = new BinInterpNode(bin.Position, $"Something {i}");
                    node.Items.Add(MakeNameNode(bin, "SomethingName?"));
                    node.Items.Add(MakeGuidNode(bin, "SomethingGuid?"));
                    somethings.Items.Add(node);
                }
            }
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private BinInterpNode ReadShaderParameters(EndianReader bin, string shaderType, out Exception exception)
    {
        exception = null;
        if (CurrentLoadedExport.Game is not MEGame.LE3 or MEGame.UDK)
        {
            return null;
        }

        var node = new BinInterpNode(bin.Position, "ShaderParameters") { IsExpanded = true };

        try
        {
            switch (shaderType)
            {
                case "FGFxPixelShaderSDRGFx_PS_CxformMultiply2Texture": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraudMultiplyTexture": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraud": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_TextTexture": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraudMultiplyNoAddAlpha": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraudMultiply": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraudNoAddAlpha": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_Cxform2Texture": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_CxformGouraudTexture": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_TextTextureSRGBMultiply":  // Verified NOT PRESENT IN LE2 ============================
                case "FGFxPixelShaderSDRGFx_PS_TextTextureSRGB": // Verified NOT PRESENT IN LE2 ============================
                case "FGFxPixelShaderSDRGFx_PS_TextTextureColorMultiply": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_TextTextureColor": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_CxformTextureMultiply": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_CxformTexture": // Verified LE2
                case "FGFxPixelShaderSDRGFx_PS_SolidColor": // Verified LE2
                    for (int i = 0; i < 4; i++)
                    {
                        node.Items.Add(FShaderResourceParameter($"TextureParams[{i}]"));
                    }
                    node.Items.Add(FShaderParameter("ConstantColor"));
                    node.Items.Add(FShaderParameter("ColorScale"));
                    node.Items.Add(FShaderParameter("ColorBias"));
                    node.Items.Add(FShaderParameter("InverseGamma"));
                    if (CurrentLoadedExport.Game == MEGame.LE2)
                    {
                        // Constructor at 7ff7c696ddb0
                        node.Items.Add(FShaderParameter("HDRBrightnessScale"));
                    }
                    break;
                case "THeightFogPixelShader<4>": // Verified LE2
                case "THeightFogPixelShader<1>": // Verified LE2
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FShaderParameter("FogDistanceScale"));
                    node.Items.Add(FShaderParameter("FogExtinctionDistance"));
                    node.Items.Add(FShaderParameter("FogInScattering"));
                    node.Items.Add(FShaderParameter("FogStartDistance"));
                    node.Items.Add(FShaderParameter("FogMinStartDistance"));
                    node.Items.Add(FShaderParameter("EncodePower"));
                    node.Items.Add(FShaderParameter("FalloffStrength"));
                    break;
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFHighQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFHighQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFHighQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFMediumQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFMediumQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFMediumQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFLowQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFLowQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFSpotLightPolicyFLowQualityHwPCF":
                    FBranchingPCFModProjectionPixelShader();
                    FSpotLightPolicy_ModShadowPixelParamsType();
                    break;
                case "FGFxVertexShader<GFx_VS_Glyph>":
                case "FGFxVertexShader<GFx_VS_XY16iCF32_T2>":
                case "FGFxVertexShader<GFx_VS_XY16iCF32_NoTexNoAlpha>":
                case "FGFxVertexShader<GFx_VS_Strip>":
                case "FGFxVertexShader<GFx_VS_XY16iCF32_NoTex>":
                case "FGFxVertexShader<GFx_VS_XY16iCF32>":
                case "FGFxVertexShader<GFx_VS_XY16iC32>":
                    node.Items.Add(FShaderParameter("Transform")); // Verified LE2

                    // LE2 has different parameter names but same number
                    if (CurrentLoadedExport.Game == MEGame.LE2)
                    {
                        // Verified LE2 - 0x7ff7c696e15 FGFxVertexShaderTemplate constructor
                        node.Items.Add(FShaderParameter("TextureMatrix"));
                        node.Items.Add(FShaderParameter("TextureMatrix2"));
                    }
                    else if (CurrentLoadedExport.Game == MEGame.LE3)
                    {
                        node.Items.Add(FShaderParameter("TextureMatrixParams[0]"));
                        node.Items.Add(FShaderParameter("TextureMatrixParams[1]"));
                    }

                    break;
                case "FResolveVertexShader": // Verified: LE2 - Uses FShader::Serialize
                case "FReconstructHDRVertexShader": // Verified: LE2 - Uses FShader::Serialize
                case "FLDRExtractVertexShader": // Verified: LE2 - Uses FShader::Serialize
                case "FMotionBlurVertexShader": // Verified: LE2 - Uses FShader::Serialize
                case "FBinkVertexShader": // Verified: LE2 - Uses FShader::Serialize
                case "FOneColorVertexShader": // Verified: LE2 - Uses FShader::Serialize
                case "FGammaCorrectionVertexShader": // Verified: LE2 - Uses FShader::Serialize
                case "FNULLPixelShader": // Verified: LE2 - Uses FShader::Serialize
                case "FHorizonBasedAOVertexShader":
                case "FModShadowVolumeVertexShader":
                case "FOcclusionQueryVertexShader<0>":
                case "FOcclusionQueryVertexShader<NUM_CUBE_VERTICES>":
                case "FModShadowProjectionVertexShader":
                case "FLUTBlenderVertexShader":
                case "FPostProcessAAVertexShader":
                case "FShadowProjectionVertexShader":
                case "FScreenVertexShader":
                case "FFluidVertexShader":
                case "FEdgePreservingFilterVertexShader":
                case "FLightFunctionVertexShader":
                    //These types have no params
                    return null;
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFMediumQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFHighQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFLowQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFHighQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFHighQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFMediumQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFMediumQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFLowQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFDirectionalLightPolicyFLowQualityHwPCF":
                    FBranchingPCFModProjectionPixelShader();
                    //FDirectionalLightPolicy::ModShadowPixelParamsType has no params
                    break;
                case "FResolveSingleSamplePixelShader": // Verified: LE2
                    node.Items.Add(FShaderResourceParameter("UnresolvedSurface"));
                    node.Items.Add(FShaderParameter("SingleSampleIndex"));
                    break;
                case "FResolveDepthPixelShader": // Verified: LE2
                    node.Items.Add(FShaderResourceParameter("UnresolvedSurface"));
                    break;
                case "TModShadowVolumePixelShaderFPointLightPolicy":
                    FModShadowVolumePixelShader_Maybe();
                    FPointLightPolicy_ModShadowPixelParamsType();
                    break;
                case "FGammaCorrectionPixelShader":
                    node.Items.Add(FShaderResourceParameter("SceneTexture"));
                    node.Items.Add(FShaderParameter("InverseGamma"));
                    node.Items.Add(FShaderParameter("ColorScale"));
                    node.Items.Add(FShaderParameter("OverlayColor"));
                    break;
                case "FGFxPixelShaderHDRGFx_PS_CxformMultiply2Texture": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraudMultiplyTexture": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraud": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraudMultiplyNoAddAlpha": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraudNoAddAlpha": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraudMultiply": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_Cxform2Texture": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_CxformGouraudTexture": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_CxformTexture": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_TextTextureSRGBMultiply": // Verified NOT PRESENT IN LE2 --------------------------
                case "FGFxPixelShaderHDRGFx_PS_CxformTextureMultiply": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_TextTextureSRGB": // Verified NOT PRESENT IN LE2 ----------------------------------
                case "FGFxPixelShaderHDRGFx_PS_TextTextureColorMultiply":  // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_TextTextureColor": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_TextTexture": // Verified LE2
                case "FGFxPixelShaderHDRGFx_PS_SolidColor": // Verified LE2
                    for (int i = 0; i < 4; i++)
                    {
                        if (CurrentLoadedExport.Game == MEGame.LE2)
                        {
                            node.Items.Add(FShaderResourceParameterIndexed($"TextureImage", i));
                        }
                        else if (CurrentLoadedExport.Game == MEGame.LE3)
                        {
                            // Todo: Review this parameter name
                            node.Items.Add(FShaderResourceParameter($"TextureParams[{i}]"));
                        }
                    }
                    node.Items.Add(FShaderParameter("ConstantColor"));
                    node.Items.Add(FShaderParameter("ColorScale"));
                    node.Items.Add(FShaderParameter("ColorBias"));
                    node.Items.Add(FShaderParameter("InverseGamma"));
                    node.Items.Add(FShaderParameter("HDRBrightnessScale"));
                    break;
                case "FDownsampleSceneDepthPixelShader":
                    node.Items.Add(FShaderParameter("ProjectionScaleBias"));
                    node.Items.Add(FShaderParameter("SourceTexelOffsets01"));
                    node.Items.Add(FShaderParameter("SourceTexelOffsets23"));
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    break;
                case "FFXAA3BlendPixelShader":
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FShaderParameter("rcpFrame"));
                    node.Items.Add(FShaderParameter("rcpFrameOpt"));
                    break;
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFLowQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFLowQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFLowQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFMediumQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFMediumQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFMediumQualityHwPCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFHighQualityManualPCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFHighQualityFetch4PCF":
                case "TBranchingPCFModProjectionPixelShaderFPointLightPolicyFHighQualityHwPCF":
                    FBranchingPCFModProjectionPixelShader();
                    FPointLightPolicy_ModShadowPixelParamsType();
                    break;
                case "FTexturedCalibrationBoxHDRPixelShader": // Verified LE2 7ff7c6567d70
                    node.Items.Add(FShaderParameter("CalibrationParameters"));
                    node.Items.Add(FShaderResourceParameter("SourceTexture"));
                    break;
                case "FScreenPixelShader":
                case "FHBAOApplyPixelShader":
                case "FCopyVariancePixelShader":
                case "FSimpleElementHitProxyPixelShader":
                    node.Items.Add(FShaderResourceParameter("Texture"));
                    break;
                case "FMotionBlurPixelShader":
                case "FMotionBlurPixelShaderDynamicVelocitiesOnly":
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FMotionBlurShaderParameters("MotionBlurParameters"));
                    break;
                case "FDownsampleDepthVertexShader":
                    node.Items.Add(FShaderParameter("HalfSceneColorTexelSize"));
                    break;
                case "FAmbientOcclusionVertexShader": // Verified LE2
                    node.Items.Add(FShaderParameter("ScreenToView"));
                    break;
                case "FCalibrationBoxHDRPixelShader": // Verified LE2
                    node.Items.Add(FShaderParameter("CalibrationParameters"));
                    break;
                case "TFilterVertexShader<16>": // Verified LE2
                case "TFilterVertexShader<15>": // Verified LE2
                case "TFilterVertexShader<14>": // Verified LE2
                case "TFilterVertexShader<13>": // Verified LE2
                case "TFilterVertexShader<12>": // Verified LE2
                case "TFilterVertexShader<11>": // Verified LE2
                case "TFilterVertexShader<10>": // Verified LE2
                case "TFilterVertexShader<9>": // Verified LE2
                case "TFilterVertexShader<8>": // Verified LE2
                case "TFilterVertexShader<7>": // Verified LE2
                case "TFilterVertexShader<6>": // Verified LE2
                case "TFilterVertexShader<5>": // Verified LE2
                case "TFilterVertexShader<4>": // Verified LE2
                case "TFilterVertexShader<3>": // Verified LE2
                case "TFilterVertexShader<2>": // Verified LE2
                case "TFilterVertexShader<1>": // Verified LE2
                case "TDOFAndBloomGatherVertexShader<MAX_FILTER_SAMPLES>": // Verified LE2
                case "TDOFAndBloomGatherVertexShader<NumFPFilterSamples>": // Verified LE2
                case "TDOFAndBloomGatherVertexShader<1>": // Verified LE2
                    node.Items.Add(FShaderParameter("SampleOffsets"));
                    break;
                case "FShaderComplexityAccumulatePixelShader":
                    node.Items.Add(FShaderParameter("NormalizedComplexity"));
                    break;
                case "FDistortionApplyScreenVertexShader":
                case "FSimpleElementVertexShader": // Verified LE2
                    node.Items.Add(FShaderParameter("Transform"));
                    break;
                case "FDownsampleLightShaftsVertexShader": // Verified LE2
                    node.Items.Add(FShaderParameter("ScreenToWorld"));
                    break;
                case "FRadialBlurVertexShader": // Verified LE2
                    node.Items.Add(FShaderParameter("WorldCenterPos"));
                    break;
                case "FOneColorPixelShader": // Verified LE2
                    node.Items.Add(FShaderParameter("DrawColor"));
                    break;
                case "FDOFAndBloomBlendVertexShader": // Verified LE2
                    node.Items.Add(FShaderParameter("SceneCoordinateScaleBias"));
                    break;
                case "FHistoryUpdateVertexShader": // Verified LE2
                    node.Items.Add(FShaderParameter("ScreenToWorldOffset"));
                    break;
                case "FReconstructHDRPixelShader<FALSE>": // Verified LE2
                case "FReconstructHDRPixelShader<TRUE>": // Verified LE2
                    node.Items.Add(FShaderResourceParameter("SourceTexture"));
                    node.Items.Add(FShaderParameter("HDRParameters"));
                    node.Items.Add(FShaderParameter("CalibrationParameters"));
                    break;
                case "FSimpleElementPixelShader": // Verified LE2
                    node.Items.Add(FShaderResourceParameter("Texture"));
                    node.Items.Add(FShaderParameter("TextureComponentReplicate"));
                    node.Items.Add(FShaderParameter("TextureComponentReplicateAlpha"));
                    break;
                case "TFilterPixelShader<16>":
                case "TFilterPixelShader<15>":
                case "TFilterPixelShader<14>":
                case "TFilterPixelShader<13>":
                case "TFilterPixelShader<12>":
                case "TFilterPixelShader<11>":
                case "TFilterPixelShader<10>":
                case "TFilterPixelShader<9>":
                case "TFilterPixelShader<8>":
                case "TFilterPixelShader<7>":
                case "TFilterPixelShader<6>":
                case "TFilterPixelShader<5>":
                case "TFilterPixelShader<4>":
                case "TFilterPixelShader<3>":
                case "TFilterPixelShader<2>":
                case "TFilterPixelShader<1>":
                    node.Items.Add(FShaderResourceParameter("FilterTexture"));
                    node.Items.Add(FShaderParameter("SampleWeights"));
                    node.Items.Add(FShaderParameter("SampleMaskRect"));
                    break;
                case "FShadowVolumeVertexShader":
                    node.Items.Add(FShaderParameter("LightPosition"));
                    node.Items.Add(FShaderParameter("BaseExtrusion"));
                    node.Items.Add(FShaderParameter("LocalToWorld"));
                    break;
                case "FSFXUberPostProcessBlendPixelShader0011111":
                case "FSFXUberPostProcessBlendPixelShader0101001":
                case "FSFXUberPostProcessBlendPixelShader1111001":
                case "FSFXUberPostProcessBlendPixelShader1010001":
                case "FSFXUberPostProcessBlendPixelShader0110010":
                case "FSFXUberPostProcessBlendPixelShader1100010":
                case "FSFXUberPostProcessBlendPixelShader0011100":
                case "FSFXUberPostProcessBlendPixelShader0101010":
                case "FSFXUberPostProcessBlendPixelShader1111010":
                case "FSFXUberPostProcessBlendPixelShader1010010":
                case "FSFXUberPostProcessBlendPixelShader0010110":
                case "FSFXUberPostProcessBlendPixelShader0101111":
                case "FSFXUberPostProcessBlendPixelShader1111111":
                case "FSFXUberPostProcessBlendPixelShader1010111":
                case "FSFXUberPostProcessBlendPixelShader0100011":
                case "FSFXUberPostProcessBlendPixelShader1110011":
                case "FSFXUberPostProcessBlendPixelShader1011011":
                case "FSFXUberPostProcessBlendPixelShader1001111":
                case "FSFXUberPostProcessBlendPixelShader1000101":
                case "FSFXUberPostProcessBlendPixelShader0101100":
                case "FSFXUberPostProcessBlendPixelShader1111100":
                case "FSFXUberPostProcessBlendPixelShader1010100":
                case "FSFXUberPostProcessBlendPixelShader0011110":
                case "FSFXUberPostProcessBlendPixelShader0101000":
                case "FSFXUberPostProcessBlendPixelShader1111000":
                case "FSFXUberPostProcessBlendPixelShader1010000":
                case "FSFXUberPostProcessBlendPixelShader0110011":
                case "FSFXUberPostProcessBlendPixelShader1100011":
                case "FSFXUberPostProcessBlendPixelShader0011101":
                case "FSFXUberPostProcessBlendPixelShader0101011":
                case "FSFXUberPostProcessBlendPixelShader1111011":
                case "FSFXUberPostProcessBlendPixelShader1010011":
                case "FSFXUberPostProcessBlendPixelShader0010111":
                case "FSFXUberPostProcessBlendPixelShader0101110":
                case "FSFXUberPostProcessBlendPixelShader1111110":
                case "FSFXUberPostProcessBlendPixelShader1010110":
                case "FSFXUberPostProcessBlendPixelShader0100010":
                case "FSFXUberPostProcessBlendPixelShader1110010":
                case "FSFXUberPostProcessBlendPixelShader1011010":
                case "FSFXUberPostProcessBlendPixelShader1001110":
                case "FSFXUberPostProcessBlendPixelShader1000100":
                case "FSFXUberPostProcessBlendPixelShader0101101":
                case "FSFXUberPostProcessBlendPixelShader1111101":
                case "FSFXUberPostProcessBlendPixelShader1010101":
                case "FSFXUberPostProcessBlendPixelShader0100111":
                case "FSFXUberPostProcessBlendPixelShader0111001":
                case "FSFXUberPostProcessBlendPixelShader1110111":
                case "FSFXUberPostProcessBlendPixelShader1101001":
                case "FSFXUberPostProcessBlendPixelShader1011111":
                case "FSFXUberPostProcessBlendPixelShader1001011":
                case "FSFXUberPostProcessBlendPixelShader0100110":
                case "FSFXUberPostProcessBlendPixelShader0111000":
                case "FSFXUberPostProcessBlendPixelShader1110110":
                case "FSFXUberPostProcessBlendPixelShader1101000":
                case "FSFXUberPostProcessBlendPixelShader1011110":
                case "FSFXUberPostProcessBlendPixelShader1001010":
                case "FSFXUberPostProcessBlendPixelShader0100101":
                case "FSFXUberPostProcessBlendPixelShader0110100":
                case "FSFXUberPostProcessBlendPixelShader0111011":
                case "FSFXUberPostProcessBlendPixelShader0111110":
                case "FSFXUberPostProcessBlendPixelShader1110101":
                case "FSFXUberPostProcessBlendPixelShader1101110":
                case "FSFXUberPostProcessBlendPixelShader1101011":
                case "FSFXUberPostProcessBlendPixelShader1100100":
                case "FSFXUberPostProcessBlendPixelShader1011101":
                case "FSFXUberPostProcessBlendPixelShader1001001":
                case "FSFXUberPostProcessBlendPixelShader0100100":
                case "FSFXUberPostProcessBlendPixelShader0110101":
                case "FSFXUberPostProcessBlendPixelShader0111010":
                case "FSFXUberPostProcessBlendPixelShader0111111":
                case "FSFXUberPostProcessBlendPixelShader1110100":
                case "FSFXUberPostProcessBlendPixelShader1101111":
                case "FSFXUberPostProcessBlendPixelShader1101010":
                case "FSFXUberPostProcessBlendPixelShader1100101":
                case "FSFXUberPostProcessBlendPixelShader1011100":
                case "FSFXUberPostProcessBlendPixelShader1001000":
                case "FSFXUberPostProcessBlendPixelShader0011000":
                case "FSFXUberPostProcessBlendPixelShader0100001":
                case "FSFXUberPostProcessBlendPixelShader1110001":
                case "FSFXUberPostProcessBlendPixelShader1011001":
                case "FSFXUberPostProcessBlendPixelShader1001101":
                case "FSFXUberPostProcessBlendPixelShader1000111":
                case "FSFXUberPostProcessBlendPixelShader1000010":
                case "FSFXUberPostProcessBlendPixelShader0011001":
                case "FSFXUberPostProcessBlendPixelShader0100000":
                case "FSFXUberPostProcessBlendPixelShader1110000":
                case "FSFXUberPostProcessBlendPixelShader1011000":
                case "FSFXUberPostProcessBlendPixelShader1001100":
                case "FSFXUberPostProcessBlendPixelShader1000110":
                case "FSFXUberPostProcessBlendPixelShader1000011":
                case "FSFXUberPostProcessBlendPixelShader0011010":
                case "FSFXUberPostProcessBlendPixelShader0110111":
                case "FSFXUberPostProcessBlendPixelShader0111101":
                case "FSFXUberPostProcessBlendPixelShader1101101":
                case "FSFXUberPostProcessBlendPixelShader1100111":
                case "FSFXUberPostProcessBlendPixelShader1000000":
                case "FSFXUberPostProcessBlendPixelShader0011011":
                case "FSFXUberPostProcessBlendPixelShader0110110":
                case "FSFXUberPostProcessBlendPixelShader0111100":
                case "FSFXUberPostProcessBlendPixelShader1101100":
                case "FSFXUberPostProcessBlendPixelShader1100110":
                case "FSFXUberPostProcessBlendPixelShader1000001":
                case "FSFXUberPostProcessBlendPixelShader0110001":
                case "FSFXUberPostProcessBlendPixelShader1100001":
                case "FSFXUberPostProcessBlendPixelShader0110000":
                case "FSFXUberPostProcessBlendPixelShader1100000":
                case "FSFXUberPostProcessBlendPixelShader0000100":
                case "FSFXUberPostProcessBlendPixelShader0010101":
                case "FSFXUberPostProcessBlendPixelShader0000101":
                case "FSFXUberPostProcessBlendPixelShader0010100":
                case "FSFXUberPostProcessBlendPixelShader0010011":
                case "FSFXUberPostProcessBlendPixelShader0010010":
                case "FSFXUberPostProcessBlendPixelShader0010001":
                case "FSFXUberPostProcessBlendPixelShader0010000":
                case "FSFXUberPostProcessBlendPixelShader0001111":
                case "FSFXUberPostProcessBlendPixelShader0001110":
                case "FSFXUberPostProcessBlendPixelShader0001101":
                case "FSFXUberPostProcessBlendPixelShader0001100":
                case "FSFXUberPostProcessBlendPixelShader0001011":
                case "FSFXUberPostProcessBlendPixelShader0001010":
                case "FSFXUberPostProcessBlendPixelShader0001001":
                case "FSFXUberPostProcessBlendPixelShader0001000":
                case "FSFXUberPostProcessBlendPixelShader0000111":
                case "FSFXUberPostProcessBlendPixelShader0000110":
                case "FSFXUberPostProcessBlendPixelShader0000011":
                case "FSFXUberPostProcessBlendPixelShader0000010":
                case "FSFXUberPostProcessBlendPixelShader0000001":
                case "FSFXUberPostProcessBlendPixelShader0000000":
                    FUberPostProcessBlendPixelShader();
                    node.Items.Add(FShaderResourceParameter("NoiseTexture"));
                    node.Items.Add(FShaderParameter("NoiseTextureOffset"));
                    node.Items.Add(FShaderParameter("FilmGrain_Scale"));
                    node.Items.Add(FShaderResourceParameter("smpFilmicLUT"));

                    // Not present in LE2 - 7ff7c63e15b0
                    if (CurrentLoadedExport.Game == MEGame.LE3)
                    {
                        node.Items.Add(FShaderParameter("ScreenUVScaleBias"));
                        node.Items.Add(FShaderParameter("HighPrecisionGamma"));
                    }

                    break;
                case "FUberPostProcessVertexShader":
                    node.Items.Add(FShaderParameter("SceneCoordinate1ScaleBias"));
                    node.Items.Add(FShaderParameter("SceneCoordinate2ScaleBias"));
                    node.Items.Add(FShaderParameter("SceneCoordinate3ScaleBias"));
                    break;
                case "TModShadowVolumePixelShaderFSpotLightPolicy":
                    FModShadowVolumePixelShader_Maybe();
                    FSpotLightPolicy_ModShadowPixelParamsType();
                    break;
                case "FSFXUberHalfResPixelShader0001":
                case "FSFXUberHalfResPixelShader0010":
                case "FSFXUberHalfResPixelShader1000":
                case "FSFXUberHalfResPixelShader1011":
                case "FSFXUberHalfResPixelShader0000":
                case "FSFXUberHalfResPixelShader0011":
                case "FSFXUberHalfResPixelShader1001":
                case "FSFXUberHalfResPixelShader1010":
                case "FUberHalfResPixelShader101":
                case "FUberHalfResPixelShader100":
                case "FUberHalfResPixelShader001":
                case "FUberHalfResPixelShader000":
                    FDOFAndBloomBlendPixelShader();
                    node.Items.Add(FMotionBlurShaderParameters("MotionBlurParameters"));
                    node.Items.Add(FShaderResourceParameter("LowResSceneBufferPoint"));
                    break;
                case "FUberPostProcessBlendPixelShader001":
                case "FUberPostProcessBlendPixelShader010":
                case "FUberPostProcessBlendPixelShader100":
                case "FUberPostProcessBlendPixelShader111":
                case "FUberPostProcessBlendPixelShader000":
                case "FUberPostProcessBlendPixelShader011":
                case "FUberPostProcessBlendPixelShader101":
                case "FUberPostProcessBlendPixelShader110":
                    FUberPostProcessBlendPixelShader();
                    break;
                case "TAOApplyPixelShader<AOApply_ReadFromAOHistory>":
                case "TAOApplyPixelShader<AOApply_Normal>":
                    node.Items.Add(FAmbientOcclusionParams("AOParams"));
                    node.Items.Add(FShaderParameter("OcclusionColor"));
                    node.Items.Add(FShaderParameter("FogColor"));
                    node.Items.Add(FShaderParameter("TargetSize"));
                    node.Items.Add(FShaderParameter("InvEncodePower"));
                    node.Items.Add(FShaderResourceParameter("FogTexture"));
                    break;
                case "TModShadowProjectionPixelShaderFSpotLightPolicyF16SampleManualPCF":
                case "TModShadowProjectionPixelShaderFSpotLightPolicyF16SampleFetch4PCF":
                case "TModShadowProjectionPixelShaderFSpotLightPolicyF16SampleHwPCF":
                case "TModShadowProjectionPixelShaderFSpotLightPolicyF4SampleManualPCF":  // Verified LE2
                case "TModShadowProjectionPixelShaderFSpotLightPolicyF4SampleHwPCF": // Verified LE2
                    TModShadowProjectionPixelShader();
                    FSpotLightPolicy_ModShadowPixelParamsType(); // Verified LE2
                    break;
                case "FFluidApplyPixelShader":
                    node.Items.Add(FShaderResourceParameter("FluidHeightTexture"));
                    node.Items.Add(FShaderResourceParameter("FluidNormalTexture"));
                    break;
                case "TModShadowProjectionPixelShaderFDirectionalLightPolicyF16SampleManualPCF":
                case "TModShadowProjectionPixelShaderFDirectionalLightPolicyF16SampleFetch4PCF":
                case "TModShadowProjectionPixelShaderFDirectionalLightPolicyF16SampleHwPCF":
                case "TModShadowProjectionPixelShaderFDirectionalLightPolicyF4SampleManualPCF":
                case "TModShadowProjectionPixelShaderFDirectionalLightPolicyF4SampleHwPCF":
                    TModShadowProjectionPixelShader();
                    //FDirectionalLightPolicy::ModShadowPixelParamsType has no params
                    break;
                case "FXAAFilterComputeShaderVerticalDebug":
                case "FXAAFilterComputeShaderVertical":
                case "FXAAFilterComputeShaderHorizontalDebug":
                case "FXAAFilterComputeShaderHorizontal":
                    node.Items.Add(FShaderResourceParameter("WorkQueue"));
                    node.Items.Add(FShaderResourceParameter("Color"));
                    node.Items.Add(FShaderResourceParameter("InColor"));
                    node.Items.Add(FShaderResourceParameter("Luma"));
                    node.Items.Add(FShaderResourceParameter("LinearSampler"));
                    node.Items.Add(FShaderParameter("RcpTextureSize"));
                    break;
                case "TShadowProjectionPixelShader<F16SampleManualPCF>":
                case "TShadowProjectionPixelShader<F16SampleFetch4PCF>":
                case "TShadowProjectionPixelShader<F16SampleHwPCF>":
                case "TShadowProjectionPixelShader<F4SampleManualPCF>":
                case "TShadowProjectionPixelShader<F4SampleHwPCF>":
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FShaderParameter("ScreenToShadowMatrix"));
                    node.Items.Add(FShaderResourceParameter("ShadowDepthTexture"));
                    node.Items.Add(FShaderResourceParameter("ShadowDepthTextureComparisonSampler"));
                    node.Items.Add(FShaderParameter("SampleOffsets"));
                    node.Items.Add(FShaderParameter("ShadowBufferSize"));
                    node.Items.Add(FShaderParameter("ShadowFadeFraction"));
                    break;
                case "FBlurLightShaftsPixelShader":
                    node.Items.Add(FLightShaftPixelShaderParameters("LightShaftParameters"));
                    node.Items.Add(FShaderParameter("BlurPassIndex"));
                    break;
                case "FFilterVSMComputeShader":
                    node.Items.Add(FShaderResourceParameter("DepthTexture"));
                    node.Items.Add(FShaderResourceParameter("VarianceTexture"));
                    node.Items.Add(FShaderParameter("SubRect"));
                    break;
                case "FDistortionApplyScreenPixelShader":
                    node.Items.Add(FShaderResourceParameter("AccumulatedDistortionTextureParam"));
                    node.Items.Add(FShaderResourceParameter("SceneColorTextureParam"));
                    node.Items.Add(FShaderParameter("SceneColorRect"));
                    break;
                case "FSRGBMLAABlendPixelShader":
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FShaderResourceParameter("EdgeCountTexture"));
                    node.Items.Add(FShaderParameter("gRTSize"));
                    node.Items.Add(FShaderParameter("gLuminanceEquation"));
                    node.Items.Add(FShaderParameter("gInverseDisplayGamma"));
                    break;
                case "TBasePassVertexShaderFNoLightMapPolicyFNoDensityPolicy":
                    //FNoLightMapPolicy::VertexParametersType has no params
                    TBasePassVertexShader();
                    //FNoDensityPolicy::VertexShaderParametersType has no params
                    break;
                case "FShadowProjectionMaskPixelShader":
                    node.Items.Add(FShaderParameter("LightDirection"));
                    node.Items.Add(FShaderParameter("ScreenPositionScaleBias"));
                    node.Items.Add(FShaderResourceParameter("SceneNormalTexture"));
                    break;
                case "TModShadowProjectionPixelShaderFPointLightPolicyF16SampleManualPCF":
                case "TModShadowProjectionPixelShaderFPointLightPolicyF16SampleFetch4PCF":
                case "TModShadowProjectionPixelShaderFPointLightPolicyF16SampleHwPCF":
                case "TModShadowProjectionPixelShaderFPointLightPolicyF4SampleManualPCF":
                case "TModShadowProjectionPixelShaderFPointLightPolicyF4SampleHwPCF":
                    TModShadowProjectionPixelShader();
                    FPointLightPolicy_ModShadowPixelParamsType();
                    break;
                case "FShaderComplexityApplyPixelShader":
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FShaderParameter("ShaderComplexityColors"));
                    break;
                case "FCopyTranslucencyDepthPixelShader":
                case "TDownsampleDepthPixelShaderTRUE":
                case "TDownsampleDepthPixelShaderFALSE":
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    break;
                case "FApplyLightShaftsPixelShader":
                    node.Items.Add(FLightShaftPixelShaderParameters("LightShaftParameters"));
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FShaderResourceParameter("SmallSceneColorTexture"));
                    break;
                case "FFXAAResolveComputeShader":
                    node.Items.Add(FShaderResourceParameter("WorkQueueH"));
                    node.Items.Add(FShaderResourceParameter("WorkQueueV"));
                    node.Items.Add(FShaderResourceParameter("IndirectParams"));
                    break;
                case "FDownsampleSceneDepthAndNormalsPixelShader":
                    node.Items.Add(FShaderParameter("ProjectionScaleBias"));
                    node.Items.Add(FShaderParameter("SourceTexelOffsets01"));
                    node.Items.Add(FShaderParameter("SourceTexelOffsets23"));
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FShaderResourceParameter("FullSizedNormalsTexture"));
                    node.Items.Add(FShaderParameter("OffsetIndex"));
                    break;
                case "FFXAAPrepComputeShader":
                    node.Items.Add(FShaderResourceParameter("HWork"));
                    node.Items.Add(FShaderResourceParameter("VWork"));
                    node.Items.Add(FShaderResourceParameter("Color"));
                    node.Items.Add(FShaderResourceParameter("Luma"));
                    node.Items.Add(FShaderResourceParameter("LinearSampler"));
                    node.Items.Add(FShaderParameter("RcpTextureSize"));
                    node.Items.Add(FShaderParameter("TextureOffset"));
                    node.Items.Add(FShaderParameter("ContrastThreshold"));
                    node.Items.Add(FShaderParameter("SubpixelRemoval"));
                    break;
                case "Fetch4PCFMediumQualityShaderName":
                case "HwPCFMediumQualityShaderName":
                case "Fetch4PCFHighQualityShaderName":
                case "HwPCFHighQualityShaderName":
                case "Fetch4PCFLowQualityShaderName":
                case "HwPCFLowQualityShaderName":
                case "HighQualityShaderName":
                case "MediumQualityShaderName":
                case "LowQualityShaderName":
                    FBranchingPCFProjectionPixelShader();
                    break;
                case "FSRGBMLAAEdgeDetectionPixelShader":
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FShaderParameter("gRTSize"));
                    node.Items.Add(FShaderParameter("gLuminanceEquation"));
                    node.Items.Add(FShaderParameter("gInverseDisplayGamma"));
                    break;
                case "THeightFogVertexShader<4>":
                case "THeightFogVertexShader<1>":
                    node.Items.Add(FShaderParameter("ScreenPositionScaleBias"));
                    node.Items.Add(FShaderParameter("FogMinHeight"));
                    node.Items.Add(FShaderParameter("FogMaxHeight"));
                    node.Items.Add(FShaderParameter("ScreenToWorld"));
                    break;
                case "FApplyLightShaftsVertexShader":
                    node.Items.Add(FShaderParameter("SourceTextureScaleBias"));
                    node.Items.Add(FShaderParameter("SceneColorScaleBias"));
                    break;
                case "TBasePassPixelShaderFSHLightLightMapPolicySkyLight":
                case "TBasePassPixelShaderFSHLightLightMapPolicyNoSkyLight":
                    FSHLightLightMapPolicy_PixelParametersType();
                    TBasePassPixelShader();
                    break;
                case "FLUTBlenderPixelShader<1>": // Verified LE2
                case "FLUTBlenderPixelShader<2>": // Verified LE2
                case "FLUTBlenderPixelShader<3>": // Verified LE2
                case "FLUTBlenderPixelShader<4>": // Verified LE2
                case "FLUTBlenderPixelShader<5>": // Verified LE2
                    int blendCount = shaderType[^2] - 48; // ASCII value 48 is CHAR 0, 49 is CHAR 1... etc
                    for (int i = 1; i <= blendCount; i++)
                    {
                        node.Items.Add(FShaderResourceParameter($"Texture{i}"));
                    }
                    node.Items.Add(FShaderParameter("Weights"));
                    node.Items.Add(FGammaShaderParameters("GammaParameters")); // Verified LE2
                    node.Items.Add(FColorRemapShaderParameters("MaterialParameters")); // Verified LE2
                    break;
                case "FFluidNormalPixelShader":
                    node.Items.Add(FShaderParameter("CellSize"));
                    node.Items.Add(FShaderParameter("HeightScale"));
                    node.Items.Add(FShaderResourceParameter("HeightTexture"));
                    node.Items.Add(FShaderParameter("SplineMargin"));
                    break;
                case "TDownsampleLightShaftsPixelShader<LS_Spot>":
                case "TDownsampleLightShaftsPixelShader<LS_Directional>":
                case "TDownsampleLightShaftsPixelShader<LS_Point>":
                    node.Items.Add(FLightShaftPixelShaderParameters("LightShaftParameters"));
                    node.Items.Add(FShaderParameter("SampleOffsets"));
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FShaderResourceParameter("SmallSceneColorTexture"));
                    break;
                case "FModShadowMeshPixelShader":
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("AttenAllowed"));
                    break;
                case "FFluidSimulatePixelShader":
                    node.Items.Add(FShaderParameter("CellSize"));
                    node.Items.Add(FShaderParameter("DampFactor"));
                    node.Items.Add(FShaderParameter("TravelSpeed"));
                    node.Items.Add(FShaderParameter("PreviousOffset1"));
                    node.Items.Add(FShaderParameter("PreviousOffset2"));
                    node.Items.Add(FShaderResourceParameter("PreviousHeights1"));
                    node.Items.Add(FShaderResourceParameter("PreviousHeights2"));
                    break;
                case "FApplyForcePixelShader":
                    node.Items.Add(FShaderParameter("ForcePosition"));
                    node.Items.Add(FShaderParameter("ForceRadius"));
                    node.Items.Add(FShaderParameter("ForceMagnitude"));
                    node.Items.Add(FShaderResourceParameter("PreviousHeights1"));
                    break;
                case "FDOFAndBloomBlendPixelShader":
                    FDOFAndBloomBlendPixelShader();
                    break;
                case "TDOFBoxBlurMinPixelShader<3>":
                case "TDOFBoxBlurMaxPixelShader<5>":
                case "TDOFBlur1PixelShader<4>":
                case "TDOFBoxBlurMinPixelShader<2>":
                case "TDOFBoxBlurMaxPixelShader<4>":
                case "TDOFBlur1PixelShader<3>":
                case "TDOFBoxBlurMinPixelShader<5>":
                case "TDOFBoxBlurMaxPixelShader<3>":
                case "TDOFBoxBlurMinPixelShader<4>":
                case "TDOFBoxBlurMaxPixelShader<2>":
                case "TDOFBlur2PixelShader<8>":
                case "TDOFBlur2PixelShader<6>":
                case "TDOFBlur2PixelShader<4>":
                case "TDOFBlur2PixelShader<3>":
                case "TDOFBlur1PixelShader<8>":
                case "TDOFBlur1PixelShader<6>":
                    node.Items.Add(FDOFShaderParameters("DOFParameters"));
                    node.Items.Add(FShaderResourceParameter("DOFTempTexture"));
                    node.Items.Add(FShaderResourceParameter("DOFTempTexture2"));
                    node.Items.Add(FShaderParameter("DOFKernelParams"));
                    node.Items.Add(FShaderParameter("BlurDirections"));
                    break;
                case "TDOFAndBloomGatherPixelShader<MAX_FILTER_SAMPLES>":
                case "TBloomGatherPixelShader<NumFPFilterSamples>":
                case "TDOFAndBloomGatherPixelShader<NumFPFilterSamples>":
                    TDOFAndBloomGatherPixelShader();
                    break;
                case "TLightMapDensityPixelShader<FDirectionalLightMapTexturePolicy>":
                case "TLightMapDensityPixelShader<FDummyLightMapTexturePolicy>":
                case "TLightMapDensityPixelShader<FSimpleLightMapTexturePolicy>":
                    FLightMapTexturePolicy_PixelParametersType();
                    TLightMapDensityPixelShader();
                    break;
                case "TDOFGatherPixelShader<NumFPFilterSamples>":
                    TDOFAndBloomGatherPixelShader();
                    node.Items.Add(FShaderParameter("InputTextureSize"));
                    break;
                case "TModShadowVolumePixelShaderFDirectionalLightPolicy":
                    FModShadowVolumePixelShader_Maybe();
                    //FDirectionalLightPolicy::ModShadowPixelParamsType has no params
                    break;
                case "FHBAOBlurComputeShader":
                    node.Items.Add(FHBAOShaderParameters("HBAOParameters"));
                    node.Items.Add(FShaderResourceParameter("AOTexture"));
                    node.Items.Add(FShaderResourceParameter("BlurOut"));
                    node.Items.Add(FShaderParameter("AOTexDimensions"));
                    break;
                case "FHBAODeinterleaveComputeShader":
                    node.Items.Add(FHBAOShaderParameters("HBAOParameters"));
                    node.Items.Add(FShaderResourceParameter("SceneDepthTexture"));
                    node.Items.Add(FShaderResourceParameter("DeinterleaveOut"));
                    node.Items.Add(FShaderParameter("ArrayOffset"));
                    break;
                case "FFXAA3VertexShader":
                    node.Items.Add(FShaderParameter("rcpFrame"));
                    node.Items.Add(FShaderParameter("rcpFrameOpt"));
                    break;
                case "FSimpleElementDistanceFieldGammaPixelShader":
                    node.Items.Add(FShaderParameter("SmoothWidth"));
                    node.Items.Add(FShaderParameter("EnableShadow"));
                    node.Items.Add(FShaderParameter("ShadowDirection"));
                    node.Items.Add(FShaderParameter("ShadowColor"));
                    node.Items.Add(FShaderParameter("ShadowSmoothWidth"));
                    node.Items.Add(FShaderParameter("EnableGlow"));
                    node.Items.Add(FShaderParameter("GlowColor"));
                    node.Items.Add(FShaderParameter("GlowOuterRadius"));
                    node.Items.Add(FShaderParameter("GlowInnerRadius"));
                    break;
                case "TShadowDepthVertexShader<ShadowDepth_OutputDepthToColor>":
                case "TShadowDepthVertexShader<ShadowDepth_PerspectiveCorrect>":
                case "TShadowDepthVertexShader<ShadowDepth_OutputDepth>":
                    node.Items.Add(FVertexFactoryParameterRef());
                    node.Items.Add(FMaterialVertexShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("ProjectionMatrix"));
                    node.Items.Add(FShaderParameter("InvMaxSubjectDepth"));
                    node.Items.Add(FShaderParameter("DepthBias"));
                    node.Items.Add(FShaderParameter("bClampToNearPlane"));
                    break;
                case "FSimpleElementMaskedGammaPixelShader": // Verified: LE2
                    FSimpleElementGammaPixelShader();
                    node.Items.Add(FShaderParameter("ClipRef"));
                    break;
                case "FSimpleElementGammaPixelShader":
                    FSimpleElementGammaPixelShader();
                    break;
                case "FGenerateDeinterleavedHBAOComputeShader":
                    node.Items.Add(FHBAOShaderParameters("HBAOParameters"));
                    node.Items.Add(FShaderResourceParameter("OutAO"));
                    node.Items.Add(FShaderResourceParameter("QuarterResDepthCS"));
                    node.Items.Add(FShaderResourceParameter("ViewNormalTex"));
                    node.Items.Add(FShaderParameter("JitterCS"));
                    node.Items.Add(FShaderParameter("ArrayOffset"));
                    break;
                case "FHBAOReconstructNormalsComputeShader":
                    node.Items.Add(FHBAOShaderParameters("HBAOParameters"));
                    node.Items.Add(FShaderResourceParameter("SceneDepthTexture"));
                    node.Items.Add(FShaderResourceParameter("ReconstructNormalOut"));
                    break;
                case "TAOMaskPixelShader<AO_HistoryUpdateManualDepthTest>":
                case "TAOMaskPixelShader<AO_HistoryMaskManualDepthTest>":
                case "TAOMaskPixelShader<AO_HistoryUpdate>":
                case "TAOMaskPixelShader<AO_HistoryMask>":
                case "TAOMaskPixelShader<AO_OcclusionMask>":
                    node.Items.Add(FAmbientOcclusionParams("AOParams"));
                    node.Items.Add(FShaderParameter("HistoryConvergenceRates"));
                    break;
                case "FStaticHistoryUpdatePixelShader":
                    node.Items.Add(FAmbientOcclusionParams("AOParams"));
                    node.Items.Add(FShaderParameter("PrevViewProjMatrix"));
                    node.Items.Add(FShaderParameter("HistoryConvergenceRates"));
                    break;
                case "TEdgePreservingFilterPixelShader<30>":
                case "TEdgePreservingFilterPixelShader<20>":
                case "TEdgePreservingFilterPixelShader<28>":
                case "TEdgePreservingFilterPixelShader<26>":
                case "TEdgePreservingFilterPixelShader<24>":
                case "TEdgePreservingFilterPixelShader<22>":
                case "TEdgePreservingFilterPixelShader<1>":
                case "TEdgePreservingFilterPixelShader<18>":
                case "TEdgePreservingFilterPixelShader<16>":
                case "TEdgePreservingFilterPixelShader<14>":
                case "TEdgePreservingFilterPixelShader<12>":
                case "TEdgePreservingFilterPixelShader<10>":
                case "TEdgePreservingFilterPixelShader<8>":
                case "TEdgePreservingFilterPixelShader<6>":
                case "TEdgePreservingFilterPixelShader<4>":
                case "TEdgePreservingFilterPixelShader<2>":
                    node.Items.Add(FAmbientOcclusionParams("AOParams"));
                    node.Items.Add(FShaderParameter("FilterSampleOffsets"));
                    node.Items.Add(FShaderParameter("FilterParameters"));
                    node.Items.Add(FShaderParameter("CustomParameters"));
                    break;
                case "TAmbientOcclusionPixelShaderFDefaultQualityAOTRUEFALSE":
                case "TAmbientOcclusionPixelShaderFDefaultQualityAOFALSETRUE":
                case "TAmbientOcclusionPixelShaderFDefaultQualityAOFALSEFALSE":
                case "TAmbientOcclusionPixelShaderFDefaultQualityAOTRUETRUE":
                    node.Items.Add(FShaderParameter("OcclusionSampleOffsets"));
                    node.Items.Add(FShaderResourceParameter("RandomNormalTexture"));
                    node.Items.Add(FShaderParameter("ProjectionScale"));
                    node.Items.Add(FShaderParameter("ProjectionMatrix"));
                    node.Items.Add(FShaderParameter("NoiseScale"));
                    node.Items.Add(FAmbientOcclusionParams("AOParams"));
                    node.Items.Add(FShaderParameter("OcclusionCalcParameters"));
                    node.Items.Add(FShaderParameter("HaloDistanceScale"));
                    node.Items.Add(FShaderParameter("OcclusionRemapParameters"));
                    node.Items.Add(FShaderParameter("OcclusionFadeoutParameters"));
                    node.Items.Add(FShaderParameter("MaxRadiusTransform"));
                    break;
                case "FBinkGpuShaderYCrCbToRGBNoAlpha":
                case "FBinkGpuShaderYCrCbToRGB":
                    node.Items.Add(FShaderResourceParameter("YTex"));
                    node.Items.Add(FShaderResourceParameter("CrCbTex"));
                    node.Items.Add(FShaderResourceParameter("ATex"));
                    node.Items.Add(FShaderParameter("cmatrix"));
                    node.Items.Add(FShaderParameter("alpha_mult"));
                    break;
                case "FBinkGpuShaderHDRNoAlpha": // Verified LE2
                case "FBinkGpuShaderHDR": // Verified LE2
                    node.Items.Add(FShaderResourceParameter("YTex"));
                    node.Items.Add(FShaderResourceParameter("CrCbTex"));
                    node.Items.Add(FShaderResourceParameter("ATex"));
                    node.Items.Add(FShaderResourceParameter("HTex"));
                    node.Items.Add(FShaderParameter("alpha_mult"));
                    node.Items.Add(FShaderParameter("hdr"));
                    node.Items.Add(FShaderParameter("ctcp"));
                    break;
                case "FBinkYCrCbAToRGBAPixelShader": // Verified LE2. SErialization method seems to show it << parameters before shader?
                    node.Items.Add(FShaderResourceParameter("tex3"));
                    break;
                case "FBinkYCrCbToRGBNoPixelAlphaPixelShader": // Verified LE2
                    node.Items.Add(FShaderResourceParameter("tex0"));
                    node.Items.Add(FShaderResourceParameter("tex1"));
                    node.Items.Add(FShaderResourceParameter("tex2"));
                    node.Items.Add(FShaderParameter("crc"));
                    node.Items.Add(FShaderParameter("cbc"));
                    node.Items.Add(FShaderParameter("adj"));
                    node.Items.Add(FShaderParameter("yscale"));
                    node.Items.Add(FShaderParameter("consts"));
                    break;
                case "TLightPixelShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                    FSpotLightPolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "TLightMapDensityVertexShader<FDummyLightMapTexturePolicy>":
                case "TLightMapDensityVertexShader<FDirectionalLightMapTexturePolicy>":
                case "TLightMapDensityVertexShader<FSimpleLightMapTexturePolicy>":
                    FLightMapTexturePolicy_VertexParametersType();
                    node.Items.Add(FVertexFactoryParameterRef());
                    node.Items.Add(FMaterialVertexShaderParameters("MaterialParameters"));
                    break;
                case "TLightVertexShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                    FDirectionalLightPolicy_VertexParametersType();
                    TLightVertexShader();
                    break;
                case "TLightVertexShaderFSpotLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFSpotLightPolicyFShadowVertexBufferPolicy":
                case "TLightVertexShaderFPointLightPolicyFNoStaticShadowingPolicy":
                case "TLightVertexShaderFPointLightPolicyFShadowVertexBufferPolicy":
                    FPointLightPolicy_VertexParametersType();
                    TLightVertexShader();
                    break;
                case "TLightPixelShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                    FSphericalHarmonicLightPolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "TLightPixelShaderFPointLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFPointLightPolicyFShadowVertexBufferPolicy":
                    FPointLightPolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "TLightVertexShaderFSphericalHarmonicLightPolicyFNoStaticShadowingPolicy":
                    TLightVertexShader();
                    break;
                case "FModShadowMeshVertexShader":
                    node.Items.Add(FVertexFactoryParameterRef());
                    node.Items.Add(FMaterialVertexShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("LightPosition"));
                    break;
                case "TLightPixelShaderFDirectionalLightPolicyFNoStaticShadowingPolicy":
                case "TLightPixelShaderFDirectionalLightPolicyFShadowVertexBufferPolicy":
                    FDirectionalLightPolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "FSFXWorldNormalPixelShader":
                case "TDepthOnlyScreenDoorPixelShader":
                case "FTranslucencyPostRenderDepthPixelShader":
                case "TDistortionMeshPixelShader<FDistortMeshAccumulatePolicy>":
                case "TDepthOnlySolidPixelShader":
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    break;
                case "FSFXWorldNormalVertexShader":
                case "TLightMapDensityVertexShader<FNoLightMapPolicy>":
                case "FTextureDensityVertexShader":
                case "TDepthOnlyVertexShader<0>":
                case "FHitProxyVertexShader":
                case "TDistortionMeshVertexShader<FDistortMeshAccumulatePolicy>":
                case "FFogVolumeApplyVertexShader":
                case "TDepthOnlyVertexShader<1>":
                    node.Items.Add(FVertexFactoryParameterRef());
                    node.Items.Add(FMaterialVertexShaderParameters("MaterialParameters"));
                    break;
                case "TLightPixelShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                    FSFXPointLightPolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "TLightVertexShaderFSFXPointLightPolicyFNoStaticShadowingPolicy":
                    FSFXPointLightPolicy_VertexParametersType();
                    TLightVertexShader();
                    break;
                case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFNoDensityPolicy":
                    FVertexLightMapPolicy_VertexParametersType();
                    TBasePassVertexShader();
                    //FNoDensityPolicy::VertexShaderParametersType has no params
                    break;
                case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFNoDensityPolicy":
                    FLightMapTexturePolicy_VertexParametersType();
                    TBasePassVertexShader();
                    //FNoDensityPolicy::VertexShaderParametersType has no params
                    break;
                case "TBasePassVertexShaderFSHLightLightMapPolicyFNoDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFNoDensityPolicy":
                    FDirectionalLightLightMapPolicy_VertexParametersType();
                    TBasePassVertexShader();
                    //FNoDensityPolicy::VertexShaderParametersType has no params
                    break;
                case "TBasePassPixelShaderFDirectionalLightLightMapPolicySkyLight":
                case "TBasePassPixelShaderFDirectionalLightLightMapPolicyNoSkyLight":
                    FDirectionalLightLightMapPolicy_PixelParametersType();
                    TBasePassPixelShader();
                    break;
                case "TBasePassPixelShaderFNoLightMapPolicySkyLight":
                case "TBasePassPixelShaderFNoLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicySkyLight":
                case "TBasePassPixelShaderFCustomSimpleVertexLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicySkyLight":
                case "TBasePassPixelShaderFCustomVectorVertexLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFSimpleVertexLightMapPolicySkyLight":
                case "TBasePassPixelShaderFSimpleVertexLightMapPolicyNoSkyLight":
                case "TBasePassPixelShaderFDirectionalVertexLightMapPolicySkyLight":
                case "TBasePassPixelShaderFDirectionalVertexLightMapPolicyNoSkyLight":
                    //PixelParametersType for these LightMapPolicys have no params
                    TBasePassPixelShader();
                    break;
                case "FVelocityPixelShader":
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("VelocityScaleOffset"));
                    node.Items.Add(FShaderParameter("IndividualVelocityScale"));
                    node.Items.Add(FShaderParameter("ObjectVelocity"));
                    break;
                case "FVelocityVertexShader": // LE2 Verified, but did not see vertex factory serialization 
                    node.Items.Add(FVertexFactoryParameterRef());
                    node.Items.Add(FMaterialVertexShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("PrevViewProjectionMatrix"));
                    node.Items.Add(FShaderParameter("PreviousLocalToWorld"));
                    node.Items.Add(FShaderParameter("StretchTimeScale"));
                    break;
                case "FTextureDensityPixelShader":
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("TextureDensityParameters"));
                    node.Items.Add(FShaderParameter("TextureLookupInfo"));
                    break;
                case "TShadowDepthPixelShaderTRUETRUE":
                case "TShadowDepthPixelShaderFALSEFALSE":
                case "TShadowDepthPixelShaderFALSETRUE":
                case "TShadowDepthPixelShaderTRUEFALSE":
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("InvMaxSubjectDepth"));
                    node.Items.Add(FShaderParameter("DepthBias"));
                    break;
                case "FHitProxyPixelShader":
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("HitProxyId"));
                    break;
                case "TLightMapDensityPixelShader<FNoLightMapPolicy>":
                    TLightMapDensityPixelShader();
                    break;
                case "TBasePassVertexShaderFNoLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFNoLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFNoLightMapPolicyFConstantDensityPolicy":
                    TBasePassVertexShader();
                    FConstantDensityPolicy_VertexShaderParametersType();
                    break;
                case "TLightPixelShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    FSpotLightPolicy_PixelParametersType();
                    FSignedDistanceFieldShadowTexturePolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "TLightVertexShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightVertexShaderFPointLightPolicyFShadowTexturePolicy":
                    FPointLightPolicy_VertexParametersType();
                    FShadowTexturePolicy_VertexParametersType();
                    TLightVertexShader();
                    break;
                case "TLightVertexShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightVertexShaderFDirectionalLightPolicyFShadowTexturePolicy":
                    FDirectionalLightPolicy_VertexParametersType();
                    FShadowTexturePolicy_VertexParametersType();
                    TLightVertexShader();
                    break;
                case "TLightVertexShaderFSpotLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                case "TLightVertexShaderFSpotLightPolicyFShadowTexturePolicy":
                    FSpotLightPolicy_VertexParametersType();
                    FShadowTexturePolicy_VertexParametersType();
                    TLightVertexShader();
                    break;
                case "TLightPixelShaderFSpotLightPolicyFShadowTexturePolicy":
                    FSpotLightPolicy_PixelParametersType();
                    FShadowTexturePolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "TLightPixelShaderFPointLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    FPointLightPolicy_PixelParametersType();
                    FSignedDistanceFieldShadowTexturePolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "TLightPixelShaderFPointLightPolicyFShadowTexturePolicy":
                    FPointLightPolicy_PixelParametersType();
                    FShadowTexturePolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "TLightPixelShaderFDirectionalLightPolicyFSignedDistanceFieldShadowTexturePolicy":
                    FDirectionalLightPolicy_PixelParametersType();
                    FSignedDistanceFieldShadowTexturePolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "TLightPixelShaderFDirectionalLightPolicyFShadowTexturePolicy":
                    FDirectionalLightPolicy_PixelParametersType();
                    FShadowTexturePolicy_PixelParametersType();
                    TLightPixelShader();
                    break;
                case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicySkyLight":
                case "TBasePassPixelShaderFCustomSimpleLightMapTexturePolicyNoSkyLight":
                case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicySkyLight":
                case "TBasePassPixelShaderFCustomVectorLightMapTexturePolicyNoSkyLight":
                    FCustomLightMapTexturePolicy_PixelParametersType();
                    TBasePassPixelShader();
                    break;
                case "TBasePassPixelShaderFSimpleLightMapTexturePolicySkyLight":
                case "TBasePassPixelShaderFSimpleLightMapTexturePolicyNoSkyLight":
                case "TBasePassPixelShaderFDirectionalLightMapTexturePolicySkyLight":
                case "TBasePassPixelShaderFDirectionalLightMapTexturePolicyNoSkyLight":
                    FLightMapTexturePolicy_PixelParametersType();
                    TBasePassPixelShader();
                    break;
                case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorVertexLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFSimpleVertexLightMapPolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFDirectionalVertexLightMapPolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleVertexLightMapPolicyFConstantDensityPolicy":
                    FVertexLightMapPolicy_VertexParametersType();
                    TBasePassVertexShader();
                    FConstantDensityPolicy_VertexShaderParametersType();
                    break;
                case "TBasePassVertexShaderFSHLightLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFSHLightLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFSHLightLightMapPolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightLightMapPolicyFConstantDensityPolicy":
                    FDirectionalLightLightMapPolicy_VertexParametersType();
                    TBasePassVertexShader();
                    FConstantDensityPolicy_VertexShaderParametersType();
                    break;
                case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFSimpleLightMapTexturePolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFDirectionalLightMapTexturePolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFConstantDensityPolicy":
                case "TBasePassVertexShaderFCustomVectorLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFSphereDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFLinearHalfspaceDensityPolicy":
                case "TBasePassVertexShaderFCustomSimpleLightMapTexturePolicyFConstantDensityPolicy":
                    FLightMapTexturePolicy_VertexParametersType();
                    TBasePassVertexShader();
                    FConstantDensityPolicy_VertexShaderParametersType();
                    break;
                case "FFogVolumeApplyPixelShader":
                    node.Items.Add(FShaderParameter("MaxIntegral"));
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderResourceParameter("AccumulatedFrontfacesLineIntegralTexture"));
                    node.Items.Add(FShaderResourceParameter("AccumulatedBackfacesLineIntegralTexture"));
                    break;
                case "TFogIntegralPixelShader<FSphereDensityPolicy>":
                case "TFogIntegralPixelShader<FLinearHalfspaceDensityPolicy>":
                case "TFogIntegralPixelShader<FConstantDensityPolicy>":
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("DepthFilterSampleOffsets"));
                    node.Items.Add(FShaderParameter("ScreenToWorld"));
                    node.Items.Add(FShaderParameter("FogCameraPosition"));
                    node.Items.Add(FShaderParameter("FaceScale"));
                    node.Items.Add(FShaderParameter("FirstDensityFunctionParameters"));
                    node.Items.Add(FShaderParameter("SecondDensityFunctionParameters"));
                    node.Items.Add(FShaderParameter("StartDistance"));
                    node.Items.Add(FShaderParameter("InvMaxIntegral"));
                    break;
                case "TFogIntegralVertexShader<FSphereDensityPolicy>":
                case "TFogIntegralVertexShader<FLinearHalfspaceDensityPolicy>":
                case "TFogIntegralVertexShader<FConstantDensityPolicy>":
                    node.Items.Add(FVertexFactoryParameterRef());
                    node.Items.Add(FMaterialVertexShaderParameters("MaterialParameters"));
                    node.Items.Add(FHeightFogVertexShaderParameters("HeightFogParameters"));
                    break;
                case "FRadialBlurVelocityPixelShader":
                case "FRadialBlurPixelShader":
                    node.Items.Add(FShaderParameter("RadialBlurScale"));
                    node.Items.Add(FShaderParameter("RadialBlurFalloffExp"));
                    node.Items.Add(FShaderParameter("RadialBlurOpacity"));
                    node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    break;
                case "FHitMaskPixelShader":
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("HitStartLocation"));
                    node.Items.Add(FShaderParameter("HitLocation"));
                    node.Items.Add(FShaderParameter("HitRadius"));
                    node.Items.Add(FShaderParameter("HitCullDistance"));
                    node.Items.Add(FShaderResourceParameter("CurrentMaskTexture"));
                    break;
                case "FHitMaskVertexShader":
                    node.Items.Add(FVertexFactoryParameterRef());
                    node.Items.Add(FMaterialVertexShaderParameters("MaterialParameters"));
                    node.Items.Add(FShaderParameter("PixelCenterOffset"));
                    break;
                case "TAOMeshVertexShader<0>":
                case "TAOMeshVertexShader<1>":
                    node.Items.Add(FVertexFactoryParameterRef());
                    node.Items.Add(FShaderParameter("PrevViewProjectionMatrix"));
                    node.Items.Add(FShaderParameter("PreviousLocalToWorld"));
                    break;
                case "FLightFunctionPixelShader":
                    node.Items.Add(FShaderResourceParameter("SceneColorTexture"));
                    node.Items.Add(FShaderResourceParameter("SceneDepthTexture"));
                    node.Items.Add(FShaderParameter("MinZ_MaxZRatio"));
                    node.Items.Add(FShaderParameter("ScreenPositionScaleBias"));
                    node.Items.Add(FShaderParameter("ScreenToLight"));
                    node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
                    break;
                case "FUberPostProcessBlendPixelShader1001": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader1010": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader1100": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader0110": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader0011": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader1111": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader0101": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader1000": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader1011": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader1101": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader0111": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader0010": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader1110": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader0100": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader0001": // LE2 - Not in LE3
                case "FUberPostProcessBlendPixelShader0000": // LE2 - Not in LE3
                    FDOFAndBloomBlendPixelShader();
                    break;
                default:
                    Debugger.Break();
                    node = null;
                    break;
            }
        }
        catch (Exception e)
        {
            exception = e;
            node.Items.Add(new BinInterpNode { Header = $"Error reading binary data: {e}" });
        }

        return node;

        BinInterpNode FShaderParameter(string name)
        {
            return new BinInterpNode(bin.Position, $"{name}: FShaderParameter")
            {
                Items =
                {
                    MakeUInt16Node(bin, "BaseIndex"),
                    MakeUInt16Node(bin, "NumBytes"),
                    MakeUInt16Node(bin, "BufferIndex"),
                },
                Length = 6
            };
        }

        BinInterpNode FShaderResourceParameter(string name)
        {
            return FShaderResourceParameterIndexed(name, -1);
        }


        BinInterpNode FShaderResourceParameterIndexed(string name, int loopIndexForOneBasedIndexSuffix)
        {
            if (loopIndexForOneBasedIndexSuffix is > 0)
            {
                name += (loopIndexForOneBasedIndexSuffix + 1); // Textures -> Textures2 if index value is 1, but just Textures if value is 0.
            }
            var header = $"{name}: FShaderResourceParameter";
            return new BinInterpNode(bin.Position, header)
            {
                Items =
                {
                    MakeUInt16Node(bin, "BaseIndex"),
                    MakeUInt16Node(bin, "NumResources"),
                    MakeUInt16Node(bin, "SamplerIndex"),
                },
                Length = 6
            };
        }

        BinInterpNode FVertexFactoryParameterRef()
        {
            var vertexFactoryParameterRef = new BinInterpNode(bin.Position, $"VertexFactoryParameters: FVertexFactoryParameterRef")
            {
                Items =
                {
                    MakeNameNode(bin, "VertexFactoryType", out var vertexFactoryName),
                    MakeUInt32HexNode(bin, "File offset to end of FVertexFactoryParameterRef (may be inaccurate in modded files)")
                },
                IsExpanded = true
            };
            vertexFactoryParameterRef.Items.AddRange(FVertexFactoryShaderParameters(vertexFactoryName));
            return vertexFactoryParameterRef;
        }

        BinInterpNode FSceneTextureShaderParameters(string name) // Verified LE2
        {
            return new BinInterpNode(bin.Position, $"{name}: FSceneTextureShaderParameters")
            {
                Items =
                {
                    FShaderResourceParameter("SceneColorTexture"),
                    FShaderResourceParameter("SceneDepthTexture"),
                    FShaderParameter("MinZ_MaxZRatio"),
                    FShaderParameter("ScreenPositionScaleBias"),
                },
            };
        }

        BinInterpNode FMaterialShaderParameters(string name, string type = "FMaterialShaderParameters")
        {
            return new BinInterpNode(bin.Position, $"{name}: {type}")
            {
                Items =
                {
                    FShaderParameter("CameraWorldPosition"),
                    FShaderParameter("ObjectWorldPositionAndRadius"),
                    FShaderParameter("ObjectOrientation"),
                    FShaderParameter("WindDirectionAndSpeed"),
                    FShaderParameter("FoliageImpulseDirection"),
                    FShaderParameter("FoliageNormalizedRotationAxisAndAngle"),
                }
            };
        }

        BinInterpNode FMaterialPixelShaderParameters(string name)
        {
            var super = FMaterialShaderParameters(name, "FMaterialPixelShaderParameters");
            super.Items.AddRange(
            [
                MakeArrayNode(bin, "UniformPixelScalarShaderParameters", _ => TUniformParameter(FShaderParameter)),
                MakeArrayNode(bin, "UniformPixelVectorShaderParameters", _ => TUniformParameter(FShaderParameter)),
                MakeArrayNode(bin, "UniformPixel2DShaderResourceParameters", _ => TUniformParameter(FShaderResourceParameter)),
                MakeArrayNode(bin, "UniformPixelCubeShaderResourceParameters", _ => TUniformParameter(FShaderResourceParameter)),
                FShaderParameter("LocalToWorld"),
                FShaderParameter("WorldToLocal"),
                FShaderParameter("WorldToView"),
                FShaderParameter("InvViewProjection"),
                FShaderParameter("ViewProjection"),
                FSceneTextureShaderParameters("SceneTextureParameters"),
                FShaderParameter("TwoSidedSign"),
                FShaderParameter("InvGamma"),
                FShaderParameter("DecalFarPlaneDistance"),
                FShaderParameter("ObjectPostProjectionPosition"),
                FShaderParameter("ObjectMacroUVScales"),
                FShaderParameter("ObjectNDCPosition"),
                FShaderParameter("OcclusionPercentage"),
                FShaderParameter("EnableScreenDoorFade"),
                FShaderParameter("ScreenDoorFadeSettings"),
                FShaderParameter("ScreenDoorFadeSettings2"),
                FShaderResourceParameter("ScreenDoorNoiseTexture"),
                //false if any params in the related arrays have NumBytes != 16,
                //or have differing BufferIndex values, or have Index values not in sequence,
                //or have a BaseIndex value that is not the sum of the previous params BaseIndex and NumBytes values
                MakeBoolIntNode(bin, "UniformPixelScalarShaderParameters is well formed?"),
                MakeBoolIntNode(bin, "UniformPixelVectorShaderParameters is well formed?"),
                FShaderParameter("WrapLightingParameters")
            ]);
            return super;
        }

        BinInterpNode FMaterialVertexShaderParameters(string name)
        {
            var super = FMaterialShaderParameters(name, "FMaterialVertexShaderParameters");
            super.Items.AddRange(
            [
                MakeArrayNode(bin, "UniformVertexScalarShaderParameters", _ => TUniformParameter(FShaderParameter)),
                MakeArrayNode(bin, "UniformVertexVectorShaderParameters", _ => TUniformParameter(FShaderParameter)),
            ]);
            return super;
        }

        BinInterpNode TUniformParameter(Func<string, BinInterpNode> parameter)
        {
            return parameter($"[{bin.ReadInt32()}]");
        }

        IEnumerable<ITreeItem> FVertexFactoryShaderParameters(string vertexFactor)
        {
            switch (vertexFactor)
            {
                case "FLocalVertexFactory":
                case "FLocalVertexFactoryApex":
                    return
                    [
                        FShaderParameter("LocalToWorld"),
                        FShaderParameter("LocalToWorldRotDeterminantFlip"),
                        FShaderParameter("WorldToLocal"),
                    ];
                case "FFluidTessellationVertexFactory":
                    return
                    [
                        .. FVertexFactoryShaderParameters("FLocalVertexFactory"),
                        FShaderParameter("GridSize"),
                        FShaderParameter("TessellationParameters"),
                        FShaderResourceParameter("Heightmap"),
                        FShaderParameter("TessellationFactors1"),
                        FShaderParameter("TessellationFactors2"),
                        FShaderParameter("TexcoordScaleBias"),
                        FShaderParameter("SplineParameters"),
                    ];
                case "FFoliageVertexFactory":
                    return
                    [
                        FShaderParameter("InvNumVerticesPerInstance"),
                        FShaderParameter("NumVerticesPerInstance"),
                    ];
                case "FGPUSkinMorphDecalVertexFactory":
                case "FGPUSkinDecalVertexFactory":
                    return
                    [
                        .. FVertexFactoryShaderParameters("FGPUSkinVertexFactory"),
                        FShaderParameter("BoneToDecalRow0"),
                        FShaderParameter("BoneToDecalRow1"),
                        FShaderParameter("DecalLocation"),
                    ];
                case "FGPUSkinVertexFactory":
                case "FGPUSkinMorphVertexFactory":
                    return
                    [
                        FShaderParameter("LocalToWorld"),
                        FShaderParameter("WorldToLocal"),
                        FShaderParameter("BoneMatrices"),
                        FShaderParameter("MaxBoneInfluences"),
                        FShaderParameter("MeshOrigin"),
                        FShaderParameter("MeshExtension"),
                        FShaderParameter("WoundEllipse0"),
                        FShaderParameter("WoundEllipse1"),
                    ];
                case "FInstancedStaticMeshVertexFactory":
                    return
                    [
                        .. FVertexFactoryShaderParameters("FLocalVertexFactory"),
                        FShaderParameter("InstancedViewTranslation"),
                        FShaderParameter("InstancingParameters"),
                    ];
                case "FLensFlareVertexFactory":
                    return
                    [
                        FShaderParameter("CameraRight"),
                        FShaderParameter("CameraUp"),
                        FShaderParameter("LocalToWorld"),
                    ];
                case "FLocalDecalVertexFactory":
                    return
                    [
                        .. FVertexFactoryShaderParameters("FLocalVertexFactory"),
                        FShaderParameter("DecalMatrix"),
                        FShaderParameter("DecalLocation"),
                        FShaderParameter("DecalOffset"),
                        FShaderParameter("DecalLocalBinormal"),
                        FShaderParameter("DecalLocalTangent"),
                        FShaderParameter("DecalLocalNormal"),
                        FShaderParameter("DecalBlendInterval"),
                    ];
                case "FGPUSkinVertexFactoryApex":
                    return
                    [
                        .. FVertexFactoryShaderParameters("FLocalVertexFactory"),
                        FShaderParameter("BoneMatrices"),
                        FShaderParameter("ApexDummy"),
                    ];
                case "FParticleBeamTrailVertexFactory":
                case "FParticleBeamTrailDynamicParameterVertexFactory":
                    return
                    [
                        FShaderParameter("CameraWorldPosition"),
                        FShaderParameter("CameraRight"),
                        FShaderParameter("CameraUp"),
                        FShaderParameter("ScreenAlignment"),
                        FShaderParameter("LocalToWorld"),
                    ];
                case "FParticleInstancedMeshVertexFactory":
                    return
                    [
                        FShaderParameter("InvNumVerticesPerInstance"),
                        FShaderParameter("NumVerticesPerInstance"),
                        FShaderParameter("InstancedPreViewTranslation"),
                    ];
                case "FParticleVertexFactory":
                case "FParticleSubUVVertexFactory":
                case "FParticleDynamicParameterVertexFactory":
                case "FParticleSubUVDynamicParameterVertexFactory":
                    return
                    [
                        FShaderParameter("CameraWorldPosition"),
                        FShaderParameter("CameraRight"),
                        FShaderParameter("CameraUp"),
                        FShaderParameter("ScreenAlignment"),
                        FShaderParameter("LocalToWorld"),
                        FShaderParameter("AxisRotationVectorSourceIndex"),
                        FShaderParameter("AxisRotationVectors"),
                        FShaderParameter("ParticleUpRightResultScalars"),
                        FShaderParameter("NormalsType"),
                        FShaderParameter("NormalsSphereCenter"),
                        FShaderParameter("NormalsCylinderUnitDirection"),
                    ];
                case "FSplineMeshVertexFactory":
                    return
                    [
                        .. FVertexFactoryShaderParameters("FLocalVertexFactory"),
                        FShaderParameter("SplineStartPos"),
                        FShaderParameter("SplineStartTangent"),
                        FShaderParameter("SplineStartRoll"),
                        FShaderParameter("SplineStartScale"),
                        FShaderParameter("SplineStartOffset"),
                        FShaderParameter("SplineEndPos"),
                        FShaderParameter("SplineEndTangent"),
                        FShaderParameter("SplineEndRoll"),
                        FShaderParameter("SplineEndScale"),
                        FShaderParameter("SplineEndOffset"),
                        FShaderParameter("SplineXDir"),
                        FShaderParameter("SmoothInterpRollScale"),
                        FShaderParameter("MeshMinZ"),
                        FShaderParameter("MeshRangeZ"),
                    ];
                case "FTerrainFullMorphDecalVertexFactory":
                case "FTerrainMorphDecalVertexFactory":
                case "FTerrainDecalVertexFactory":
                    return
                    [
                        .. FVertexFactoryShaderParameters("FTerrainVertexFactory"),
                        FShaderParameter("DecalMatrix"),
                        FShaderParameter("DecalLocation"),
                        FShaderParameter("DecalOffset"),
                        FShaderParameter("DecalLocalBinormal"),
                        FShaderParameter("DecalLocalTangent"),
                    ];
                case "FTerrainFullMorphVertexFactory":
                case "FTerrainMorphVertexFactory":
                case "FTerrainVertexFactory":
                    return
                    [
                        FShaderParameter("LocalToWorld"),
                        FShaderParameter("WorldToLocal"),
                        FShaderParameter("LocalToView"),
                        FShaderParameter("TerrainLightmapCoordinateScaleBias"),
                        FShaderParameter("TessellationInterpolation"),
                        FShaderParameter("InvMaxTesselationLevel_ZScale"),
                        FShaderParameter("InvTerrainSize_SectionBase"),
                        FShaderParameter("Unused"),
                        FShaderParameter("TessellationDistanceScale"),
                        FShaderParameter("TessInterpDistanceValues"),
                    ];
            }
            Debugger.Break();
            return Array.Empty<BinInterpNode>();
        }

        BinInterpNode FGammaShaderParameters(string name) // Verified: LE2
        {
            return new BinInterpNode(bin.Position, $"{name}: FGammaShaderParameters")
            {
                Items =
                {
                    FShaderParameter("GammaColorScaleAndInverse"),
                    FShaderParameter("GammaOverlayColor"),
                    FShaderResourceParameter("ColorGradingLUT"),
                    FShaderParameter("RenderTargetExtent"),
                },
            };
        }

        BinInterpNode FColorRemapShaderParameters(string name) // Verified: LE2
        {
            return new BinInterpNode(bin.Position, $"{name}: FColorRemapShaderParameters")
            {
                Items =
                {
                    FShaderParameter("SceneShadowsAndDesaturation"),
                    FShaderParameter("SceneInverseHighLights"),
                    FShaderParameter("SceneMidTones"),
                    FShaderParameter("SceneScaledLuminanceWeights"),
                },
            };
        }

        BinInterpNode FAmbientOcclusionParams(string name)
        {
            return new BinInterpNode(bin.Position, $"{name}: FAmbientOcclusionParams")
            {
                Items =
                {
                    FShaderResourceParameter("AmbientOcclusionTexture"),
                    FShaderResourceParameter("AOHistoryTexture"),
                    FShaderParameter("AOScreenPositionScaleBias"),
                    FShaderParameter("ScreenEdgeLimits"),
                },
            };
        }

        BinInterpNode FLightShaftPixelShaderParameters(string name)
        {
            return new BinInterpNode(bin.Position, $"{name}: FLightShaftPixelShaderParameters")
            {
                Items =
                {
                    FShaderParameter("TextureSpaceBlurOrigin"),
                    FShaderParameter("WorldSpaceBlurOriginAndRadius"),
                    FShaderParameter("SpotAngles"),
                    FShaderParameter("WorldSpaceSpotDirection"),
                    FShaderParameter("WorldSpaceCameraPosition"),
                    FShaderParameter("UVMinMax"),
                    FShaderParameter("AspectRatioAndInvAspectRatio"),
                    FShaderParameter("LightShaftParameters"),
                    FShaderParameter("BloomTintAndThreshold"),
                    FShaderParameter("BloomScreenBlendThreshold"),
                    FShaderParameter("DistanceFade"),
                    FShaderResourceParameter("SourceTexture"),
                    FShaderParameter("OcclusionValueLimit"),
                },
            };
        }

        BinInterpNode FDOFShaderParameters(string name) // Verified LE2
        {
            return new BinInterpNode(bin.Position, $"{name}: FDOFShaderParameters")
            {
                Items =
                {
                    FShaderParameter("PackedParameters"),
                    FShaderParameter("MinMaxBlurClamp"),
                    FShaderResourceParameter("DOFTexture")
                },
            };
        }

        BinInterpNode FHBAOShaderParameters(string name)
        {
            return new BinInterpNode(bin.Position, $"{name}: {nameof(FHBAOShaderParameters)}")
            {
                Items =
                {
                    FShaderParameter("RadiusToScreen"),
                    FShaderParameter("NegInvR2"),
                    FShaderParameter("NDotVBias"),
                    FShaderParameter("AOMultiplier"),
                    FShaderParameter("PowExponent"),
                    FShaderParameter("ProjInfo"),
                    FShaderParameter("BlurSharpness"),
                    FShaderParameter("InvFullResolution"),
                    FShaderParameter("InvQuarterResolution"),
                    FShaderParameter("FullResOffset"),
                    FShaderParameter("QuarterResOffset"),
                },
            };
        }

        BinInterpNode FHeightFogVertexShaderParameters(string name)
        {
            return new BinInterpNode(bin.Position, $"{name}: FHeightFogVertexShaderParameters")
            {
                Items =
                {
                    FShaderParameter("FogDistanceScale"),
                    FShaderParameter("FogExtinctionDistance"),
                    FShaderParameter("FogMinHeight"),
                    FShaderParameter("FogMaxHeight"),
                    FShaderParameter("FogInScattering"),
                    FShaderParameter("FogStartDistance"),
                },
            };
        }

        BinInterpNode FForwardShadowingShaderParameters(string name)
        {
            return new BinInterpNode(bin.Position, $"{name}: FForwardShadowingShaderParameters")
            {
                Items =
                {
                    FShaderParameter("bReceiveDynamicShadows"),
                    FShaderParameter("ScreenToShadowMatrix"),
                    FShaderParameter("ShadowBufferAndTexelSize"),
                    FShaderParameter("ShadowOverrideFactor"),
                    FShaderResourceParameter("ShadowDepthTexture"),
                },
            };
        }

        BinInterpNode FFogVolumeVertexShaderParameters()
        {
            return new BinInterpNode(bin.Position, $"FFogVolumeVertexShaderParameters")
            {
                Items =
                {
                    FShaderParameter("FirstDensityFunction"),
                    FShaderParameter("SecondDensityFunction"),
                    FShaderParameter("StartDistance"),
                    FShaderParameter("FogVolumeBoxMin"),
                    FShaderParameter("FogVolumeBoxMax"),
                    FShaderParameter("ApproxFogColor"),
                },
            };
        }

        void FBranchingPCFProjectionPixelShader()
        {
            node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
            node.Items.Add(FShaderParameter("ScreenToShadowMatrix"));
            node.Items.Add(FShaderParameter("InvRandomAngleTextureSize"));
            node.Items.Add(FShaderResourceParameter("ShadowDepthTexture"));
            node.Items.Add(FShaderResourceParameter("RandomAngleTexture"));
            node.Items.Add(FShaderParameter("RefiningSampleOffsets"));
            node.Items.Add(FShaderParameter("EdgeSampleOffsets"));
            node.Items.Add(FShaderParameter("ShadowBufferSize"));
            node.Items.Add(FShaderParameter("ShadowFadeFraction"));
        }

        void FBranchingPCFModProjectionPixelShader()
        {
            FBranchingPCFProjectionPixelShader();
            node.Items.Add(FShaderParameter("ShadowModulateColorParam"));
            node.Items.Add(FShaderParameter("ScreenToWorldParam"));
            node.Items.Add(FShaderParameter("EmissiveAlphaMaskScale"));
            node.Items.Add(FShaderParameter("UseEmissiveMask"));
        }

        void FPointLightPolicy_ModShadowPixelParamsType()
        {
            node.Items.Add(FShaderParameter("LightPositionParam"));
            node.Items.Add(FShaderParameter("FalloffParameters"));
        }

        void FPointLightPolicy_PixelParametersType()
        {
            node.Items.Add(FShaderParameter("LightColorAndFalloffExponent"));
        }

        void FDirectionalLightPolicy_PixelParametersType()
        {
            node.Items.Add(FShaderParameter("LightColor"));
            node.Items.Add(FShaderParameter("bReceiveDynamicShadows"));
            node.Items.Add(FShaderParameter("bEnableDistanceShadowFading"));
            node.Items.Add(FShaderParameter("DistanceFadeParameters"));
        }

        void FSphericalHarmonicLightPolicy_PixelParametersType()
        {
            node.Items.Add(FShaderParameter("WorldIncidentLighting"));
        }

        void FModShadowVolumePixelShader_Maybe()
        {
            node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
            node.Items.Add(FShaderParameter("ShadowModulateColor"));
            node.Items.Add(FShaderParameter("ScreenToWorld"));
        }

        void FSpotLightPolicy_ModShadowPixelParamsType() // Verified LE2
        {
            node.Items.Add(FShaderParameter("LightPositionParam"));
            node.Items.Add(FShaderParameter("FalloffParameters"));
            node.Items.Add(FShaderParameter("SpotDirectionParam"));
            node.Items.Add(FShaderParameter("SpotAnglesParam"));
        }

        void FSpotLightPolicy_PixelParametersType()
        {
            node.Items.Add(FShaderParameter("SpotAngles"));
            node.Items.Add(FShaderParameter("SpotDirection"));
            node.Items.Add(FShaderParameter("LightColorAndFalloffExponent"));
        }

        void FSpotLightPolicy_VertexParametersType()
        {
            node.Items.Add(FShaderParameter("LightPositionAndInvRadius"));
        }

        void FShadowTexturePolicy_VertexParametersType()
        {
            node.Items.Add(FShaderParameter("LightmapCoordinateScaleBias"));
        }

        void FShadowTexturePolicy_PixelParametersType()
        {
            node.Items.Add(FShaderResourceParameter("ShadowTexture"));
        }

        void FLightMapTexturePolicy_PixelParametersType()
        {
            node.Items.Add(FShaderResourceParameter("LightMapTextures"));
            node.Items.Add(FShaderParameter("LightMapScale"));
        }

        void FCustomLightMapTexturePolicy_PixelParametersType()
        {
            FLightMapTexturePolicy_PixelParametersType();
            node.Items.Add(FShaderParameter("LightMapBias"));
        }

        void FSignedDistanceFieldShadowTexturePolicy_PixelParametersType()
        {
            node.Items.Add(FShaderParameter("DistanceFieldParameters"));
            node.Items.Add(FShaderResourceParameter("ShadowTexture"));
        }

        void FLightMapTexturePolicy_VertexParametersType()
        {
            node.Items.Add(FShaderParameter("LightmapCoordinateScaleBias"));
        }

        void FVertexLightMapPolicy_VertexParametersType()
        {
            node.Items.Add(FShaderParameter("LightMapScale"));
        }

        void FDOFAndBloomBlendPixelShader()
        {
            node.Items.Add(FDOFShaderParameters("DOFParameters"));
            node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
            node.Items.Add(FShaderResourceParameter("BlurredImage"));
            node.Items.Add(FShaderResourceParameter("DOFBlurredNear"));
            node.Items.Add(FShaderResourceParameter("DOFBlurredFar"));
            node.Items.Add(FShaderResourceParameter("BlurredImageSeperateBloom")); // Spelled 'BlurredImageSeperateBloom' in decomp
            node.Items.Add(FShaderParameter("BloomTintAndScreenBlendThreshold"));
            if (CurrentLoadedExport.Game == MEGame.LE2)
            {
                // LE2 7ff7c62126e7
                node.Items.Add(FShaderResourceParameter("SeparateTranslucencyTexture"));
            }
            node.Items.Add(FShaderParameter("InputTextureSize"));
            node.Items.Add(FShaderParameter("DOFKernelParams"));
        }

        BinInterpNode FMotionBlurShaderParameters(string name)
        {
            // Verified LE2
            // 7ff7c65d91a0 LE2
            // 7ff74ac66b50 LE3

            var binInterpNode = new BinInterpNode(bin.Position, $"{name}: FMotionBlurShaderParameters");
            binInterpNode.Items.Add(FShaderResourceParameter("LowResSceneBuffer"));
            binInterpNode.Items.Add(FShaderResourceParameter("VelocityBuffer"));
            binInterpNode.Items.Add(FShaderParameter("ScreenToWorld"));
            binInterpNode.Items.Add(FShaderParameter("PrevViewProjMatrix"));
            binInterpNode.Items.Add(FShaderParameter("StaticVelocityParameters"));
            binInterpNode.Items.Add(FShaderParameter("DynamicVelocityParameters"));
            binInterpNode.Items.Add(FShaderParameter("RenderTargetClampParameter"));
            binInterpNode.Items.Add(FShaderParameter("MotionBlurMaskScaleAndBias"));
            binInterpNode.Items.Add(FShaderParameter("StepOffsetsOpaque"));
            binInterpNode.Items.Add(FShaderParameter("StepWeightsOpaque"));
            binInterpNode.Items.Add(FShaderParameter("StepOffsetsTranslucent"));
            binInterpNode.Items.Add(FShaderParameter("StepWeightsTranslucent"));
            return binInterpNode;
        }

        void FUberPostProcessBlendPixelShader()
        {
            FDOFAndBloomBlendPixelShader();
            node.Items.Add(FColorRemapShaderParameters("MaterialParameters"));
            node.Items.Add(FGammaShaderParameters("GammaParameters"));
            node.Items.Add(FShaderResourceParameter("LowResSceneBuffer"));
            node.Items.Add(FShaderParameter("HalfResMaskRec"));
            node.Items.Add(FMotionBlurShaderParameters("MotionBlurParameters"));
        }

        void TModShadowProjectionPixelShader()
        {
            node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters")); // Verified LE2
            node.Items.Add(FShaderParameter("ScreenToShadowMatrix")); // Rest are also Verified LE2
            node.Items.Add(FShaderResourceParameter("ShadowDepthTexture"));
            node.Items.Add(FShaderResourceParameter("ShadowDepthTextureComparisonSampler"));
            node.Items.Add(FShaderParameter("SampleOffsets"));
            node.Items.Add(FShaderParameter("ShadowBufferSize"));
            node.Items.Add(FShaderParameter("ShadowFadeFraction"));
            node.Items.Add(FShaderParameter("ShadowModulateColor"));
            node.Items.Add(FShaderParameter("ScreenToWorld"));
            node.Items.Add(FShaderParameter("bEmissiveAlphaMaskScale"));
            node.Items.Add(FShaderParameter("bApplyEmissiveToShadowCoverage"));
        }

        void FSHLightLightMapPolicy_PixelParametersType()
        {
            node.Items.Add(FShaderParameter("LightColorAndFalloffExponent"));
            node.Items.Add(FShaderParameter("bReceiveDynamicShadows"));
            node.Items.Add(FShaderParameter("WorldIncidentLighting"));
        }

        void TDOFAndBloomGatherPixelShader()
        {
            node.Items.Add(FDOFShaderParameters("DOFParameters"));
            node.Items.Add(FSceneTextureShaderParameters("SceneTextureParameters"));
            node.Items.Add(FShaderParameter("BloomScaleAndThreshold"));
            node.Items.Add(FShaderParameter("SceneMultiplier"));
            node.Items.Add(FShaderResourceParameter("SmallSceneColorTexture"));
        }

        void FSimpleElementGammaPixelShader()
        {
            FSimpleElementPixelShader();
            node.Items.Add(FShaderParameter("Gamma"));
        }

        void FSimpleElementPixelShader()
        {
            node.Items.Add(FShaderResourceParameter("_Texture"));
            node.Items.Add(FShaderParameter("TextureComponentReplicate"));
            node.Items.Add(FShaderParameter("TextureComponentReplicateAlpha"));
        }

        void FDirectionalLightPolicy_VertexParametersType()
        {
            node.Items.Add(FShaderParameter("LightDirection"));
        }

        void FPointLightPolicy_VertexParametersType()
        {
            node.Items.Add(FShaderParameter("LightPositionAndInvRadius"));
        }

        void FSFXPointLightPolicy_PixelParametersType()
        {
            node.Items.Add(FShaderResourceParameter("LightSpaceShadowMap"));
            node.Items.Add(FShaderParameter("LightColorAndFalloffExponent"));
            node.Items.Add(FShaderParameter("ShadowFilter"));
            node.Items.Add(FShaderParameter("ShadowTextureRegion"));
            node.Items.Add(FShaderParameter("MaxVarianceShadowAttenuation"));
        }

        void FSFXPointLightPolicy_VertexParametersType()
        {
            node.Items.Add(FShaderParameter("LightPositionAndInvRadius"));
            node.Items.Add(FShaderParameter("ShadowViewProjection"));
        }

        void FDirectionalLightLightMapPolicy_VertexParametersType()
        {
            node.Items.Add(FShaderParameter("LightDirectionAndbDirectional"));
        }

        void FDirectionalLightLightMapPolicy_PixelParametersType()
        {
            node.Items.Add(FShaderParameter("LightColorAndFalloffExponent"));
            node.Items.Add(FShaderParameter("bReceiveDynamicShadows"));
        }

        void TBasePassPixelShader()
        {
            node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
            node.Items.Add(FShaderParameter("AmbientColorAndSkyFactor"));
            node.Items.Add(FShaderParameter("UpperSkyColor"));
            node.Items.Add(FShaderParameter("LowerSkyColor"));
            node.Items.Add(FShaderParameter("MotionBlurMask"));
            node.Items.Add(FShaderParameter("CharacterMask"));
            node.Items.Add(FShaderParameter("TranslucencyDepth"));
        }

        void TBasePassVertexShader()
        {
            node.Items.Add(FVertexFactoryParameterRef());
            node.Items.Add(FHeightFogVertexShaderParameters("HeightFogParameters"));
            node.Items.Add(FMaterialVertexShaderParameters("MaterialParameters"));
        }

        void TLightPixelShader()
        {
            node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
            node.Items.Add(FShaderResourceParameter("LightAttenuationTexture"));
            node.Items.Add(FShaderParameter("bReceiveDynamicShadows"));
        }

        void TLightMapDensityPixelShader()
        {
            node.Items.Add(FMaterialPixelShaderParameters("MaterialParameters"));
            node.Items.Add(FShaderParameter("LightMapDensityParameters"));
            node.Items.Add(FShaderParameter("BuiltLightingAndSelectedFlags"));
            node.Items.Add(FShaderParameter("DensitySelectedColor"));
            node.Items.Add(FShaderParameter("LightMapResolutionScale"));
            node.Items.Add(FShaderParameter("LightMapDensityDisplayOptions"));
            node.Items.Add(FShaderParameter("VertexMappedColor"));
            node.Items.Add(FShaderResourceParameter("GridTexture"));
        }

        void TLightVertexShader()
        {
            node.Items.Add(FVertexFactoryParameterRef());
            node.Items.Add(FMaterialVertexShaderParameters("MaterialParameters"));
        }

        void FConstantDensityPolicy_VertexShaderParametersType()
        {
            node.Items.Add(FFogVolumeVertexShaderParameters());
        }
    }

    //For Consoles
    private List<ITreeItem> StartShaderCachePayloadScanStream(ref int binarystart)
    {
        var subnodes = new List<ITreeItem>();
        try
        {
            var export = CurrentLoadedExport; //Prevents losing the reference
            int dataOffset = export.DataOffset;
            var bin = new EndianReader(CurrentLoadedExport.GetReadOnlyDataStream()) { Endian = CurrentLoadedExport.FileRef.Endian };
            bin.JumpTo(binarystart);

            var platformByte = bin.ReadByte();
            if (export.Game.IsLEGame())
            {
                var platform = (EShaderPlatformOT)bin.ReadByte();
                subnodes.Add(new BinInterpNode(bin.Position, $"Platform: {platform}") { Length = 1 });
            }
            else
            {
                var platform = (EShaderPlatformLE)bin.ReadByte();
                subnodes.Add(new BinInterpNode(bin.Position, $"Platform: {platform}") { Length = 1 });
            }

            //if (platform != EShaderPlatform.XBOXDirect3D){
            int mapCount = (Pcc.Game == MEGame.ME3 || Pcc.Game == MEGame.UDK) ? 2 : 1;
            if (platformByte == (byte)EShaderPlatformOT.XBOXDirect3D && !export.Game.IsLEGame()) mapCount = 1;
            var nameMappings = new[] { "CompressedCacheMap", "ShaderTypeCRCMap" };
            while (mapCount > 0)
            {
                mapCount--;
                int vertexMapCount = bin.ReadInt32();
                var mappingNode = new BinInterpNode(bin.Position - 4, $"{nameMappings[mapCount]}, {vertexMapCount} items");
                subnodes.Add(mappingNode);

                for (int i = 0; i < vertexMapCount; i++)
                {
                    NameReference shaderName = bin.ReadNameReference(Pcc);
                    int shaderCRC = bin.ReadInt32();
                    mappingNode.Items.Add(new BinInterpNode(bin.Position - 12, $"CRC:{shaderCRC:X8} {shaderName.Instanced}") { Length = 12 });
                }
            }

            //if (export.FileRef.Platform != MEPackage.GamePlatform.Xenon && export.FileRef.Game == MEGame.ME3)
            //{
            //    subnodes.Add(MakeInt32Node(bin, "PS3/WiiU Count of something??"));
            //}

            //subnodes.Add(MakeInt32Node(bin, "???"));
            //subnodes.Add(MakeInt32Node(bin, "Shader File Count?"));

            int embeddedShaderFileCount = bin.ReadInt32();
            var embeddedShaderCount = new BinInterpNode(bin.Position - 4, $"Embedded Shader File Count: {embeddedShaderFileCount}");
            subnodes.Add(embeddedShaderCount);
            for (int i = 0; i < embeddedShaderFileCount; i++)
            {
                NameReference shaderName = bin.ReadNameReference(Pcc);
                var shaderNode = new BinInterpNode(bin.Position - 8, $"Shader {i} {shaderName.Instanced}");
                try
                {
                    shaderNode.Items.Add(new BinInterpNode(bin.Position - 8, $"Shader Type: {shaderName.Instanced}")
                    { Length = 8 });
                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader GUID {bin.ReadGuid()}")
                    { Length = 16 });
                    if (Pcc.Game == MEGame.UDK)
                    {
                        shaderNode.Items.Add(MakeGuidNode(bin, "2nd Guid?"));
                        shaderNode.Items.Add(MakeUInt32Node(bin, "unk?"));
                    }

                    int shaderEndOffset = bin.ReadInt32();
                    shaderNode.Items.Add(
                        new BinInterpNode(bin.Position - 4, $"Shader End Offset: {shaderEndOffset}") { Length = 4 });

                    if (export.Game.IsLEGame())
                    {
                        shaderNode.Items.Add(
                            new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatformLE)bin.ReadByte()}")
                            { Length = 1 });
                    }
                    else
                    {
                        shaderNode.Items.Add(
                            new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatformOT)bin.ReadByte()}")
                            { Length = 1 });
                    }

                    shaderNode.Items.Add(new BinInterpNode(bin.Position,
                            $"Frequency: {(EShaderFrequency)bin.ReadByte()}")
                    { Length = 1 });

                    int shaderSize = bin.ReadInt32();
                    shaderNode.Items.Add(new BinInterpNode(bin.Position - 4, $"Shader File Size: {shaderSize}")
                    { Length = 4 });

                    shaderNode.Items.Add(new BinInterpNode(bin.Position, "Shader File") { Length = shaderSize });
                    bin.Skip(shaderSize);

                    shaderNode.Items.Add(MakeInt32Node(bin, "ParameterMap CRC"));

                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader End GUID: {bin.ReadGuid()}") { Length = 16 });

                    shaderNode.Items.Add(
                        new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });


                    shaderNode.Items.Add(MakeInt32Node(bin, "Number of Instructions"));

                    shaderNode.Items.Add(
                        new BinInterpNode(bin.Position,
                                $"Unknown shader bytes ({shaderEndOffset - (dataOffset + bin.Position)} bytes)")
                        { Length = (int)(shaderEndOffset - (dataOffset + bin.Position)) });

                    embeddedShaderCount.Items.Add(shaderNode);

                    bin.JumpTo(shaderEndOffset - dataOffset);
                }
                catch (Exception)
                {
                    embeddedShaderCount.Items.Add(shaderNode);
                    break;
                }
            }

            /*
                int mapCount = Pcc.Game >= MEGame.ME3 ? 2 : 1;
                for (; mapCount > 0; mapCount--)
                {
                    int vertexMapCount = bin.ReadInt32();
                    var mappingNode = new BinInterpNode(bin.Position - 4, $"Name Mapping {mapCount}, {vertexMapCount} items");
                    subnodes.Add(mappingNode);

                    for (int i = 0; i < vertexMapCount; i++)
                    {
                        NameReference shaderName = bin.ReadNameReference(Pcc);
                        int shaderCRC = bin.ReadInt32();
                        mappingNode.Items.Add(new BinInterpNode(bin.Position - 12, $"CRC:{shaderCRC:X8} {shaderName.Instanced}") { Length = 12 });
                    }
                }

                if (Pcc.Game == MEGame.ME1)
                {
                    ReadVertexFactoryMap();
                }

                int embeddedShaderFileCount = bin.ReadInt32();
                var embeddedShaderCount = new BinInterpNode(bin.Position - 4, $"Embedded Shader File Count: {embeddedShaderFileCount}");
                subnodes.Add(embeddedShaderCount);
                for (int i = 0; i < embeddedShaderFileCount; i++)
                {
                    NameReference shaderName = bin.ReadNameReference(Pcc);
                    var shaderNode = new BinInterpNode(bin.Position - 8, $"Shader {i} {shaderName.Instanced}");

                    shaderNode.Items.Add(new BinInterpNode(bin.Position - 8, $"Shader Type: {shaderName.Instanced}") { Length = 8 });
                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader GUID {bin.ReadGuid()}") { Length = 16 });
                    if (Pcc.Game == MEGame.UDK)
                    {
                        shaderNode.Items.Add(MakeGuidNode(bin, "2nd Guid?"));
                        shaderNode.Items.Add(MakeUInt32Node(bin, "unk?"));
                    }
                    int shaderEndOffset = bin.ReadInt32();
                    shaderNode.Items.Add(new BinInterpNode(bin.Position - 4, $"Shader End Offset: {shaderEndOffset}") { Length = 4 });


                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatform)bin.ReadByte()}") { Length = 1 });
                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Frequency: {(EShaderFrequency)bin.ReadByte()}") { Length = 1 });

                    int shaderSize = bin.ReadInt32();
                    shaderNode.Items.Add(new BinInterpNode(bin.Position - 4, $"Shader File Size: {shaderSize}") { Length = 4 });

                    shaderNode.Items.Add(new BinInterpNode(bin.Position, "Shader File") { Length = shaderSize });
                    bin.Skip(shaderSize);

                    shaderNode.Items.Add(MakeInt32Node(bin, "ParameterMap CRC"));

                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader End GUID: {bin.ReadGuid()}") { Length = 16 });

                    shaderNode.Items.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });

                    shaderNode.Items.Add(MakeInt32Node(bin, "Number of Instructions"));

                    embeddedShaderCount.Items.Add(shaderNode);

                    bin.JumpTo(shaderEndOffset - dataOffset);
                }

                void ReadVertexFactoryMap()
                {
                    int vertexFactoryMapCount = bin.ReadInt32();
                    var factoryMapNode = new BinInterpNode(bin.Position - 4, $"Vertex Factory Name Mapping, {vertexFactoryMapCount} items");
                    subnodes.Add(factoryMapNode);

                    for (int i = 0; i < vertexFactoryMapCount; i++)
                    {
                        NameReference shaderName = bin.ReadNameReference(Pcc);
                        int shaderCRC = bin.ReadInt32();
                        factoryMapNode.Items.Add(new BinInterpNode(bin.Position - 12, $"{shaderCRC:X8} {shaderName.Instanced}") { Length = 12 });
                    }
                }
                if (Pcc.Game == MEGame.ME2 || Pcc.Game == MEGame.ME3)
                {
                    ReadVertexFactoryMap();
                }

                int materialShaderMapcount = bin.ReadInt32();
                var materialShaderMaps = new BinInterpNode(bin.Position - 4, $"Material Shader Maps, {materialShaderMapcount} items");
                subnodes.Add(materialShaderMaps);
                for (int i = 0; i < materialShaderMapcount; i++)
                {
                    var nodes = new List<ITreeItem>();
                    materialShaderMaps.Items.Add(new BinInterpNode(bin.Position, $"Material Shader Map {i}") { Items = nodes });
                    nodes.Add(ReadFStaticParameterSet(bin));

                    if (Pcc.Game >= MEGame.ME3)
                    {
                        nodes.Add(new BinInterpNode(bin.Position, $"Unreal Version {bin.ReadInt32()}") { Length = 4 });
                        nodes.Add(new BinInterpNode(bin.Position, $"Licensee Version {bin.ReadInt32()}") { Length = 4 });
                    }

                    int shaderMapEndOffset = bin.ReadInt32();
                    nodes.Add(new BinInterpNode(bin.Position - 4, $"Material Shader Map end offset {shaderMapEndOffset}") { Length = 4 });

                    int unkCount = bin.ReadInt32();
                    var unkNodes = new List<ITreeItem>();
                    nodes.Add(new BinInterpNode(bin.Position - 4, $"Shaders {unkCount}") { Length = 4, Items = unkNodes });
                    for (int j = 0; j < unkCount; j++)
                    {
                        unkNodes.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc).Instanced}") { Length = 8 });
                        unkNodes.Add(new BinInterpNode(bin.Position, $"GUID: {bin.ReadGuid()}") { Length = 16 });
                        unkNodes.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc).Instanced}") { Length = 8 });
                    }

                    int meshShaderMapsCount = bin.ReadInt32();
                    var meshShaderMaps = new BinInterpNode(bin.Position - 4, $"Mesh Shader Maps, {meshShaderMapsCount} items") { Length = 4 };
                    nodes.Add(meshShaderMaps);
                    for (int j = 0; j < meshShaderMapsCount; j++)
                    {
                        var nodes2 = new List<ITreeItem>();
                        meshShaderMaps.Items.Add(new BinInterpNode(bin.Position, $"Mesh Shader Map {j}") { Items = nodes2 });

                        int shaderCount = bin.ReadInt32();
                        var shaders = new BinInterpNode(bin.Position - 4, $"Shaders, {shaderCount} items") { Length = 4 };
                        nodes2.Add(shaders);
                        for (int k = 0; k < shaderCount; k++)
                        {
                            var nodes3 = new List<ITreeItem>();
                            shaders.Items.Add(new BinInterpNode(bin.Position, $"Shader {k}") { Items = nodes3 });

                            nodes3.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                            nodes3.Add(new BinInterpNode(bin.Position, $"GUID: {bin.ReadGuid()}") { Length = 16 });
                            nodes3.Add(new BinInterpNode(bin.Position, $"Shader Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                        }
                        nodes2.Add(new BinInterpNode(bin.Position, $"Vertex Factory Type: {bin.ReadNameReference(Pcc)}") { Length = 8 });
                        if (Pcc.Game == MEGame.ME1)
                        {
                            nodes2.Add(MakeUInt32Node(bin, "Unk"));
                        }
                    }

                    nodes.Add(new BinInterpNode(bin.Position, $"MaterialId: {bin.ReadGuid()}") { Length = 16 });

                    nodes.Add(MakeStringNode(bin, "Friendly Name"));

                    nodes.Add(ReadFStaticParameterSet(bin));

                    if (Pcc.Game >= MEGame.ME3)
                    {
                        string[] uniformExpressionArrays =
                        {
                            "UniformPixelVectorExpressions",
                            "UniformPixelScalarExpressions",
                            "Uniform2DTextureExpressions",
                            "UniformCubeTextureExpressions",
                            "UniformVertexVectorExpressions",
                            "UniformVertexScalarExpressions"
                        };

                        foreach (string uniformExpressionArrayName in uniformExpressionArrays)
                        {
                            int expressionCount = bin.ReadInt32();
                            nodes.Add(new BinInterpNode(bin.Position - 4, $"{uniformExpressionArrayName}, {expressionCount} expressions")
                            {
                                Items = ReadList(expressionCount, x => ReadMaterialUniformExpression(bin))
                            });
                        }
                        nodes.Add(new BinInterpNode(bin.Position, $"Platform: {(EShaderPlatform)bin.ReadInt32()}") { Length = 4 });
                    }

                    bin.JumpTo(shaderMapEndOffset - dataOffset);
                }

                int numShaderCachePayloads = bin.ReadInt32();
                var shaderCachePayloads = new BinInterpNode(bin.Position - 4, $"Shader Cache Payloads, {numShaderCachePayloads} items");
                subnodes.Add(shaderCachePayloads);
                for (int i = 0; i < numShaderCachePayloads; i++)
                {
                    shaderCachePayloads.Items.Add(MakeEntryNode(bin, $"Payload {i}"));
                } */
        }
        catch (Exception ex)
        {
            subnodes.Add(new BinInterpNode { Header = $"Error reading binary data: {ex}" });
        }

        return subnodes;
    }

    private enum EShaderPlatformOT : byte
    {
        PCDirect3D_ShaderModel3 = 0,
        PS3 = 1,
        XBOXDirect3D = 2,
        PCDirect3D_ShaderModel4 = 3,
        PCDirect3D_ShaderModel5 = 4, // UDK?
        WiiU = 5 // unless its LE then it's SM5!
    }

    private enum EShaderPlatformLE : byte
    {
        SM_SM3 = 0,
        SM_PS3 = 1,
        SM_360 = 2,
        SM_SM2 = 3,
        SM_SM4 = 4,
        SM_SM5 = 5,
        SM_Dingo = 6,
        SM_Orbis = 7,
    }

    private enum EShaderFrequency : byte
    {
        Vertex = 0,
        Pixel = 1,
        PixelUDK = 3, // This is a hack
    }
}