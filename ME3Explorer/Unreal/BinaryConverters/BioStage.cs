using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class BioStage : ObjectBinary
    {
        public int length;
        public OrderedMultiValueDictionary<NameReference, PropertyCollection> CameraList; //PropertyCollection is struct of type BioStageCamera, which contains nothing that needs relinking

        protected override void Serialize(SerializingContainer2 sc)
        {
            if (sc.Game != MEGame.ME3)
            {
                if (sc.IsLoading)
                {
                    CameraList = new OrderedMultiValueDictionary<NameReference, PropertyCollection>();
                }
                return;
            }
            long startPos = sc.ms.Position;
            sc.Serialize(ref length);
            if (length == 0 && (sc.IsLoading || CameraList.Count == 0))
            {
                if (sc.IsLoading)
                {
                    CameraList = new OrderedMultiValueDictionary<NameReference, PropertyCollection>();
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
                CameraList = new OrderedMultiValueDictionary<NameReference, PropertyCollection>(count);
                for (int i = 0; i < count; i++)
                {
                    CameraList.Add(sc.ms.ReadNameReference(sc.Pcc), PropertyCollection.ReadProps(Export, sc.ms, "BioStageCamera", true, entry: Export));
                }
            }
            else
            {
                sc.ms.WriteInt32(CameraList.Count);
                sc.ms.WriteInt32(0);
                foreach ((NameReference name, PropertyCollection props) in CameraList)
                {
                    sc.ms.WriteNameReference(name, sc.Pcc);
                    props.WriteTo(sc.ms, sc.Pcc);
                }
            }

            if (sc.IsSaving)
            {
                long endPos = sc.ms.Position;
                sc.ms.JumpTo(startPos);
                sc.ms.WriteInt32((int)(endPos - startPos - 4));
                sc.ms.JumpTo(endPos);
            }
        }
    }
}
