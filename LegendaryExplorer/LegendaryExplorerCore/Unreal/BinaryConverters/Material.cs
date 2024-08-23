using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        public uint unkUint1;
        public uint udkUnk2;
        public uint udkUnk3;
        public uint udkUnk4;
        //begin ME1
        public ME1MaterialUniformExpressionsElement[] Me1MaterialUniformExpressionsList;
        public int unk1;
        public int unkCount
        {
            get => unkList?.Length ?? 0;
            set => Array.Resize(ref unkList, value);
        }
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

                reader.ReadNumProp(out mat.unkUint1, nameof(unkUint1));
                reader.ReadNumProp(out mat.udkUnk2, nameof(udkUnk2));
                reader.ReadNumProp(out mat.udkUnk3, nameof(udkUnk3));
                reader.ReadNumProp(out mat.udkUnk4, nameof(udkUnk4));

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
                writer.WriteNumber(nameof(unkUint1), value.unkUint1);
                writer.WriteNumber(nameof(udkUnk2), value.udkUnk2);
                writer.WriteNumber(nameof(udkUnk3), value.udkUnk3);
                writer.WriteNumber(nameof(udkUnk4), value.udkUnk4);

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

        public Guid BaseMaterialId;
        public StaticSwitchParameter[] StaticSwitchParameters;
        public StaticComponentMaskParameter[] StaticComponentMaskParameters;
        public NormalParameter[] NormalParameters;//ME3

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

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = BaseMaterialId.GetHashCode();
                foreach (StaticSwitchParameter staticSwitchParameter in StaticSwitchParameters)
                {
                    hashCode = hashCode * 31 + staticSwitchParameter.GetHashCode();
                }
                foreach (StaticComponentMaskParameter staticComponentMaskParameter in StaticComponentMaskParameters)
                {
                    hashCode = hashCode * 31 + staticComponentMaskParameter.GetHashCode();
                }
                foreach (NormalParameter normalParameter in NormalParameters)
                {
                    hashCode = hashCode * 31 + normalParameter.GetHashCode();
                }
                return hashCode;
            }
        }

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
                NormalParameters = []
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
    //FMaterialUniformExpressionRealTime
    //FMaterialUniformExpressionTime
    //FMaterialUniformExpressionFractionOfEffectEnabled
    public class MaterialUniformExpression
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

        public static MaterialUniformExpression Create(SerializingContainer sc)
        {
            NameReference expressionType = sc.ms.ReadNameReference(sc.Pcc);
            sc.ms.Skip(-8);//ExpressionType will be read again during serialization, so back the stream up.
            switch (expressionType.Name)
            {
                case "FMaterialUniformExpressionAbs":
                case "FMaterialUniformExpressionCeil":
                case "FMaterialUniformExpressionFloor":
                case "FMaterialUniformExpressionFrac":
                case "FMaterialUniformExpressionPeriodic":
                case "FMaterialUniformExpressionSquareRoot":
                    return new MaterialUniformExpressionUnaryOp();
                case "FMaterialUniformExpressionAppendVector":
                    return new MaterialUniformExpressionAppendVector();
                case "FMaterialUniformExpressionClamp":
                    return new MaterialUniformExpressionClamp();
                case "FMaterialUniformExpressionConstant":
                    return new MaterialUniformExpressionConstant();
                case "FMaterialUniformExpressionFmod":
                case "FMaterialUniformExpressionMax":
                case "FMaterialUniformExpressionMin":
                    return new MaterialUniformExpressionBinaryOp();
                case "FMaterialUniformExpressionFoldedMath":
                    return new MaterialUniformExpressionFoldedMath();
                case "FMaterialUniformExpressionTime":
                case "FMaterialUniformExpressionRealTime":
                case "FMaterialUniformExpressionFractionOfEffectEnabled":
                    return new MaterialUniformExpression();
                case "FMaterialUniformExpressionScalarParameter":
                    return new MaterialUniformExpressionScalarParameter();
                case "FMaterialUniformExpressionSine":
                    return new MaterialUniformExpressionSine();
                case "FMaterialUniformExpressionTexture":
                case "FMaterialUniformExpressionFlipBookTextureParameter":
                    return new MaterialUniformExpressionTexture();
                case "FMaterialUniformExpressionTextureParameter":
                    return new MaterialUniformExpressionTextureParameter();
                case "FMaterialUniformExpressionVectorParameter":
                    return new MaterialUniformExpressionVectorParameter();
                case "FMaterialUniformExpressionFlipbookParameter":
                    return new MaterialUniformExpressionFlipbookParameter();
                default:
                    throw new ArgumentException(expressionType.Instanced);
            }
        }
    }
    // FMaterialUniformExpressionAbs
    // FMaterialUniformExpressionCeil
    // FMaterialUniformExpressionFloor
    // FMaterialUniformExpressionFrac
    // FMaterialUniformExpressionPeriodic
    // FMaterialUniformExpressionSquareRoot
    public class MaterialUniformExpressionUnaryOp : MaterialUniformExpression
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
    }
    //FMaterialUniformExpressionFlipbookParameter
    public class MaterialUniformExpressionFlipbookParameter : MaterialUniformExpression
    {
        public int Index;
        public UIndex TextureIndex;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Index);
            sc.Serialize(ref TextureIndex);
        }
    }
    //FMaterialUniformExpressionSine
    public class MaterialUniformExpressionSine : MaterialUniformExpressionUnaryOp
    {
        public bool bIsCosine;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref bIsCosine);
        }
    }
    // FMaterialUniformExpressionFmod
    // FMaterialUniformExpressionMax
    // FMaterialUniformExpressionMin
    public class MaterialUniformExpressionBinaryOp : MaterialUniformExpression
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
    }
    // FMaterialUniformExpressionAppendVector
    public class MaterialUniformExpressionAppendVector : MaterialUniformExpressionBinaryOp
    {
        public uint NumComponentsA;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref NumComponentsA);
        }
    }
    //FMaterialUniformExpressionFoldedMath
    public class MaterialUniformExpressionFoldedMath : MaterialUniformExpressionBinaryOp
    {
        public byte Op; //EFoldedMathOperation
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref Op);
        }
    }
    // FMaterialUniformExpressionClamp
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
    }
    //FMaterialUniformExpressionConstant
    public class MaterialUniformExpressionConstant : MaterialUniformExpression
    {
        public float R;
        public float G;
        public float B;
        public float A;
        public byte ValueType;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref R);
            sc.Serialize(ref G);
            sc.Serialize(ref B);
            sc.Serialize(ref A);
            sc.Serialize(ref ValueType);
        }
    }
    //FMaterialUniformExpressionTexture
    //FMaterialUniformExpressionFlipBookTextureParameter
    public class MaterialUniformExpressionTexture : MaterialUniformExpression
    {
        public UIndex TextureIndex; //UIndex in ME1/2, index into MaterialResource's Uniform2DTextureExpressions in ME3
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref TextureIndex);
        }
    }
    //FMaterialUniformExpressionTextureParameter
    public class MaterialUniformExpressionTextureParameter : MaterialUniformExpressionTexture
    {
        public NameReference ParameterName;
        public override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref ExpressionType);
            sc.Serialize(ref ParameterName);
            sc.Serialize(ref TextureIndex);
        }
    }
    //FMaterialUniformExpressionScalarParameter
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
    }
    //FMaterialUniformExpressionVectorParameter
    public class MaterialUniformExpressionVectorParameter : MaterialUniformExpression
    {
        public NameReference ParameterName;
        public float DefaultR;
        public float DefaultG;
        public float DefaultB;
        public float DefaultA;
        public override void Serialize(SerializingContainer sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref ParameterName);
            sc.Serialize(ref DefaultR);
            sc.Serialize(ref DefaultG);
            sc.Serialize(ref DefaultB);
            sc.Serialize(ref DefaultA);
        }
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
                Serialize(ref mres.unkUint1);

                // Not used in TerrainMaterialResource (use that specific class instead)
                if (Game == MEGame.UDK)
                {
                    Serialize(ref mres.udkUnk2);
                    Serialize(ref mres.udkUnk3);
                    Serialize(ref mres.udkUnk4);
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
            }
            else if (IsLoading)
            {
                paramSet.NormalParameters = [];
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