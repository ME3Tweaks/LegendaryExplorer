using System.Collections.Generic;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class PhysicsAssetInstance : ObjectBinary
    {
        public List<(int, int)> CollisionDisableTable;

        protected override void Serialize(SerializingContainer sc)
        {
            sc.Serialize(ref CollisionDisableTable, (ref (int, int) indexPair) =>
            {
                if (sc.IsLoading)
                {
                    indexPair = (sc.ms.ReadInt32(), sc.ms.ReadInt32());
                }
                else
                {
                    sc.ms.Writer.WriteInt32(indexPair.Item1);
                    sc.ms.Writer.WriteInt32(indexPair.Item2);
                }
                sc.SerializeConstInt(0);
            });
        }

        public static PhysicsAssetInstance Create()
        {
            return new()
            {
                CollisionDisableTable = []
            };
        }
    }
}
