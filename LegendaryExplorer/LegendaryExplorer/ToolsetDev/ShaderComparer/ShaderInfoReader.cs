using DocumentFormat.OpenXml.Wordprocessing;
using SharpDX.D3DCompiler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using TerraFX.Interop.Windows;

namespace LegendaryExplorer.ToolsetDev.ShaderComparer;

/// <summary>
/// Pure C# implementation of shader info extraction via D3D11 shader reflection,
/// replacing the native GetShaderInfo P/Invoke call.
/// </summary>
public static class ShaderInfoReader
{
    public class ResourceBindingEntry
    {
        public string Name { get; set; } = "";
        public ushort BufferIndex { get; set; }
        public ushort BaseIndex { get; set; }
        public ushort Size { get; set; }
        public string SamplerIndex { get; set; } = "";
    }

    /// <summary>
    /// Checks whether a sampler resource name corresponds to a texture resource name.
    /// UE3 SM5 shaders use the convention: Texture2D ParamName, SamplerState ParamNameSampler.
    /// Array brackets are stripped before comparison.
    /// </summary>
    private static bool SamplerMatchesTexture(string samplerName, string textureName)
    {
        int idx = samplerName.IndexOf('[');
        if (idx >= 0) samplerName = samplerName[..idx];

        idx = textureName.IndexOf('[');
        if (idx >= 0) textureName = textureName[..idx];

        // Exact match (both share the same name)
        if (string.Equals(samplerName, textureName, StringComparison.Ordinal))
            return true;

        // UE3 SM5 convention: sampler is named "{TextureName}Sampler"
        if (samplerName.EndsWith("Sampler", StringComparison.Ordinal) &&
            samplerName.AsSpan()[..^"Sampler".Length].Equals(textureName.AsSpan(), StringComparison.Ordinal))
            return true;

        return false;
    }

