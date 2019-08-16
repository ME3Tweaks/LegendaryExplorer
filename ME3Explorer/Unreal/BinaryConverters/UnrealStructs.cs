using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public readonly struct Vector
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vector(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
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

    public static class UnrealStructSerializingContainerExtensions
    {
        public static void Serialize(this SerializingContainer2 sc, ref Vector vec)
        {
            if (sc.IsLoading)
            {
                vec = new Vector(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
            }
            else
            {
                sc.ms.WriteFloat(vec.X);
                sc.ms.WriteFloat(vec.Y);
                sc.ms.WriteFloat(vec.Z);
            }
        }
        public static void Serialize(this SerializingContainer2 sc, ref Vector[] arr)
        {
            int count = arr?.Length ?? 0;
            sc.Serialize(ref count);
            if (sc.IsLoading)
            {
                arr = new Vector[count];
            }

            for (int i = 0; i < count; i++)
            {
                sc.Serialize(ref arr[i]);
            }

        }
        public static void Serialize(this SerializingContainer2 sc, ref LightmassPrimitiveSettings lps)
        {
            if (sc.IsLoading)
            {
                lps = new LightmassPrimitiveSettings();
            }
            sc.Serialize(ref lps.bUseTwoSidedLighting, true);
            sc.Serialize(ref lps.bShadowIndirectOnly, true);
            sc.Serialize(ref lps.FullyOccludedSamplesFraction);
            sc.Serialize(ref lps.bUseEmissiveForStaticLighting, true);
            sc.Serialize(ref lps.EmissiveLightFalloffExponent);
            sc.Serialize(ref lps.EmissiveLightExplicitInfluenceRadius);
            sc.Serialize(ref lps.EmissiveBoost);
            sc.Serialize(ref lps.DiffuseBoost);
            sc.Serialize(ref lps.SpecularBoost);
        }
    }
}