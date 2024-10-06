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

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref Model);
            sc.Serialize(ref ZoneIndex);
            sc.Serialize(ref Elements, sc.Serialize);
            sc.Serialize(ref ComponentIndex);
            sc.Serialize(ref Nodes);
        }

        public static ModelComponent Create()
        {
            return new()
            {
                Model = 0,
                Elements = [],
                Nodes = []
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

    public partial class SerializingContainer
    {
        public void Serialize(ref ModelElement elem)
        {
            if (IsLoading)
            {
                elem = new ModelElement();
            }
            Serialize(ref elem.LightMap);
            Serialize(ref elem.Component);
            Serialize(ref elem.Material);
            Serialize(ref elem.Nodes);
            Serialize(ref elem.ShadowMaps, Serialize);
            Serialize(ref elem.IrrelevantLights, Serialize);
        }
    }
}