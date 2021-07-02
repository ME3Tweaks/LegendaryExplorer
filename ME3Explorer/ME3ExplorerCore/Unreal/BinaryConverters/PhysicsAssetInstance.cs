using System.Collections.Generic;

namespace ME3ExplorerCore.Unreal.BinaryConverters
{
    public class PhysicsAssetInstance : ObjectBinary
    {
        public List<(int, int)> CollisionDisableTable;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref CollisionDisableTable, (SerializingContainer2 sc2, ref (int, int) indexPair) =>
            {
                if (sc2.IsLoading)
                {
                    indexPair = (sc2.ms.ReadInt32(), sc2.ms.ReadInt32());
                }
                else
                {
                    sc2.ms.Writer.WriteInt32(indexPair.Item1);
                    sc2.ms.Writer.WriteInt32(indexPair.Item2);
                }
                sc2.SerializeConstInt(0);
            });
        }
    }
}
