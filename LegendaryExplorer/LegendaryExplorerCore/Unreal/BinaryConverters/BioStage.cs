using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.Collections;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioStage : ObjectBinary
    {
        public int length;
        public UMultiMap<NameReference, PropertyCollection> CameraList; //PropertyCollection is struct of type BioStageCamera, which contains nothing that needs relinking //TODO: Make this a UMap

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (!sc.Game.IsGame3())
            {
                if (sc.IsLoading)
                {
                    CameraList = new();
                }
                return;
            }
            long startPos = sc.ms.Position;
            sc.Serialize(ref length);
            if (length == 0 && (sc.IsLoading || CameraList.Count == 0))
            {
                if (sc.IsLoading)
                {
                    CameraList = new();
                }
                return;
            }

            NameReference arrayName = "m_aCameraList";
            sc.Serialize(ref arrayName);
            int dummy = 0;
            sc.Serialize(ref dummy);

            if (sc.IsLoading)
            {
                int count = sc.ms.ReadInt32();
                sc.ms.SkipInt32();
                CameraList = new(count);
                for (int i = 0; i < count; i++)
                {
                    CameraList.Add(sc.ms.ReadNameReference(sc.Pcc), PropertyCollection.ReadProps(Export, sc.ms.BaseStream, "BioStageCamera", true, entry: Export));
                }
            }
            else
            {
                sc.ms.Writer.WriteInt32(CameraList.Count);
                sc.ms.Writer.WriteInt32(0);
                foreach ((NameReference name, PropertyCollection props) in CameraList)
                {
                    sc.ms.Writer.WriteNameReference(name, sc.Pcc);
                    props.WriteTo(sc.ms.Writer, sc.Pcc);
                }
            }

            if (sc.IsSaving)
            {
                long endPos = sc.ms.Position;
                sc.ms.JumpTo(startPos);
                sc.ms.Writer.WriteInt32((int)(endPos - startPos - 4));
                sc.ms.JumpTo(endPos);
            }
        }

        public static BioStage Create()
        {
            return new()
            {
                CameraList = new ()
            };
        }

        //GetNames for this class must be implemented whereever ObjectBinary::GetNames is called
        //Gross, should fix this someday
    }
}
