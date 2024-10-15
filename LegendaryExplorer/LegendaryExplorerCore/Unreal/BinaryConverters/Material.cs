using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class Material : ObjectBinary
    {
        public MaterialResource SM3MaterialResource;
        public MaterialResource SM2MaterialResource;
        protected override void Serialize(SerializingContainer sc)
        {
            if (sc.Game == MEGame.UDK)
            {
                int numResources = 1;
                sc.Serialize(ref numResources);
            }
            sc.Serialize(ref SM3MaterialResource);
            if (sc.Game != MEGame.UDK)
            {
                if (sc.IsSaving && SM2MaterialResource == null)
                {
                    // Can happen when converting UDK -> ME
                    SM2MaterialResource = MaterialResource.Create();
                }
                sc.Serialize(ref SM2MaterialResource);
            }
        }



        public static Material Create()
        {
            return new()
            {
                SM3MaterialResource = MaterialResource.Create(),
                SM2MaterialResource = MaterialResource.Create()
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(SM3MaterialResource.GetNames(game));
            if (game != MEGame.UDK)
            {
                names.AddRange(SM2MaterialResource.GetNames(game));
            }

            return names;
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            SM3MaterialResource.ForEachUIndex(game, action, "SM3MaterialResource.");
            if (game is not MEGame.UDK)
            {
                SM2MaterialResource.ForEachUIndex(game, action, "SM2MaterialResource.");
            }
        }

        public void JsonSerialize(Stream outStream)
        {
            if (Export?.FileRef is null)
            {
                throw new Exception($"Cannot serialize to JSON a {nameof(Material)} that was not constructed from an {nameof(ExportEntry)}.");
            }
            JsonSerializer.Serialize(outStream, this, LEXJSONState.CreateSerializerOptions(Export.FileRef, options: new JsonSerializerOptions
            {
                Converters =
                {
                    new MaterialResource.MaterialResourceJsonConverter()
                }
            }));
        }

        public static Material JsonDeserialize(Stream inStream, IMEPackage pcc, Func<IMEPackage, string, IEntry> missingObjectResolver = null)
        {
            return JsonSerializer.Deserialize<Material>(inStream, LEXJSONState.CreateSerializerOptions(pcc, missingObjectResolver, new JsonSerializerOptions
            {
                Converters =
                {
                    new MaterialResource.MaterialResourceJsonConverter()
                }
            }));
        }
    }
    public class MaterialInstance : ObjectBinary
    {
        public MaterialResource SM3StaticPermutationResource;
        public StaticParameterSet SM3StaticParameterSet; //Not SM3 in LE... not sure what to call the variable though
        public MaterialResource SM2StaticPermutationResource;
        public StaticParameterSet SM2StaticParameterSet;
        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref SM3StaticPermutationResource);
            sc.Serialize(ref SM3StaticParameterSet);
            if (sc.Game != MEGame.UDK)
            {
                sc.Serialize(ref SM2StaticPermutationResource);
                sc.Serialize(ref SM2StaticParameterSet);
            }
        }

        public static MaterialInstance Create()
        {
            return new()
            {
                SM3StaticPermutationResource = MaterialResource.Create(),
                SM3StaticParameterSet = (StaticParameterSet)Guid.Empty,
                SM2StaticPermutationResource = MaterialResource.Create(),
                SM2StaticParameterSet = (StaticParameterSet)Guid.Empty
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(SM3StaticPermutationResource.GetNames(game));
            names.AddRange(SM3StaticParameterSet.GetNames(game));
            if (game != MEGame.UDK)
            {
                names.AddRange(SM2StaticPermutationResource.GetNames(game));
                names.AddRange(SM2StaticParameterSet.GetNames(game, "ShaderModel2StaticParameterSet."));
            }

            return names;
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            SM3StaticPermutationResource.ForEachUIndex(game, action, "SM3StaticPermutationResource.");
            if (game is not MEGame.UDK)
            {
                SM2StaticPermutationResource.ForEachUIndex(game, action, "SM2StaticPermutationResource.");
            }
        }
    }

    //structs

    public class MaterialResource
    {
        public class TextureLookup
        {
            public int TexCoordIndex;
            public int TextureIndex;
            public float UScale;
            public float VScale;
            public uint Unk; //LE only
        }

        public string[] CompileErrors;
        public UMultiMap<UIndex, int> TextureDependencyLengthMap;  //TODO: Make this a UMap
        public int MaxTextureDependencyLength;
        public Guid ID;
        public uint NumUserTexCoords;
        public UIndex[] UniformExpressionTextures; //serialized for ME3/LE, but will be set here for ME1 and ME2 as well
        //begin ME1/ME2
        public MaterialUniformExpression[] UniformPixelVectorExpressions;
        public MaterialUniformExpression[] UniformPixelScalarExpressions;
        public MaterialUniformExpressionTexture[] Uniform2DTextureExpressions;
        public MaterialUniformExpressionTexture[] UniformCubeTextureExpressions;
        //end ME1/ME2
        public bool bUsesSceneColor;
        public bool bUsesSceneDepth;
        //begin >= ME3
        public bool bUsesDynamicParameter;
        public bool bUsesLightmapUVs;
        public bool bUsesMaterialVertexPositionOffset;
        public bool unkBool1;
        //end >= ME3
        public uint UsingTransforms; //ECoordTransformUsage
        public TextureLookup[] TextureLookups; //not ME1
        public uint DummyDroppedFallbackComponents;
        //begin ME1
        public ME1MaterialUniformExpressionsElement[] Me1MaterialUniformExpressionsList;
        public int unk1;
        public int unkCount
        {
            get => unkList?.Length ?? 0;
            set => Array.Resize(ref unkList, value);
        }

        // UDK ONLY ----------------
        public uint BlendModeOverrideValue;
        public bool bIsMaskOverrideValue; // BOOL
        public bool bIsBlendModeOverrided; // BOOL
        // END UDK ONLY ============

        public int unkInt2;
        public (int, float, int)[] unkList;

        
        //end ME1

        public static MaterialResource Create()
        {
            return new()
            {
                CompileErrors = [],
                TextureDependencyLengthMap = [],
                UniformExpressionTextures = [],
                UniformPixelVectorExpressions = [],
                UniformPixelScalarExpressions = [],
                Uniform2DTextureExpressions = [],
                UniformCubeTextureExpressions = [],
                TextureLookups = [],
                Me1MaterialUniformExpressionsList = [],
                unkList = [],
            };
        }

        public List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            if (game <= MEGame.ME2)
            {
                var uniformExpressionArrays = new List<(string, MaterialUniformExpression[])>
                {
                    (nameof(UniformPixelVectorExpressions), UniformPixelVectorExpressions),
                    (nameof(UniformPixelScalarExpressions), UniformPixelScalarExpressions),
                    (nameof(Uniform2DTextureExpressions), Uniform2DTextureExpressions),
                    (nameof(UniformCubeTextureExpressions), UniformCubeTextureExpressions),
                };
                if (game == MEGame.ME1)
                {
                    int j = 0;
                    foreach (ME1MaterialUniformExpressionsElement expressionsElement in Me1MaterialUniformExpressionsList)
                    {
                        uniformExpressionArrays.Add(($"MaterialUniformExpressions[{j}].UniformPixelVectorExpressions", expressionsElement.UniformPixelVectorExpressions));
                        uniformExpressionArrays.Add(($"MaterialUniformExpressions[{j}].UniformPixelScalarExpressions", expressionsElement.UniformPixelScalarExpressions));
                        uniformExpressionArrays.Add(($"MaterialUniformExpressions[{j}].Uniform2DTextureExpressions", expressionsElement.Uniform2DTextureExpressions));
                        uniformExpressionArrays.Add(($"MaterialUniformExpressions[{j}].UniformCubeTextureExpressions", expressionsElement.UniformCubeTextureExpressions));
                        ++j;
                    }
                }

                foreach ((string prefix, MaterialUniformExpression[] expressions) in uniformExpressionArrays)
                {
                    for (int i = 0; i < expressions.Length; i++)
                    {
                        MaterialUniformExpression expression = expressions[i];
                        names.Add((expression.ExpressionType, $"{prefix}[{i}].ExpressionType"));
                        switch (expression)
                        {
                            case MaterialUniformExpressionTextureParameter texParamExpression:
                                names.Add((texParamExpression.ParameterName, $"{prefix}[{i}].ParameterName"));
                                break;
                            case MaterialUniformExpressionScalarParameter scalarParameterExpression:
                                names.Add((scalarParameterExpression.ParameterName, $"{prefix}[{i}].ParameterName"));
                                break;
                            case MaterialUniformExpressionVectorParameter vecParameterExpression:
                                names.Add((vecParameterExpression.ParameterName, $"{prefix}[{i}].ParameterName"));
                                break;
                        }

                        names.AddRange(expression.GetNames(game));
                    }
                }
            }

            return names;
        }
        public virtual void ForEachUIndex<TAction>(MEGame game, in TAction action, string prefix) where TAction : struct, IUIndexAction
        {
            ObjectBinary.ForEachUIndexKeyInMultiMap(action, TextureDependencyLengthMap, nameof(TextureDependencyLengthMap));
            if (game >= MEGame.ME3)
            {
                ObjectBinary.ForEachUIndexInSpan(action, UniformExpressionTextures.AsSpan(), $"{prefix}{nameof(UniformExpressionTextures)}");
            }
            else
            {
                //seperating this monstrosity out so the poor jit can ignore it in the common case (LE)
                NonLE_UIndexes(game, action, prefix);
            }
        }
        private void NonLE_UIndexes<TAction>(MEGame meGame, TAction uIndexAction, string prefix) where TAction : struct, IUIndexAction
        {
            TAction a = Unsafe.AsRef(in uIndexAction);
            for (int i = 0; i < UniformPixelVectorExpressions.Length; i++)
            {
                switch (UniformPixelVectorExpressions[i])
                {
                    case MaterialUniformExpressionFlipbookParameter flipParm:
                        a.Invoke(ref flipParm.TextureIndex, $"{prefix}UniformPixelVectorExpressions[{i}]");
                        break;
                    case MaterialUniformExpressionTexture parm:
                        a.Invoke(ref parm.TextureIndex, $"{prefix}UniformPixelVectorExpressions[{i}]");
                        break;
                }
            }
            for (int i = 0; i < UniformPixelScalarExpressions.Length; i++)
            {
                switch (UniformPixelScalarExpressions[i])
                {
                    case MaterialUniformExpressionFlipbookParameter flipParm:
                        a.Invoke(ref flipParm.TextureIndex, $"{prefix}UniformPixelScalarExpressions[{i}]");
                        break;
                    case MaterialUniformExpressionTexture parm:
                        a.Invoke(ref parm.TextureIndex, $"{prefix}UniformPixelScalarExpressions[{i}]");
                        break;
                }
            }
            for (int i = 0; i < Uniform2DTextureExpressions.Length; i++)
            {
                a.Invoke(ref Uniform2DTextureExpressions[i].TextureIndex, $"{prefix}Uniform2DTextureExpressions[{i}]");
            }
            for (int i = 0; i < UniformCubeTextureExpressions.Length; i++)
            {
                a.Invoke(ref UniformCubeTextureExpressions[i].TextureIndex, $"{prefix}UniformCubeTextureExpressions[{i}]");
            }
            if (meGame is MEGame.ME1)
            {
                for (int j = 0; j < Me1MaterialUniformExpressionsList.Length; j++)
                {
                    ME1MaterialUniformExpressionsElement expressionsElement = Me1MaterialUniformExpressionsList[j];
                    for (int i = 0; i < expressionsElement.UniformPixelVectorExpressions.Length; i++)
                    {
                        switch (expressionsElement.UniformPixelVectorExpressions[i])
                        {
                            case MaterialUniformExpressionFlipbookParameter flipParm:
                                a.Invoke(ref flipParm.TextureIndex, $"{prefix}MaterialUniformExpressions[{j}].UniformPixelVectorExpressions[{i}]");
                                break;
                            case MaterialUniformExpressionTexture parm:
                                a.Invoke(ref parm.TextureIndex, $"{prefix}MaterialUniformExpressions[{j}].UniformPixelVectorExpressions[{i}]");
                                break;
                        }
                    }
                    for (int i = 0; i < expressionsElement.UniformPixelScalarExpressions.Length; i++)
                    {
                        switch (expressionsElement.UniformPixelScalarExpressions[i])
                        {
                            case MaterialUniformExpressionFlipbookParameter flipParm:
                                a.Invoke(ref flipParm.TextureIndex, $"{prefix}MaterialUniformExpressions[{j}].UniformPixelScalarExpressions[{i}]");
                                break;
                            case MaterialUniformExpressionTexture parm:
                                a.Invoke(ref parm.TextureIndex, $"{prefix}MaterialUniformExpressions[{j}].UniformPixelScalarExpressions[{i}]");
                                break;
                        }
                    }
                    for (int i = 0; i < expressionsElement.Uniform2DTextureExpressions.Length; i++)
                    {
                        a.Invoke(ref expressionsElement.Uniform2DTextureExpressions[i].TextureIndex, $"{prefix}MaterialUniformExpressions[{j}].Uniform2DTextureExpressions[{i}]");
                    }
                    for (int i = 0; i < expressionsElement.UniformCubeTextureExpressions.Length; i++)
                    {
                        a.Invoke(ref expressionsElement.UniformCubeTextureExpressions[i].TextureIndex, $"{prefix}MaterialUniformExpressions[{j}].UniformCubeTextureExpressions[{i}]");
                    }
                }
            }
        }

        internal class MaterialResourceJsonConverter : JsonConverter<MaterialResource>
        {
            public override MaterialResource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (!options.TryGetState(out LEXJSONState state))
                {
                    throw new JsonException($"Could not retrieve {nameof(LEXJSONState)} for this serialization!");
                }
                MaterialResource mat = Create();
                reader.Expect(JsonTokenType.StartObject);

                mat.CompileErrors = [.. reader.ReadList(nameof(CompileErrors), (ref Utf8JsonReader reader) => reader.GetString())];
                reader.ExpectPropertyName(nameof(TextureDependencyLengthMap));
                reader.Read();
                reader.Expect(JsonTokenType.StartObject);
                while (reader.Read())
                {
                    if (reader.TokenType is JsonTokenType.EndObject)
                    {
                        break;
                    }
                    reader.Expect(JsonTokenType.PropertyName);
                    int key = state.PathToUIndex(reader.GetString());
                    reader.Read();
                    int val = reader.GetInt32();
                    mat.TextureDependencyLengthMap.Add(key, val);
                }
                reader.ReadNumProp(out mat.MaxTextureDependencyLength, nameof(MaxTextureDependencyLength));
                mat.ID = reader.ReadGuidProp(nameof(ID));
                reader.ReadNumProp(out mat.NumUserTexCoords, nameof(NumUserTexCoords));
                mat.UniformExpressionTextures = [.. reader.ReadList(nameof(UniformExpressionTextures), state.ReadEntryValue)];
                mat.bUsesSceneColor = reader.ReadBoolProp(nameof(bUsesSceneColor));
                mat.bUsesSceneDepth = reader.ReadBoolProp(nameof(bUsesSceneDepth));
                mat.bUsesDynamicParameter = reader.ReadBoolProp(nameof(bUsesDynamicParameter));
                mat.bUsesLightmapUVs = reader.ReadBoolProp(nameof(bUsesLightmapUVs));
                mat.bUsesMaterialVertexPositionOffset = reader.ReadBoolProp(nameof(bUsesMaterialVertexPositionOffset));
                mat.unkBool1 = reader.ReadBoolProp(nameof(unkBool1));
                reader.ReadNumProp(out mat.UsingTransforms, nameof(UsingTransforms));
                mat.TextureLookups = reader.ReadList(nameof(TextureLookups), static (ref Utf8JsonReader reader) =>
                {
                    reader.Expect(JsonTokenType.StartObject);
                    var lookup = new TextureLookup();
                    reader.ReadNumProp(out lookup.TexCoordIndex, nameof(TextureLookup.TexCoordIndex));
                    reader.ReadNumProp(out lookup.TextureIndex, nameof(TextureLookup.TextureIndex));
                    reader.ReadNumProp(out lookup.UScale, nameof(TextureLookup.UScale));
                    reader.ReadNumProp(out lookup.VScale, nameof(TextureLookup.VScale));
                    reader.ReadNumProp(out lookup.Unk, nameof(TextureLookup.Unk));
                    reader.Read();
                    reader.Expect(JsonTokenType.EndObject);
                    return lookup;
                }).ToArray();

                reader.ReadNumProp(out mat.DummyDroppedFallbackComponents, nameof(DummyDroppedFallbackComponents));
                reader.ReadNumProp(out mat.BlendModeOverrideValue, nameof(BlendModeOverrideValue));
                mat.bIsBlendModeOverrided = reader.ReadBoolProp(nameof(bIsBlendModeOverrided));
                mat.bIsMaskOverrideValue = reader.ReadBoolProp(nameof(bIsMaskOverrideValue));

                reader.Read();
                reader.Expect(JsonTokenType.EndObject);
                return mat;
            }

            public override void Write(Utf8JsonWriter writer, MaterialResource value, JsonSerializerOptions options)
            {
                if (!options.TryGetState(out LEXJSONState state))
                {
                    throw new JsonException($"Could not retrieve {nameof(LEXJSONState)} for this serialization!");
                }
                if (state.Pcc.Game <= MEGame.ME2)
                {
                    throw new JsonException("ME1/ME2 MaterialResources cannot be serialized to JSON");
                }
                writer.WriteStartObject();

                writer.WriteStartArray(nameof(CompileErrors));
                foreach (string error in value.CompileErrors)
                {
                    writer.WriteStringValue(error);
                }
                writer.WriteEndArray();
                writer.WriteStartObject(nameof(TextureDependencyLengthMap));
                foreach ((UIndex key, int val) in value.TextureDependencyLengthMap)
                {
                    writer.WriteNumber(state.UIndexToPath(key), val);
                }
                writer.WriteEndObject();
                writer.WriteNumber(nameof(MaxTextureDependencyLength), value.MaxTextureDependencyLength);
                writer.WriteString(nameof(ID), value.ID);
                writer.WriteNumber(nameof(NumUserTexCoords), value.NumUserTexCoords);
                writer.WriteStartArray(nameof(UniformExpressionTextures));
                foreach (UIndex texUidx in value.UniformExpressionTextures)
                {
                    writer.WriteStringValue(state.UIndexToPath(texUidx));
                }
                writer.WriteEndArray();
                writer.WriteBoolean(nameof(bUsesSceneColor), value.bUsesSceneColor);
                writer.WriteBoolean(nameof(bUsesSceneDepth), value.bUsesSceneDepth);
                writer.WriteBoolean(nameof(bUsesDynamicParameter), value.bUsesDynamicParameter);
                writer.WriteBoolean(nameof(bUsesLightmapUVs), value.bUsesLightmapUVs);
                writer.WriteBoolean(nameof(bUsesMaterialVertexPositionOffset), value.bUsesMaterialVertexPositionOffset);
                writer.WriteBoolean(nameof(unkBool1), value.unkBool1);
                writer.WriteNumber(nameof(UsingTransforms), value.UsingTransforms);
                writer.WriteStartArray(nameof(TextureLookups));
                foreach (TextureLookup texLookup in value.TextureLookups)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber(nameof(TextureLookup.TexCoordIndex), texLookup.TexCoordIndex);
                    writer.WriteNumber(nameof(TextureLookup.TextureIndex), texLookup.TextureIndex);
                    writer.WriteNumber(nameof(TextureLookup.UScale), texLookup.UScale);
                    writer.WriteNumber(nameof(TextureLookup.VScale), texLookup.VScale);
                    writer.WriteNumber(nameof(TextureLookup.Unk), texLookup.Unk);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteNumber(nameof(DummyDroppedFallbackComponents), value.DummyDroppedFallbackComponents);
                writer.WriteNumber(nameof(BlendModeOverrideValue), value.BlendModeOverrideValue);
                writer.WriteBoolean(nameof(bIsBlendModeOverrided), value.bIsBlendModeOverrided);
                writer.WriteBoolean(nameof(bIsMaskOverrideValue), value.bIsMaskOverrideValue);

                writer.WriteEndObject();
            }
        }
    }

    public class ME1MaterialUniformExpressionsElement
    {
        public MaterialUniformExpression[] UniformPixelVectorExpressions;
        public MaterialUniformExpression[] UniformPixelScalarExpressions;
        public MaterialUniformExpressionTexture[] Uniform2DTextureExpressions;
        public MaterialUniformExpressionTexture[] UniformCubeTextureExpressions;
        public uint unk2;
        public uint unk3;
        public uint unk4;
        public uint unk5;
    }

    public class StaticParameterSet : IEquatable<StaticParameterSet>
    {
        public class StaticSwitchParameter : IEquatable<StaticSwitchParameter>
        {
            public NameReference ParameterName;
            public bool Value;
            public bool bOverride; //ignored in equality checks
            public Guid ExpressionGUID;

            public bool Equals(StaticSwitchParameter other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;
                return ParameterName.Equals(other.ParameterName) && Value == other.Value && ExpressionGUID.Equals(other.ExpressionGUID);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((StaticSwitchParameter) obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ParameterName, Value, ExpressionGUID);
            }
        }
        public class StaticComponentMaskParameter : IEquatable<StaticComponentMaskParameter>
        {
            public NameReference ParameterName;
            public bool R;
            public bool G;
            public bool B;
            public bool A;
            public bool bOverride; //ignored in equality checks
            public Guid ExpressionGUID;

            public bool Equals(StaticComponentMaskParameter other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;
                return ParameterName.Equals(other.ParameterName) && R == other.R && G == other.G && B == other.B && A == other.A && ExpressionGUID.Equals(other.ExpressionGUID);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((StaticComponentMaskParameter) obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ParameterName, R, G, B, A, ExpressionGUID);
            }
        }
        public class NormalParameter : IEquatable<NormalParameter>
        {
            public NameReference ParameterName;
            public byte CompressionSettings;
            public bool bOverride; //ignored in equality checks
            public Guid ExpressionGUID;

            public bool Equals(NormalParameter other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;
                return ParameterName.Equals(other.ParameterName) && CompressionSettings == other.CompressionSettings && bOverride == other.bOverride && ExpressionGUID.Equals(other.ExpressionGUID);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((NormalParameter) obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ParameterName, CompressionSettings, bOverride, ExpressionGUID);
            }
        }

        public class TerrainWeightParameter : IEquatable<TerrainWeightParameter>
        {
            public NameReference ParameterName;
            public int WeightmapIndex;
            public bool bOverride; //ignored in equality checks
            public Guid ExpressionGUID;

            public bool Equals(TerrainWeightParameter other)
            {
                if (other is null) return false;
                if (ReferenceEquals(this, other)) return true;
                return ParameterName.Equals(other.ParameterName) && WeightmapIndex == other.WeightmapIndex && bOverride == other.bOverride && ExpressionGUID.Equals(other.ExpressionGUID);
            }

            public override bool Equals(object obj)
            {
                if (obj is null) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((TerrainWeightParameter)obj);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(ParameterName, WeightmapIndex, bOverride, ExpressionGUID);
            }
        }

        public Guid BaseMaterialId;
        public StaticSwitchParameter[] StaticSwitchParameters;
        public StaticComponentMaskParameter[] StaticComponentMaskParameters;
        public NormalParameter[] NormalParameters;//ME3
        public TerrainWeightParameter[] TerrainWeightParameters; // UDK
        #region IEquatable

        public bool Equals(StaticParameterSet other)
        {
            if (other is null || other.BaseMaterialId != BaseMaterialId || other.StaticSwitchParameters.Length != StaticSwitchParameters.Length ||
                other.StaticComponentMaskParameters.Length != StaticComponentMaskParameters.Length || other.NormalParameters.Length != NormalParameters.Length)
            {
                return false;
            }
            //bOverride is intentionally left out of the following comparisons
            for (int i = 0; i < StaticSwitchParameters.Length; i++)
            {
                var a = StaticSwitchParameters[i];
                var b = other.StaticSwitchParameters[i];
                if (a.ParameterName != b.ParameterName || a.ExpressionGUID != b.ExpressionGUID || a.Value != b.Value)
                {
                    return false;
                }
            }
            for (int i = 0; i < StaticComponentMaskParameters.Length; i++)
            {
                var a = StaticComponentMaskParameters[i];
                var b = other.StaticComponentMaskParameters[i];
                if (a.ParameterName != b.ParameterName || a.ExpressionGUID != b.ExpressionGUID || a.R != b.R || a.G != b.G || a.B != b.B || a.A != b.A)
                {
                    return false;
                }
            }
            for (int i = 0; i < NormalParameters.Length; i++)
            {
                var a = NormalParameters[i];
                var b = other.NormalParameters[i];
                if (a.ParameterName != b.ParameterName || a.ExpressionGUID != b.ExpressionGUID || a.CompressionSettings != b.CompressionSettings)
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((StaticParameterSet)obj);
        }

        //The hashcode does not have to uniquely identify the object, so just the Guid is enough
        //including everything else was causing intermittent failures in Dictionary lookup
        public override int GetHashCode() => BaseMaterialId.GetHashCode();

        public static bool operator ==(StaticParameterSet left, StaticParameterSet right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(StaticParameterSet left, StaticParameterSet right)
        {
            return !Equals(left, right);
        }
        #endregion

        public static explicit operator StaticParameterSet(Guid guid)
        {
            return new StaticParameterSet
            {
                BaseMaterialId = guid,
                StaticSwitchParameters = [],
                StaticComponentMaskParameters = [],
                NormalParameters = [],
                TerrainWeightParameters = [], // UDK
            };
        }

        public List<(NameReference, string)> GetNames(MEGame game, string prefix = "")
        {
            var names = new List<(NameReference, string)>();

            names.AddRange(StaticSwitchParameters.Select((param, i) => (param.ParameterName, $"{prefix}{nameof(StaticSwitchParameters)}[{i}].ParameterName")));
            names.AddRange(StaticComponentMaskParameters.Select((param, i) => (param.ParameterName, $"{prefix}{nameof(StaticComponentMaskParameters)}[{i}].ParameterName")));
            if (game >= MEGame.ME3)
            {
                names.AddRange(NormalParameters.Select((param, i) => (param.ParameterName, $"{prefix}{nameof(NormalParameters)}[{i}].ParameterName")));
            }

            return names;
        }
    }

    #region MaterialUniformExpressions

    public class UniformExpressionRenderContext(
        Dictionary<string, float> scalarParameterValues,
        Dictionary<string, LinearColor> vectorParameterValues,
        float currentTime,
        float currentRealTime)
    {
        public readonly Dictionary<string, float> ScalarParameterValues = scalarParameterValues;
        public readonly Dictionary<string, LinearColor> VectorParameterValues = vectorParameterValues;
        public readonly float CurrentTime = currentTime;
        public readonly float CurrentRealTime = currentRealTime;
    }

    public abstract class MaterialUniformExpression
    {
        public NameReference ExpressionType;

        public virtual void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref ExpressionType);
        }

        public virtual List<(NameReference, string)> GetNames(MEGame game)
        {
            return [];
        }

        public abstract void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal);

        public abstract bool IsNotFrameDependent { get; }

        public static MaterialUniformExpression Create(SerializingContainer sc)
        {
            NameReference expressionType = sc.ms.ReadNameReference(sc.Pcc);
            sc.ms.Skip(-8);//ExpressionType will be read again during serialization, so back the stream up.
            return expressionType.Name switch
            {
                "FMaterialUniformExpressionAbs" => new MaterialUniformExpressionAbs(),
                "FMaterialUniformExpressionCeil" => new MaterialUniformExpressionCeil(),
                "FMaterialUniformExpressionFloor" => new MaterialUniformExpressionFloor(),
                "FMaterialUniformExpressionFrac" => new MaterialUniformExpressionFrac(),
                "FMaterialUniformExpressionPeriodic" => new MaterialUniformExpressionPeriodic(),
                "FMaterialUniformExpressionSquareRoot" => new MaterialUniformExpressionSquareRoot(),
                "FMaterialUniformExpressionAppendVector" => new MaterialUniformExpressionAppendVector(),
                "FMaterialUniformExpressionClamp" => new MaterialUniformExpressionClamp(),
                "FMaterialUniformExpressionConstant" => new MaterialUniformExpressionConstant(),
                "FMaterialUniformExpressionFmod" => new MaterialUniformExpressionFmod(),
                "FMaterialUniformExpressionMax" => new MaterialUniformExpressionMax(),
                "FMaterialUniformExpressionMin" => new MaterialUniformExpressionMin(),
                "FMaterialUniformExpressionFoldedMath" => new MaterialUniformExpressionFoldedMath(),
                "FMaterialUniformExpressionTime" => new MaterialUniformExpressionTime(),
                "FMaterialUniformExpressionRealTime" => new MaterialUniformExpressionRealTime(),
                "FMaterialUniformExpressionFractionOfEffectEnabled" => new MaterialUniformExpressionFractionOfEffectEnabled(),
                "FMaterialUniformExpressionScalarParameter" => new MaterialUniformExpressionScalarParameter(),
                "FMaterialUniformExpressionSine" => new MaterialUniformExpressionSine(),
                "FMaterialUniformExpressionTexture" => new MaterialUniformExpressionTexture(),
                "FMaterialUniformExpressionFlipBookTextureParameter" => new MaterialUniformExpressionFlipBookTextureParameter(),
                "FMaterialUniformExpressionTextureParameter" => new MaterialUniformExpressionTextureParameter(),
                "FMaterialUniformExpressionVectorParameter" => new MaterialUniformExpressionVectorParameter(),
                "FMaterialUniformExpressionFlipbookParameter" => new MaterialUniformExpressionFlipbookParameter(),
                _ => throw new ArgumentException(expressionType.Instanced)
            };
        }
    }

    public class MaterialUniformExpressionTime : MaterialUniformExpression
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            outVal.R = context.CurrentTime;
        }

        public override bool IsNotFrameDependent => false;
    }

    public class MaterialUniformExpressionRealTime : MaterialUniformExpression
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            outVal.R = context.CurrentRealTime;
        }

        public override bool IsNotFrameDependent => false;
    }

    public class MaterialUniformExpressionFractionOfEffectEnabled : MaterialUniformExpression
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            //TODO: replace guess with whatever this actually should be
            Debugger.Break();
            outVal = new LinearColor(1, 1, 1, 1);
        }

        public override bool IsNotFrameDependent => false; //Re-evaluate once we've figured out what this is
    }

    public abstract class MaterialUniformExpressionUnaryOp : MaterialUniformExpression
    {
        public MaterialUniformExpression X;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            if (sc.IsLoading)
            {
                X = Create(sc);
            }
            X.Serialize(sc);
        }
        public override bool IsNotFrameDependent => X.IsNotFrameDependent;
    }

    public class MaterialUniformExpressionAbs : MaterialUniformExpressionUnaryOp
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            X.GetNumberValue(context, ref outVal);
            outVal.R = MathF.Abs(outVal.R);
            outVal.G = MathF.Abs(outVal.G);
            outVal.B = MathF.Abs(outVal.B);
            outVal.A = MathF.Abs(outVal.A);
        }

    }

    public class MaterialUniformExpressionCeil : MaterialUniformExpressionUnaryOp
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            X.GetNumberValue(context, ref outVal);
            outVal.R = MathF.Ceiling(outVal.R);
            outVal.G = MathF.Ceiling(outVal.G);
            outVal.B = MathF.Ceiling(outVal.B);
            outVal.A = MathF.Ceiling(outVal.A);
        }
    }

    public class MaterialUniformExpressionFloor : MaterialUniformExpressionUnaryOp
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            X.GetNumberValue(context, ref outVal);
            outVal.R = MathF.Floor(outVal.R);
            outVal.G = MathF.Floor(outVal.G);
            outVal.B = MathF.Floor(outVal.B);
            outVal.A = MathF.Floor(outVal.A);
        }
    }

    public class MaterialUniformExpressionFrac : MaterialUniformExpressionUnaryOp
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            X.GetNumberValue(context, ref outVal);
            outVal.R = outVal.R - MathF.Floor(outVal.R);
            outVal.G = outVal.G - MathF.Floor(outVal.G);
            outVal.B = outVal.B - MathF.Floor(outVal.B);
            outVal.A = outVal.A - MathF.Floor(outVal.A);
        }
    }

    public class MaterialUniformExpressionPeriodic : MaterialUniformExpressionUnaryOp
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            LinearColor temp = LinearColor.Black;
            X.GetNumberValue(context, ref temp);
            outVal.R = temp.R - MathF.Floor(temp.R);
            outVal.G = temp.G - MathF.Floor(temp.G);
            outVal.B = temp.B - MathF.Floor(temp.B);
            outVal.A = temp.A - MathF.Floor(temp.A);
        }
    }

    public class MaterialUniformExpressionSquareRoot : MaterialUniformExpressionUnaryOp
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            LinearColor temp = LinearColor.Black;
            X.GetNumberValue(context, ref temp);
            outVal.R = MathF.Sqrt(temp.R);
        }
    }

    public class MaterialUniformExpressionFlipbookParameter : MaterialUniformExpression
    {
        public int Index;
        public UIndex TextureIndex; //UIndex in ME1/2, index into MaterialResource's Uniform2DTextureExpressions in ME3/LE
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Index);
            sc.Serialize(ref TextureIndex);
        }

        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            throw new NotImplementedException();
        }
        public override bool IsNotFrameDependent => false;
    }

    public class MaterialUniformExpressionSine : MaterialUniformExpressionUnaryOp
    {
        public bool bIsCosine;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref bIsCosine);
        }

        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            LinearColor temp = LinearColor.Black;
            X.GetNumberValue(context, ref temp);
            outVal.R = bIsCosine ? MathF.Cos(temp.R) : MathF.Sin(temp.R);
        }
        public override bool IsNotFrameDependent => X.IsNotFrameDependent;
    }

    public abstract class MaterialUniformExpressionBinaryOp : MaterialUniformExpression
    {
        public MaterialUniformExpression A;
        public MaterialUniformExpression B;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            if (sc.IsLoading)
            {
                A = Create(sc);
            }
            A.Serialize(sc);
            if (sc.IsLoading)
            {
                B = Create(sc);
            }
            B.Serialize(sc);
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            // TODO: IMPROVE TEXT
            return [
                (A.ExpressionType, $"{ExpressionType}.A.ExpressionType"),
                .. A.GetNames(game),
                (B.ExpressionType, $"{ExpressionType}.B.ExpressionType"),
                .. B.GetNames(game),
            ];
        }
        public override bool IsNotFrameDependent => A.IsNotFrameDependent && B.IsNotFrameDependent;
    }

    public class MaterialUniformExpressionFmod: MaterialUniformExpressionBinaryOp
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            LinearColor tempA = LinearColor.Black;
            A.GetNumberValue(context, ref tempA);
            LinearColor tempB = LinearColor.Black;
            B.GetNumberValue(context, ref tempB);
            outVal.R = tempA.R % tempB.R;
            outVal.G = tempA.G % tempB.G;
            outVal.B = tempA.B % tempB.B;
            outVal.A = tempA.A % tempB.A;
        }
    }

    public class MaterialUniformExpressionMax : MaterialUniformExpressionBinaryOp
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            LinearColor tempA = LinearColor.Black;
            A.GetNumberValue(context, ref tempA);
            LinearColor tempB = LinearColor.Black;
            B.GetNumberValue(context, ref tempB);
            outVal.R = MathF.Max(tempA.R, tempB.R);
            outVal.G = MathF.Max(tempA.G, tempB.G);
            outVal.B = MathF.Max(tempA.B, tempB.B);
            outVal.A = MathF.Max(tempA.A, tempB.A);
        }
    }

    public class MaterialUniformExpressionMin : MaterialUniformExpressionBinaryOp
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            LinearColor tempA = LinearColor.Black;
            A.GetNumberValue(context, ref tempA);
            LinearColor tempB = LinearColor.Black;
            B.GetNumberValue(context, ref tempB);
            outVal.R = MathF.Min(tempA.R, tempB.R);
            outVal.G = MathF.Min(tempA.G, tempB.G);
            outVal.B = MathF.Min(tempA.B, tempB.B);
            outVal.A = MathF.Min(tempA.A, tempB.A);
        }
    }

    public class MaterialUniformExpressionAppendVector : MaterialUniformExpressionBinaryOp
    {
        public uint NumComponentsA;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref NumComponentsA);
        }

        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            Debug.Assert(NumComponentsA <= 4);
            LinearColor tempA = LinearColor.Black;
            A.GetNumberValue(context, ref tempA);
            LinearColor tempB = LinearColor.Black;
            B.GetNumberValue(context, ref tempB);
            ReadOnlySpan<float> aFloats = tempA.AsSpanOf<LinearColor, float>();
            ReadOnlySpan<float> bFloats = tempB.AsSpanOf<LinearColor, float>();
            Span<float> resultFloats = outVal.AsSpanOf<LinearColor, float>();
            int numComponentsA = (int)NumComponentsA;
            aFloats[..numComponentsA].CopyTo(resultFloats);
            bFloats[..^numComponentsA].CopyTo(resultFloats[numComponentsA..]);
        }
    }

    public class MaterialUniformExpressionFoldedMath : MaterialUniformExpressionBinaryOp
    {
        public EFoldedMathOperation Op; //EFoldedMathOperation
        public enum EFoldedMathOperation : byte
        {
            Add,
            Sub,
            Mul,
            Div,
            Dot
        }

        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            if (sc.IsLoading)
                Op = (EFoldedMathOperation)sc.ms.ReadByte();
            else
                sc.ms.Writer.WriteByte((byte)Op);
        }

        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            LinearColor tempA = LinearColor.Black;
            A.GetNumberValue(context, ref tempA);
            LinearColor tempB = LinearColor.Black;
            B.GetNumberValue(context, ref tempB);
            var aVec = (Vector4)tempA;
            var bVec = (Vector4)tempB;

            outVal = Op switch
            {
                EFoldedMathOperation.Add => (LinearColor)(aVec + bVec),
                EFoldedMathOperation.Sub => (LinearColor)(aVec - bVec),
                EFoldedMathOperation.Mul => (LinearColor)(aVec * bVec),
                EFoldedMathOperation.Div => new LinearColor(aVec.X / SafeDiv(bVec.X), aVec.Y / SafeDiv(bVec.Y), aVec.Z / SafeDiv(bVec.Z), aVec.W / SafeDiv(bVec.W)),
                EFoldedMathOperation.Dot => new LinearColor(Vector4.Dot(aVec, bVec)),
                _ => throw new Exception($"Unknown folded math operation {Op}")
            };
            return;

            static float SafeDiv(float divisor)
            {
                if (MathF.Abs(divisor) < 1e-05f)
                {
                    if (divisor < 0.0f)
                    {
                        return -1e-05f;
                    }
                    return +1e-05f;
                }
                return divisor;
            }
        }
    }

    public class MaterialUniformExpressionClamp : MaterialUniformExpression
    {
        public MaterialUniformExpression Input;
        public MaterialUniformExpression Min;
        public MaterialUniformExpression Max;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            if (sc.IsLoading)
            {
                Input = Create(sc);
            }
            Input.Serialize(sc);
            if (sc.IsLoading)
            {
                Min = Create(sc);
            }
            Min.Serialize(sc);
            if (sc.IsLoading)
            {
                Max = Create(sc);
            }
            Max.Serialize(sc);
        }

        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            LinearColor inVal = LinearColor.Black;
            LinearColor minVal = LinearColor.Black;
            LinearColor maxVal = LinearColor.Black;
            Input.GetNumberValue(context, ref outVal);
            Min.GetNumberValue(context, ref outVal);
            Max.GetNumberValue(context, ref outVal);

            outVal.R = Math.Clamp(inVal.R, minVal.R, maxVal.R);
            outVal.G = Math.Clamp(inVal.G, minVal.G, maxVal.G);
            outVal.B = Math.Clamp(inVal.B, minVal.B, maxVal.B);
            outVal.A = Math.Clamp(inVal.A, minVal.A, maxVal.A);
        }
        public override bool IsNotFrameDependent => Input.IsNotFrameDependent && Min.IsNotFrameDependent && Max.IsNotFrameDependent;
    }

    public class MaterialUniformExpressionConstant : MaterialUniformExpression
    {
        public LinearColor Value;
        public byte ValueType;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Value);
            sc.Serialize(ref ValueType);
        }

        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            outVal = Value;
        }
        public override bool IsNotFrameDependent => true;
    }

    public class MaterialUniformExpressionTexture : MaterialUniformExpression
    {
        public UIndex TextureIndex; //UIndex in ME1/2, index into MaterialResource's Uniform2DTextureExpressions in ME3/LE
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref TextureIndex);
        }

        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            throw new NotSupportedException();
        }

        public override bool IsNotFrameDependent => true;
    }

    public class MaterialUniformExpressionFlipBookTextureParameter : MaterialUniformExpressionTexture
    {
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            //R = U, G = V, B = A = 0
            Debugger.Break();
            throw new NotImplementedException();
        }
        public override bool IsNotFrameDependent => false;
    }

    public class MaterialUniformExpressionTextureParameter : MaterialUniformExpressionTexture
    {
        public NameReference ParameterName;
        public override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref ExpressionType);
            sc.Serialize(ref ParameterName);
            sc.Serialize(ref TextureIndex);
        }
        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            throw new NotSupportedException();
        }
    }

    public class MaterialUniformExpressionScalarParameter : MaterialUniformExpression
    {
        public NameReference ParameterName;
        public float DefaultValue;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ParameterName);
            sc.Serialize(ref DefaultValue);
        }

        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            float paramValue = context.ScalarParameterValues.GetValueOrDefault(ParameterName.Instanced, DefaultValue);
            outVal = new LinearColor(paramValue, 0, 0, 0);
        }

        public override bool IsNotFrameDependent => true;
    }

    public class MaterialUniformExpressionVectorParameter : MaterialUniformExpression
    {
        public NameReference ParameterName;
        public LinearColor DefaultValue;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ParameterName);
            sc.Serialize(ref DefaultValue);
        }

        public override void GetNumberValue(UniformExpressionRenderContext context, ref LinearColor outVal)
        {
            outVal = context.VectorParameterValues.GetValueOrDefault(ParameterName.Instanced, DefaultValue);
        }

        public override bool IsNotFrameDependent => true;
    }
    #endregion

    public partial class SerializingContainer
    {
        public void Serialize(ref MaterialResource mres)
        {
            if (IsLoading && mres == null)
            {
                mres = new MaterialResource();
            }
            Serialize(ref mres.CompileErrors, Serialize);
            Serialize(ref mres.TextureDependencyLengthMap, Serialize, Serialize);
            Serialize(ref mres.MaxTextureDependencyLength);
            Serialize(ref mres.ID);
            Serialize(ref mres.NumUserTexCoords);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref mres.UniformExpressionTextures, Serialize);
            }
            else
            {
                Serialize(ref mres.UniformPixelVectorExpressions, Serialize);
                Serialize(ref mres.UniformPixelScalarExpressions, Serialize);
                Serialize(ref mres.Uniform2DTextureExpressions, Serialize);
                Serialize(ref mres.UniformCubeTextureExpressions, Serialize);

                if (IsLoading)
                {
                    mres.UniformExpressionTextures = mres.Uniform2DTextureExpressions.Select(texExpr => texExpr.TextureIndex).ToArray();
                }
            }
            Serialize(ref mres.bUsesSceneColor);
            Serialize(ref mres.bUsesSceneDepth);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref mres.bUsesDynamicParameter);
                Serialize(ref mres.bUsesLightmapUVs);
                Serialize(ref mres.bUsesMaterialVertexPositionOffset);
                if (Game == MEGame.ME3 || Game.IsLEGame())
                {
                    Serialize(ref mres.unkBool1);
                }
            }
            Serialize(ref mres.UsingTransforms);
            if (Game == MEGame.ME1)
            {
                Serialize(ref mres.Me1MaterialUniformExpressionsList, Serialize);
            }
            else
            {
                Serialize(ref mres.TextureLookups, Serialize);
                Serialize(ref mres.DummyDroppedFallbackComponents);

                // Not used in TerrainMaterialResource (use that specific class instead)
                if (Game == MEGame.UDK)
                {
                    Serialize(ref mres.BlendModeOverrideValue);
                    Serialize(ref mres.bIsBlendModeOverrided);
                    Serialize(ref mres.bIsMaskOverrideValue);
                }
            }
            if (Game == MEGame.ME1)
            {
                Serialize(ref mres.unk1);
                int tmp = mres.unkCount;
                Serialize(ref tmp);
                mres.unkCount = tmp; //will create mr.unkList of unkCount size
                Serialize(ref mres.unkInt2);
                for (int i = 0; i < mres.unkCount; i++)
                {
                    Serialize(ref mres.unkList[i].Item1);
                    Serialize(ref mres.unkList[i].Item2);
                    Serialize(ref mres.unkList[i].Item3);
                }
            }
        }
        public void Serialize(ref MaterialResource.TextureLookup tLookup)
        {
            if (IsLoading)
            {
                tLookup = new MaterialResource.TextureLookup();
            }
            Serialize(ref tLookup.TexCoordIndex);
            Serialize(ref tLookup.TextureIndex);
            Serialize(ref tLookup.UScale);
            if (IsLoading && Game == MEGame.ME1)
            {
                tLookup.VScale = tLookup.UScale;
            }

            if (Game != MEGame.ME1)
            {
                Serialize(ref tLookup.VScale);
            }

            if (Game.IsLEGame())
            {
                Serialize(ref tLookup.Unk);
            }
        }
        public void Serialize(ref MaterialUniformExpression matExp)
        {
            if (IsLoading)
            {
                matExp = MaterialUniformExpression.Create(this);
            }
            matExp.Serialize(this);
        }
        public void Serialize(ref MaterialUniformExpressionTexture matExp)
        {
            if (IsLoading)
            {
                matExp = (MaterialUniformExpressionTexture)MaterialUniformExpression.Create(this);
            }
            matExp.Serialize(this);
        }
        public void Serialize(ref StaticParameterSet paramSet)
        {
            if (IsLoading)
            {
                paramSet = new StaticParameterSet();
            }
            Serialize(ref paramSet.BaseMaterialId);
            Serialize(ref paramSet.StaticSwitchParameters, Serialize);
            Serialize(ref paramSet.StaticComponentMaskParameters, Serialize);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref paramSet.NormalParameters, Serialize);
                if (Game == MEGame.UDK)
                {
                    Serialize(ref paramSet.TerrainWeightParameters, Serialize);
                }
            }
            else if (IsLoading)
            {
                paramSet.NormalParameters = [];
                paramSet.TerrainWeightParameters = [];
            }
        }
        public void Serialize(ref StaticParameterSet.StaticSwitchParameter param)
        {
            if (IsLoading)
            {
                param = new StaticParameterSet.StaticSwitchParameter();
            }
            Serialize(ref param.ParameterName);
            Serialize(ref param.Value);
            Serialize(ref param.bOverride);
            Serialize(ref param.ExpressionGUID);
        }
        public void Serialize(ref StaticParameterSet.StaticComponentMaskParameter param)
        {
            if (IsLoading)
            {
                param = new StaticParameterSet.StaticComponentMaskParameter();
            }
            Serialize(ref param.ParameterName);
            Serialize(ref param.R);
            Serialize(ref param.G);
            Serialize(ref param.B);
            Serialize(ref param.A);
            Serialize(ref param.bOverride);
            Serialize(ref param.ExpressionGUID);
        }
        public void Serialize(ref StaticParameterSet.NormalParameter param)
        {
            if (IsLoading)
            {
                param = new StaticParameterSet.NormalParameter();
            }
            Serialize(ref param.ParameterName);
            Serialize(ref param.CompressionSettings);
            Serialize(ref param.bOverride);
            Serialize(ref param.ExpressionGUID);
        }
        public void Serialize(ref StaticParameterSet.TerrainWeightParameter param)
        {
            if (IsLoading)
            {
                param = new StaticParameterSet.TerrainWeightParameter();
            }
            Serialize(ref param.ParameterName);
            Serialize(ref param.WeightmapIndex);
            Serialize(ref param.bOverride);
            Serialize(ref param.ExpressionGUID);
        }
        public void Serialize(ref ME1MaterialUniformExpressionsElement elem)
        {
            if (IsLoading)
            {
                elem = new ME1MaterialUniformExpressionsElement();
            }
            Serialize(ref elem.UniformPixelVectorExpressions, Serialize);
            Serialize(ref elem.UniformPixelScalarExpressions, Serialize);
            Serialize(ref elem.Uniform2DTextureExpressions, Serialize);
            Serialize(ref elem.UniformCubeTextureExpressions, Serialize);
            Serialize(ref elem.unk2);
            Serialize(ref elem.unk3);
            Serialize(ref elem.unk4);
            Serialize(ref elem.unk5);
        }
    }
}