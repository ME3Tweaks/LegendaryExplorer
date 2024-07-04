using System;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class ModelComponent : ObjectBinary
    {
        public UIndex Model;
        public int ZoneIndex;
        public ModelElement[] Elements;
        public ushort ComponentIndex;
        public ushort[] Nodes;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref Model);
            sc.Serialize(ref ZoneIndex);
            sc.Serialize(ref Elements, SCExt.Serialize);
            sc.Serialize(ref ComponentIndex);
            sc.Serialize(ref Nodes);
        }

        public static ModelComponent Create()
        {
            return new()
            {
                Model = 0,
                Elements = Array.Empty<ModelElement>(),
                Nodes = Array.Empty<ushort>()
            };
        }

        public override void ForEachUIndex<TAction>(MEGame game, in TAction action)
        {
            ref TAction a = ref Unsafe.AsRef(in action);

            a.Invoke(ref Model, nameof(Model));
            for (int i = 0; i < Elements.Length; i++)
            {
                ModelElement elm = Elements[i];
                string prefix = $"Elements[{i}].";
                elm.LightMap.ForEachUIndex(game, action, prefix);
                a.Invoke(ref elm.Component, $"{prefix}Component");
                a.Invoke(ref elm.Material, $"{prefix}Material");
                ForEachUIndexInSpan(action, elm.ShadowMaps.AsSpan(), $"{prefix}ShadowMaps");
            }
        }
    }

    public class ModelElement
    {
        public LightMap LightMap;
        public UIndex Component;
        public UIndex Material;
        public ushort[] Nodes;
        public UIndex[] ShadowMaps;
        public Guid[] IrrelevantLights;
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref ModelElement elem)
        {
            if (sc.IsLoading)
            {
                elem = new ModelElement();
            }
            sc.Serialize(ref elem.LightMap);
            sc.Serialize(ref elem.Component);
            sc.Serialize(ref elem.Material);
            sc.Serialize(ref elem.Nodes);
            sc.Serialize(ref elem.ShadowMaps, Serialize);
            sc.Serialize(ref elem.IrrelevantLights, SCExt.Serialize);
        }
    }
}