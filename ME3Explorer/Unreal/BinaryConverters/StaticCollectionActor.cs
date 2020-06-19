using System.Collections.Generic;
using System.Linq;
using ME3Explorer.Packages;
using SharpDX;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public abstract class StaticCollectionActor : ObjectBinary
    {
        public List<UIndex> Components;

        public List<Matrix> LocalToWorldTransforms;

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
                Components = components.Select(x => new UIndex(x.Value)).ToList();
                LocalToWorldTransforms = new List<Matrix>(components.Count);
            }

            for (int i = 0; i < components.Count; i++)
            {
                Matrix m = i < LocalToWorldTransforms.Count ? LocalToWorldTransforms[i] : Matrix.Identity;
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
        public override string ComponentPropName { get; } = "StaticMeshComponents";
    }
    public class StaticLightCollectionActor : StaticCollectionActor
    {
        public override string ComponentPropName { get; } = "LightComponents";
    }
}
namespace ME3Explorer
{
    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref Matrix matrix)
        {
            if (sc.IsLoading)
            {
                matrix = new Matrix(sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(),
                                    sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(),
                                    sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(),
                                    sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat(), sc.ms.ReadFloat());
            }
            else
            {
                sc.ms.Writer.WriteFloat(matrix[0, 0]); sc.ms.Writer.WriteFloat(matrix[0, 1]); sc.ms.Writer.WriteFloat(matrix[0, 2]); sc.ms.Writer.WriteFloat(matrix[0, 3]);
                sc.ms.Writer.WriteFloat(matrix[1, 0]); sc.ms.Writer.WriteFloat(matrix[1, 1]); sc.ms.Writer.WriteFloat(matrix[1, 2]); sc.ms.Writer.WriteFloat(matrix[1, 3]);
                sc.ms.Writer.WriteFloat(matrix[2, 0]); sc.ms.Writer.WriteFloat(matrix[2, 1]); sc.ms.Writer.WriteFloat(matrix[2, 2]); sc.ms.Writer.WriteFloat(matrix[2, 3]);
                sc.ms.Writer.WriteFloat(matrix[3, 0]); sc.ms.Writer.WriteFloat(matrix[3, 1]); sc.ms.Writer.WriteFloat(matrix[3, 2]); sc.ms.Writer.WriteFloat(matrix[3, 3]);
            }
        }
    }
}