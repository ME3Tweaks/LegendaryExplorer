using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using System.Numerics;
using System.Runtime.CompilerServices;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public sealed class Polys : ObjectBinary
    {
        public int PolyCount
        {
            get => Elements?.Length ?? 0;
            set => Array.Resize(ref Elements, value);
        }
        public int PolyMax;
        public UIndex Owner;
        public Poly[] Elements;

        protected override void Serialize(SerializingContainer sc)
        {
            int polyCount = PolyCount;
            sc.Serialize(ref polyCount);
            PolyCount = polyCount;
            sc.Serialize(ref PolyMax);
            sc.Serialize(ref Owner);

            for (int i = 0; i < PolyCount; i++)
            {
                sc.Serialize(ref Elements[i]);
            }
        }

        public static Polys Create()
        {
            return new()
            {
                Owner = 0,
                Elements = []
            };
        }
        
        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = new List<(NameReference, string)>();

            for (int i = 0; i < Elements.Length; i++)
            {
                Poly poly = Elements[i];
                names.Add((poly.ItemName, $"Elements[{i}].ItemName"));
                if (game >= MEGame.ME3)
                {
                    names.Add((poly.ItemName, $"Elements[{i}].RulesetVariation"));
                }
            }

            return names;
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            Unsafe.AsRef(in action).Invoke(ref Owner, nameof(Owner));
            for (int i = 0; i < Elements.Length; i++)
            {
                Unsafe.AsRef(in action).Invoke(ref Elements[i].Actor, $"Actor[{i}]");
                Unsafe.AsRef(in action).Invoke(ref Elements[i].Material, $"Material[{i}]");
            }
        }
    }
    public class Poly
    {
        public Vector3 Base;
        public Vector3 Normal;
        public Vector3 TextureU;
        public Vector3 TextureV;
        public Vector3[] Vertices;
        public int PolyFlags;
        public UIndex Actor;
        public NameReference ItemName;
        public UIndex Material;
        public int iLink;
        public int iBrushPoly;
        public float ShadowMapScale;
        public int LightingChannels;
        public LightmassPrimitiveSettings LightmassSettings; //ME3 only
        public NameReference RulesetVariation; //ME3 only
    }

    public class LightmassPrimitiveSettings
    {
        public bool bUseTwoSidedLighting;
        public bool bShadowIndirectOnly;
        public float FullyOccludedSamplesFraction;
        public bool bUseEmissiveForStaticLighting;
        public float EmissiveLightFalloffExponent;
        public float EmissiveLightExplicitInfluenceRadius;
        public float EmissiveBoost;
        public float DiffuseBoost;
        public float SpecularBoost;
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref LightmassPrimitiveSettings lps)
        {
            if (IsLoading)
            {
                lps = new LightmassPrimitiveSettings();
            }
            Serialize(ref lps.bUseTwoSidedLighting);
            Serialize(ref lps.bShadowIndirectOnly);
            Serialize(ref lps.FullyOccludedSamplesFraction);
            Serialize(ref lps.bUseEmissiveForStaticLighting);
            Serialize(ref lps.EmissiveLightFalloffExponent);
            Serialize(ref lps.EmissiveLightExplicitInfluenceRadius);
            Serialize(ref lps.EmissiveBoost);
            Serialize(ref lps.DiffuseBoost);
            Serialize(ref lps.SpecularBoost);
        }
        public void Serialize(ref Poly poly)
        {
            if (IsLoading)
            {
                poly = new Poly();
            }
            Serialize(ref poly.Base);
            Serialize(ref poly.Normal);
            Serialize(ref poly.TextureU);
            Serialize(ref poly.TextureV);
            Serialize(ref poly.Vertices);
            Serialize(ref poly.PolyFlags);
            Serialize(ref poly.Actor);
            Serialize(ref poly.ItemName);
            Serialize(ref poly.Material);
            Serialize(ref poly.iLink);
            Serialize(ref poly.iBrushPoly);
            Serialize(ref poly.ShadowMapScale);
            Serialize(ref poly.LightingChannels);
            if (Game >= MEGame.ME3)
            {
                Serialize(ref poly.LightmassSettings);
                Serialize(ref poly.RulesetVariation);
            }
            else if (IsLoading)
            {
                //defaults that won't break the lighting completely
                poly.LightmassSettings = new LightmassPrimitiveSettings
                {
                    FullyOccludedSamplesFraction = 1,
                    EmissiveLightFalloffExponent = 2,
                    DiffuseBoost = 1,
                    EmissiveBoost = 1,
                    SpecularBoost = 1,
                };
                poly.RulesetVariation = "None";
            }
        }
    }
}