    public static List<ResourceBindingEntry> GetShaderInfo(byte[] shaderBytes, out int instructionCount)
    {
        Debug.WriteLine("============");
        instructionCount = 0;
        var parameterMap = new List<ResourceBindingEntry>();

        using var reflection = new ShaderReflection(shaderBytes);
        var shaderDesc = reflection.Description;

        instructionCount = shaderDesc.InstructionCount;

        for (int resourceIndex = 0; resourceIndex < shaderDesc.BoundResources; resourceIndex++)
        {
            var bindDesc = reflection.GetResourceBindingDescription(resourceIndex);
            Debug.WriteLine($"{bindDesc.Type} {bindDesc.Name}");
            // Constant buffer / Texture Buffer
            if (bindDesc.Type == ShaderInputType.ConstantBuffer || bindDesc.Type == ShaderInputType.TextureBuffer)
            {
                ushort constantBufferIndex = (ushort)bindDesc.BindPoint;
                var constantBuffer = reflection.GetConstantBuffer(bindDesc.Name);
                var cbDesc = constantBuffer.Description;

                for (int cIndex = 0; cIndex < cbDesc.VariableCount; cIndex++)
                {
                    var variable = constantBuffer.GetVariable(cIndex);
                    var varDesc = variable.Description;
                    var used = (varDesc.Flags & ShaderVariableFlags.Used) != 0;
                    // Debug.WriteLine($"ConstantBufferVar {varDesc.Name} | {(used ? ">>Used<<" : "Unused")}");
                    if (used)
                    {
                        parameterMap.Add(new ResourceBindingEntry
                        {
                            Name = varDesc.Name,
                            BufferIndex = constantBufferIndex,
                            BaseIndex = (ushort)varDesc.StartOffset,
                            Size = (ushort)varDesc.Size,
                            SamplerIndex = "0",
                        });
                    }
                }
            }
            // Texture Resource
            else if (bindDesc.Type == ShaderInputType.Texture)
            {
                // Check if this texture has a matching sampler (exact name or with "Sampler" suffix)
                bool hasSampler = false;
                for (int i = 0; i < shaderDesc.BoundResources; i++)
                {
                    // Enumerate bound resources.
                    var otherDesc = reflection.GetResourceBindingDescription(i);
                    if (otherDesc.Type == ShaderInputType.Sampler)
                    {
                        Debug.WriteLine($"\tSampler test: {otherDesc.Name} = {bindDesc.Name}");
                        if (SamplerMatchesTexture(otherDesc.Name, bindDesc.Name))
                        {
                            /*
                            string name = bindDesc.Name;
                            int samplerBindingPoint = otherDesc.BindPoint;
                            int textureBindingPoint = bindDesc.BindPoint;
                            int actualNumBinds = 0;

                            parameterMap.Add(new ResourceBindingEntry
                            {
                                Name = name,
                                BufferIndex = 0,
                                BaseIndex = (ushort)textureBindingPoint,
                                Size = (ushort)actualNumBinds,
                                SamplerIndex = ((ushort)samplerBindingPoint).ToString(),
                            });*/

                            Debug.WriteLine($"\t\tSampler match - nothing to else to do here!");
                            hasSampler = true;
                            break;
                        }
                    }
                }

                if (!hasSampler)
                {
                    string name = bindDesc.Name;
                    ushort numTimesBound = 1;

                    int bracketPos = name.IndexOf('[');
                    if (bracketPos >= 0)
                    {
                        name = name[..bracketPos];
                        while (resourceIndex + 1 < shaderDesc.BoundResources)
                        {
                            var nextDesc = reflection.GetResourceBindingDescription(resourceIndex + 1);
                            if (nextDesc.Type == ShaderInputType.Texture &&
                                string.Compare(nextDesc.Name, 0, bindDesc.Name, 0, bracketPos, StringComparison.Ordinal) == 0)
                            {
                                numTimesBound++;
                                resourceIndex++;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    parameterMap.Add(new ResourceBindingEntry
                    {
                        Name = name,
                        BufferIndex = 0,
                        BaseIndex = (ushort)bindDesc.BindPoint,
                        Size = numTimesBound,
                        SamplerIndex = "N/A",
                    });
                }
            }
            // UAV Read-Write Typed
            else if (bindDesc.Type == ShaderInputType.UnorderedAccessViewRWTyped)
            {
                parameterMap.Add(new ResourceBindingEntry
                {
                    Name = bindDesc.Name,
                    BufferIndex = 0,
                    BaseIndex = (ushort)bindDesc.BindPoint,
                    Size = 1,
                    SamplerIndex = "N/A",
                });
            }
            // Sampler
            else if (bindDesc.Type == ShaderInputType.Sampler)
            {
                bool foundTexture = false;
                for (int i = 0; i < shaderDesc.BoundResources; i++)
                {
                    var textureDesc = reflection.GetResourceBindingDescription(i);
                    if (textureDesc.Type != ShaderInputType.Texture ||
                        !SamplerMatchesTexture(bindDesc.Name, textureDesc.Name))
                    {
                        continue;
                    }

                    foundTexture = true;

                    // Use the texture name as the UE3 parameter name
                    string name = textureDesc.Name;
                    int samplerBindingPoint = bindDesc.BindPoint;
                    int textureBindingPoint = textureDesc.BindPoint;
                    int actualNumBinds = 0;

                    // Strip []
                    int bracketPos = name.IndexOf('[');
                    if (bracketPos >= 0)
                        name = name[..bracketPos];

                    int resourceOffset = 0;
                    while (i + resourceOffset < shaderDesc.BoundResources &&
                           resourceIndex + resourceOffset < shaderDesc.BoundResources)
                    {
                        //var iterTextureDesc = reflection.GetResourceBindingDescription(i + resourceOffset);
                        var iterSampleDesc = reflection.GetResourceBindingDescription(resourceIndex + resourceOffset);

                        if (//iterTextureDesc.Type != ShaderInputType.Texture ||
                            iterSampleDesc.Type != ShaderInputType.Sampler ||
                            //!SamplerMatchesTexture(iterSampleDesc.Name, iterTextureDesc.Name))
                            !SamplerMatchesTexture(iterSampleDesc.Name, name))
                        {
            break;
                        }

                        //textureBindingPoint = Math.Min(textureBindingPoint, iterTextureDesc.BindPoint);
                        samplerBindingPoint = Math.Min(samplerBindingPoint, iterSampleDesc.BindPoint);
                        actualNumBinds++;
                        resourceOffset++;
                    }

                    // Skip past the other samplers in this array
                    resourceIndex += actualNumBinds - 1;

                    parameterMap.Add(new ResourceBindingEntry
                    {
                        Name = name,
                        BufferIndex = 0,
                        BaseIndex = (ushort)textureBindingPoint,
                        Size = (ushort)actualNumBinds,
                        SamplerIndex = ((ushort)samplerBindingPoint).ToString(),
                    });
                    break;
                }

                if (!foundTexture)
                {
                    // Sampler only — derive UE3 parameter name by stripping "Sampler" suffix
                    string name = bindDesc.Name;
                    if (name.EndsWith("Sampler", StringComparison.Ordinal))
                        name = name[..^"Sampler".Length];

                    parameterMap.Add(new ResourceBindingEntry
                    {
                        Name = name,
                        BufferIndex = 0,
                        BaseIndex = ushort.MaxValue,
                        Size = (ushort)bindDesc.BindCount,
                        SamplerIndex = ((ushort)bindDesc.BindPoint).ToString(),
                    });
                }
            }
        }

        return parameterMap;
    }
}
