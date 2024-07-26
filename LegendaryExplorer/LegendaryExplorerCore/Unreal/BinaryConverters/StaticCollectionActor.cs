using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using UIndex = System.Int32;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public abstract class StaticCollectionActor : ObjectBinary
    {
        public List<int> Components;

        public List<Matrix4x4> LocalToWorldTransforms;

        public abstract string ComponentPropName { get; }

        protected override void Serialize(SerializingContainer sc)
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

        public static bool TryGetStaticCollectionActorAndIndex(ExportEntry component, out StaticCollectionActor staticCollectionActor, out int i)
        {
            if (component.Parent is ExportEntry staticCollectionActorExport && From(staticCollectionActorExport) is StaticCollectionActor sca)
            {
                staticCollectionActor = sca;
                if (staticCollectionActorExport.GetProperty<ArrayProperty<ObjectProperty>>(staticCollectionActor.ComponentPropName) is { } components)
                {
                    i = components.FindIndex(prop => prop.Value == component.UIndex);
                    return i >= 0 && staticCollectionActor.LocalToWorldTransforms.Count > i;
                }
            }
            i = 0;
            staticCollectionActor = null;
            return false;
        }

        public (Vector3 translation, Vector3 scale, Rotator rotation) GetDecomposedTransformationForIndex(int index) => LocalToWorldTransforms[index].UnrealDecompose();

        public void UpdateTransformationForIndex(int index, Vector3 location, Vector3 scale, Rotator rotation)
        {
            Matrix4x4 m = ActorUtils.ComposeLocalToWorld(location, rotation, scale);
            LocalToWorldTransforms[index] = m;
        }
    }

    public class StaticMeshCollectionActor : StaticCollectionActor
    {
        public override string ComponentPropName => "StaticMeshComponents";

        public static StaticMeshCollectionActor Create()
        {
            return new()
            {
                Components = [],
                LocalToWorldTransforms = [],
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
                Components = [],
                LocalToWorldTransforms = [],
            };
        }
    }

    public partial class SerializingContainer
    {
        public void Serialize(ref Matrix4x4 matrix)
        {
            if (IsLoading)
            {
                matrix = new Matrix4x4(ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(),
                    ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(),
                    ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(),
                    ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat(), ms.ReadFloat());
            }
            else
            {
                ms.Writer.WriteFloat(matrix.M11); ms.Writer.WriteFloat(matrix.M12); ms.Writer.WriteFloat(matrix.M13); ms.Writer.WriteFloat(matrix.M14);
                ms.Writer.WriteFloat(matrix.M21); ms.Writer.WriteFloat(matrix.M22); ms.Writer.WriteFloat(matrix.M23); ms.Writer.WriteFloat(matrix.M24);
                ms.Writer.WriteFloat(matrix.M31); ms.Writer.WriteFloat(matrix.M32); ms.Writer.WriteFloat(matrix.M33); ms.Writer.WriteFloat(matrix.M34);
                ms.Writer.WriteFloat(matrix.M41); ms.Writer.WriteFloat(matrix.M42); ms.Writer.WriteFloat(matrix.M43); ms.Writer.WriteFloat(matrix.M44);
            }
        }
    }
}