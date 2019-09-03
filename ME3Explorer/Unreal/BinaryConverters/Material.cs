using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class Material : ObjectBinary
    {
        public MaterialResource SM3MaterialResource;
        public MaterialResource SM2MaterialResource;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref SM3MaterialResource);
            sc.Serialize(ref SM2MaterialResource);
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)>();
            uIndexes.AddRange(SM3MaterialResource.GetUIndexes(game));
            uIndexes.AddRange(SM2MaterialResource.GetUIndexes(game));
            return uIndexes;
        }
    }
    public class MaterialInstance : ObjectBinary
    {
        public MaterialResource SM3StaticPermutationResource;
        public StaticParameterSet SM3StaticParameterSet;
        public MaterialResource SM2StaticPermutationResource;
        public StaticParameterSet SM2StaticParameterSet;
        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref SM3StaticPermutationResource);
            sc.Serialize(ref SM3StaticParameterSet);
            sc.Serialize(ref SM2StaticPermutationResource);
            sc.Serialize(ref SM2StaticParameterSet);
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            var uIndexes = new List<(UIndex, string)>();
            uIndexes.AddRange(SM3StaticPermutationResource.GetUIndexes(game));
            uIndexes.AddRange(SM2StaticPermutationResource.GetUIndexes(game));
            return uIndexes;
        }
    }

    //structs

    public class MaterialResource
    {
        public class TextureLookup
        {
            public int TexCoordIndex;
            public int TextureIndex;
            public int UScale;
            public int VScale;
        }

        public string[] CompileErrors;
        public OrderedMultiValueDictionary<UIndex, int> TextureDependencyLengthMap;
        public int MaxTextureDependencyLength;
        public Guid ID;
        public uint NumUserTexCoords;
        public UIndex[] UniformExpressionTextures; //ME3
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

        public List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex uIndex, string)> uIndexes = TextureDependencyLengthMap.Keys().Select((uIndex, i) => (uIndex, $"TextureDependencyLengthMap[{i}]")).ToList();
            if (game == MEGame.ME3)
            {
                uIndexes.AddRange(UniformExpressionTextures.Select((uIndex, i) => (uIndex, $"UniformExpressionTextures[{i}]")));
            }
            else
            {
                uIndexes.AddRange(UniformPixelVectorExpressions.OfType<MaterialUniformExpressionFlipbookParameter>()
                                                               .Select((flipParam, i) => (flipParam.TextureIndex, $"UniformPixelVectorExpressions[{i}]")));
                uIndexes.AddRange(UniformPixelScalarExpressions.OfType<MaterialUniformExpressionFlipbookParameter>()
                                                               .Select((flipParam, i) => (flipParam.TextureIndex, $"UniformPixelScalarExpressions[{i}]")));
                uIndexes.AddRange(Uniform2DTextureExpressions.Select((texParam, i) => (texParam.TextureIndex, $"Uniform2DTextureExpressions[{i}]")));
                uIndexes.AddRange(UniformCubeTextureExpressions.Select((texParam, i) => (texParam.TextureIndex, $"UniformCubeTextureExpressions[{i}]")));
            }
            return uIndexes;
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

    public class StaticParameterSet
    {
        public class StaticSwitchParameter
        {
            public NameReference ParameterName;
            public bool Value;
            public bool bOverride;
            public Guid ExpressionGUID;
        }
        public class StaticComponentMaskParameter
        {
            public NameReference ParameterName;
            public bool R;
            public bool G;
            public bool B;
            public bool A;
            public bool bOverride;
            public Guid ExpressionGUID;
        }
        public class NormalParameter
        {
            public NameReference ParameterName;
            public byte CompressionSettings;
            public bool bOverride;
            public Guid ExpressionGUID;
        }

        public Guid BaseMaterialId;
        public StaticSwitchParameter[] StaticSwitchParameters;
        public StaticComponentMaskParameter[] StaticComponentMaskParameters;
        public NormalParameter[] NormalParameters;
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
                    throw new ArgumentException(expressionType.InstancedString);
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
}

namespace ME3Explorer
{
    using Unreal.BinaryConverters;

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref MaterialResource mres)
        {
            if (sc.IsLoading)
            {
                mres = new MaterialResource();
            }
            sc.Serialize(ref mres.CompileErrors, Serialize);
            sc.Serialize(ref mres.TextureDependencyLengthMap, Serialize, Serialize);
            sc.Serialize(ref mres.MaxTextureDependencyLength);
            sc.Serialize(ref mres.ID);
            sc.Serialize(ref mres.NumUserTexCoords);
            if (sc.Game == MEGame.ME3)
            {
                sc.Serialize(ref mres.UniformExpressionTextures);
            }
            else
            {
                sc.Serialize(ref mres.UniformPixelVectorExpressions, Serialize);
                sc.Serialize(ref mres.UniformPixelScalarExpressions, Serialize);
                sc.Serialize(ref mres.Uniform2DTextureExpressions, Serialize);
                sc.Serialize(ref mres.UniformCubeTextureExpressions, Serialize);
            }
            sc.Serialize(ref mres.bUsesSceneColor);
            sc.Serialize(ref mres.bUsesSceneDepth);
            if (sc.Game == MEGame.ME3)
            {
                sc.Serialize(ref mres.bUsesDynamicParameter);
                sc.Serialize(ref mres.bUsesLightmapUVs);
                sc.Serialize(ref mres.bUsesMaterialVertexPositionOffset);
                sc.Serialize(ref mres.unkBool1);
            }
            sc.Serialize(ref mres.UsingTransforms);
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref mres.Me1MaterialUniformExpressionsList, Serialize);
            }
            else
            {
                sc.Serialize(ref mres.TextureLookups, Serialize);
                int dummy = 0;
                sc.Serialize(ref dummy);
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
            sc.Serialize(ref paramSet.NormalParameters, Serialize);
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
