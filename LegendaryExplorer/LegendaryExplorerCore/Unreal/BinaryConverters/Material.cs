using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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
        protected override void Serialize(SerializingContainer2 sc)
        {
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
    }
    public class MaterialInstance : ObjectBinary
    {
        public MaterialResource SM3StaticPermutationResource;
        public StaticParameterSet SM3StaticParameterSet; //Not SM3 in LE... not sure what to call the variable though
        public MaterialResource SM2StaticPermutationResource;
        public StaticParameterSet SM2StaticParameterSet;
        protected override void Serialize(SerializingContainer2 sc)
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
        public UIndex[] UniformExpressionTextures; //serialized for ME3, but will be set here for ME1 and ME2 as well
        //begin Not ME3
        public MaterialUniformExpression[] UniformPixelVectorExpressions;
        public MaterialUniformExpression[] UniformPixelScalarExpressions;
        public MaterialUniformExpressionTexture[] Uniform2DTextureExpressions;
        public MaterialUniformExpressionTexture[] UniformCubeTextureExpressions;
        //end Not ME3
        public bool bUsesSceneColor;
        public bool bUsesSceneDepth;
        //begin ME3
        public bool bUsesDynamicParameter;
        public bool bUsesLightmapUVs;
        public bool bUsesMaterialVertexPositionOffset;
        public bool unkBool1;
        //end ME3
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
                CompileErrors = Array.Empty<string>(),
                TextureDependencyLengthMap = new (),
                UniformExpressionTextures = Array.Empty<UIndex>(),
                UniformPixelVectorExpressions = Array.Empty<MaterialUniformExpression>(),
                UniformPixelScalarExpressions = Array.Empty<MaterialUniformExpression>(),
                Uniform2DTextureExpressions = Array.Empty<MaterialUniformExpressionTexture>(),
                UniformCubeTextureExpressions = Array.Empty<MaterialUniformExpressionTexture>(),
                TextureLookups = Array.Empty<TextureLookup>(),
                Me1MaterialUniformExpressionsList = Array.Empty<ME1MaterialUniformExpressionsElement>(),
                unkList = Array.Empty<(int, float, int)>(),
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
            TAction a = Unsafe.AsRef(uIndexAction);
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
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return ParameterName.Equals(other.ParameterName) && Value == other.Value && ExpressionGUID.Equals(other.ExpressionGUID);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
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
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return ParameterName.Equals(other.ParameterName) && R == other.R && G == other.G && B == other.B && A == other.A && ExpressionGUID.Equals(other.ExpressionGUID);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
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
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return ParameterName.Equals(other.ParameterName) && CompressionSettings == other.CompressionSettings && bOverride == other.bOverride && ExpressionGUID.Equals(other.ExpressionGUID);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
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
                StaticSwitchParameters = Array.Empty<StaticSwitchParameter>(),
                StaticComponentMaskParameters = Array.Empty<StaticComponentMaskParameter>(),
                NormalParameters = Array.Empty<NormalParameter>()
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

        public virtual void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref ExpressionType);
        }

        public virtual List<(NameReference, string)> GetNames(MEGame game)
        {
            return new List<(NameReference, string)>(0);
        }

        public static MaterialUniformExpression Create(SerializingContainer2 sc)
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
        public override void Serialize(SerializingContainer2 sc)
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
        public override void Serialize(SerializingContainer2 sc)
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
        public override void Serialize(SerializingContainer2 sc)
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
        public override void Serialize(SerializingContainer2 sc)
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
            var names = new List<(NameReference, string)>();
            names.Add((A.ExpressionType, $"{ExpressionType}.A.ExpressionType"));
            names.AddRange(A.GetNames(game));
            names.Add((B.ExpressionType, $"{ExpressionType}.B.ExpressionType"));
            names.AddRange(B.GetNames(game));
            return names;
        }

    }
    // FMaterialUniformExpressionAppendVector
    public class MaterialUniformExpressionAppendVector : MaterialUniformExpressionBinaryOp
    {
        public uint NumComponentsA;
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref NumComponentsA);
        }
    }
    //FMaterialUniformExpressionFoldedMath
    public class MaterialUniformExpressionFoldedMath : MaterialUniformExpressionBinaryOp
    {
        public byte Op; //EFoldedMathOperation
        public override void Serialize(SerializingContainer2 sc)
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
        public override void Serialize(SerializingContainer2 sc)
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
        public override void Serialize(SerializingContainer2 sc)
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
        public override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref TextureIndex);
        }
    }
    //FMaterialUniformExpressionTextureParameter
    public class MaterialUniformExpressionTextureParameter : MaterialUniformExpressionTexture
    {
        public NameReference ParameterName;
        public override void Serialize(SerializingContainer2 sc)
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
        public override void Serialize(SerializingContainer2 sc)
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
        public override void Serialize(SerializingContainer2 sc)
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

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref MaterialResource mres)
        {
            if (sc.IsLoading && mres == null)
            {
                mres = new MaterialResource();
            }
            sc.Serialize(ref mres.CompileErrors, SCExt.Serialize);
            sc.Serialize(ref mres.TextureDependencyLengthMap, Serialize, Serialize);
            sc.Serialize(ref mres.MaxTextureDependencyLength);
            sc.Serialize(ref mres.ID);
            sc.Serialize(ref mres.NumUserTexCoords);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref mres.UniformExpressionTextures, Serialize);
            }
            else
            {
                sc.Serialize(ref mres.UniformPixelVectorExpressions, Serialize);
                sc.Serialize(ref mres.UniformPixelScalarExpressions, Serialize);
                sc.Serialize(ref mres.Uniform2DTextureExpressions, Serialize);
                sc.Serialize(ref mres.UniformCubeTextureExpressions, Serialize);

                if (sc.IsLoading)
                {
                    mres.UniformExpressionTextures = mres.Uniform2DTextureExpressions.Select(texExpr => texExpr.TextureIndex).ToArray();
                }
            }
            sc.Serialize(ref mres.bUsesSceneColor);
            sc.Serialize(ref mres.bUsesSceneDepth);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref mres.bUsesDynamicParameter);
                sc.Serialize(ref mres.bUsesLightmapUVs);
                sc.Serialize(ref mres.bUsesMaterialVertexPositionOffset);
                if (sc.Game == MEGame.ME3 || sc.Game.IsLEGame())
                {
                    sc.Serialize(ref mres.unkBool1);
                }
            }
            sc.Serialize(ref mres.UsingTransforms);
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref mres.Me1MaterialUniformExpressionsList, Serialize);
            }
            else
            {
                sc.Serialize(ref mres.TextureLookups, Serialize);
                sc.Serialize(ref mres.unkUint1);
                
                
                // If we are porting a terrain, these are NOT used in it's CachedMaterials!
                // This will break porting from UDK
                if (sc.Game == MEGame.UDK) // These are not used in Terrain Cached Materials!
                {
                    sc.Serialize(ref mres.udkUnk2);
                    sc.Serialize(ref mres.udkUnk3);
                    sc.Serialize(ref mres.udkUnk4);
                }
            }
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref mres.unk1);
                int tmp = mres.unkCount;
                sc.Serialize(ref tmp);
                mres.unkCount = tmp; //will create mr.unkList of unkCount size
                sc.Serialize(ref mres.unkInt2);
                for (int i = 0; i < mres.unkCount; i++)
                {
                    sc.Serialize(ref mres.unkList[i].Item1);
                    sc.Serialize(ref mres.unkList[i].Item2);
                    sc.Serialize(ref mres.unkList[i].Item3);
                }
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref MaterialResource.TextureLookup tLookup)
        {
            if (sc.IsLoading)
            {
                tLookup = new MaterialResource.TextureLookup();
            }
            sc.Serialize(ref tLookup.TexCoordIndex);
            sc.Serialize(ref tLookup.TextureIndex);
            sc.Serialize(ref tLookup.UScale);
            if (sc.IsLoading && sc.Game == MEGame.ME1)
            {
                tLookup.VScale = tLookup.UScale;
            }

            if (sc.Game != MEGame.ME1)
            {
                sc.Serialize(ref tLookup.VScale);
            }

            if (sc.Game.IsLEGame())
            {
                sc.Serialize(ref tLookup.Unk);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref MaterialUniformExpression matExp)
        {
            if (sc.IsLoading)
            {
                matExp = MaterialUniformExpression.Create(sc);
            }
            matExp.Serialize(sc);
        }
        public static void Serialize(this SerializingContainer2 sc, ref MaterialUniformExpressionTexture matExp)
        {
            if (sc.IsLoading)
            {
                matExp = (MaterialUniformExpressionTexture)MaterialUniformExpression.Create(sc);
            }
            matExp.Serialize(sc);
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticParameterSet paramSet)
        {
            if (sc.IsLoading)
            {
                paramSet = new StaticParameterSet();
            }
            sc.Serialize(ref paramSet.BaseMaterialId);
            sc.Serialize(ref paramSet.StaticSwitchParameters, Serialize);
            sc.Serialize(ref paramSet.StaticComponentMaskParameters, Serialize);
            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref paramSet.NormalParameters, Serialize);
            }
            else if (sc.IsLoading)
            {
                paramSet.NormalParameters = new StaticParameterSet.NormalParameter[0];
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticParameterSet.StaticSwitchParameter param)
        {
            if (sc.IsLoading)
            {
                param = new StaticParameterSet.StaticSwitchParameter();
            }
            sc.Serialize(ref param.ParameterName);
            sc.Serialize(ref param.Value);
            sc.Serialize(ref param.bOverride);
            sc.Serialize(ref param.ExpressionGUID);
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticParameterSet.StaticComponentMaskParameter param)
        {
            if (sc.IsLoading)
            {
                param = new StaticParameterSet.StaticComponentMaskParameter();
            }
            sc.Serialize(ref param.ParameterName);
            sc.Serialize(ref param.R);
            sc.Serialize(ref param.G);
            sc.Serialize(ref param.B);
            sc.Serialize(ref param.A);
            sc.Serialize(ref param.bOverride);
            sc.Serialize(ref param.ExpressionGUID);
        }
        public static void Serialize(this SerializingContainer2 sc, ref StaticParameterSet.NormalParameter param)
        {
            if (sc.IsLoading)
            {
                param = new StaticParameterSet.NormalParameter();
            }
            sc.Serialize(ref param.ParameterName);
            sc.Serialize(ref param.CompressionSettings);
            sc.Serialize(ref param.bOverride);
            sc.Serialize(ref param.ExpressionGUID);
        }
        public static void Serialize(this SerializingContainer2 sc, ref ME1MaterialUniformExpressionsElement elem)
        {
            if (sc.IsLoading)
            {
                elem = new ME1MaterialUniformExpressionsElement();
            }
            sc.Serialize(ref elem.UniformPixelVectorExpressions, Serialize);
            sc.Serialize(ref elem.UniformPixelScalarExpressions, Serialize);
            sc.Serialize(ref elem.Uniform2DTextureExpressions, Serialize);
            sc.Serialize(ref elem.UniformCubeTextureExpressions, Serialize);
            sc.Serialize(ref elem.unk2);
            sc.Serialize(ref elem.unk3);
            sc.Serialize(ref elem.unk4);
            sc.Serialize(ref elem.unk5);
        }
    }
}