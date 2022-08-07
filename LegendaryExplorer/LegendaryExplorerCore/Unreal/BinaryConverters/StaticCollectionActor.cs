using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public abstract class StaticCollectionActor : ObjectBinary
    {
        public List<UIndex> Components;

        public List<Matrix4x4> LocalToWorldTransforms;

        public abstract string ComponentPropName { get; }

        protected override void Serialize(SerializingContainer2 sc)
        {
            var components = Export.GetProperty<ArrayProperty<ObjectProperty>>(ComponentPropName);
            if (components == null || components.Count == 0)
            {
                return;
            }

            if (sc.IsLoading)
            {
                // Components are technically not part of the binary data. However they are required to be parsed for this class so we might as well just leverage their utility here.
                Components = components.Select(x => x.Value).ToList();
                LocalToWorldTransforms = new List<Matrix4x4>(components.Count);
            }

            for (int i = 0; i < components.Count; i++)
            {
                Matrix4x4 m = i < LocalToWorldTransforms.Count ? LocalToWorldTransforms[i] : Matrix4x4.Identity;
                sc.Serialize(ref m);
                if (sc.IsLoading)
                {
                    LocalToWorldTransforms.Add(m);
                }
            }
        }
    }

    public class StaticMeshCollectionActor : StaticCollectionActor
    {
        public override string ComponentPropName => "StaticMeshComponents";

        public static StaticMeshCollectionActor Create()
        {
            return new()
            {
                Components = new List<UIndex>(),
                LocalToWorldTransforms = new List<Matrix4x4>(),
            };
        }
    }
    public class StaticLightCollectionActor : StaticCollectionActor
    {
        public override string ComponentPropName => "LightComponents";

        public static StaticLightCollectionActor Create()
        {
            return new()
            {
                Components = new List<UIndex>(),
                LocalToWorldTransforms = new List<Matrix4x4>(),
            };
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref Matrix4x4 matrix)
        {
            if (sc.IsLoading)
            {
                matrix = new Matrix4x4(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(),
                    sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(),
                    sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(),
                    sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
            }
            else
            {
                sc.ms.Writer.WriteFloat(matrix.M11); sc.ms.Writer.WriteFloat(matrix.M12); sc.ms.Writer.WriteFloat(matrix.M13); sc.ms.Writer.WriteFloat(matrix.M14);
                sc.ms.Writer.WriteFloat(matrix.M21); sc.ms.Writer.WriteFloat(matrix.M22); sc.ms.Writer.WriteFloat(matrix.M23); sc.ms.Writer.WriteFloat(matrix.M24);
                sc.ms.Writer.WriteFloat(matrix.M31); sc.ms.Writer.WriteFloat(matrix.M32); sc.ms.Writer.WriteFloat(matrix.M33); sc.ms.Writer.WriteFloat(matrix.M34);
                sc.ms.Writer.WriteFloat(matrix.M41); sc.ms.Writer.WriteFloat(matrix.M42); sc.ms.Writer.WriteFloat(matrix.M43); sc.ms.Writer.WriteFloat(matrix.M44);
            }
        }
    }
}