using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    /*
        PersistentLevel: ULevel*
        #IF ME3
        PersistentFaceFXAnimSet: UFaceFXAnimSet*
        #ENDIF
        EditorViews[4] : FLevelViewportInfo
	        CamPosition: FVector
		        X: float
		        Y: float
		        Z: float
	        CamRotation: FRotator
		        Pitch: int
		        Yaw: int
		        Roll: int
	        CamOrthoZoom: float
        SaveGameSummary_DEPRECATED: UDEPRECATED_SaveGameSummary*
        #IF ME1
        DecalManager: UObject*
        #ENDIF
        ExtraReferencedObjects: TArray<UObject*>
     */
    public sealed class World : ObjectBinary
    {
        private UIndex PersistentLevel;
        private UIndex PersistentFaceFXAnimSet;
        private byte[] EditorViews; //112
        private UIndex DecalManager;
        private UIndex[] ExtraReferencedObjects;

        protected override void Serialize(SerializingContainer2 sc)
        {
            sc.Serialize(ref PersistentLevel);
            if (sc.Game == MEGame.ME3)
            {
                sc.Serialize(ref PersistentFaceFXAnimSet);
            }
            sc.Serialize(ref EditorViews, 112);
            int dummy = 0;
            sc.Serialize(ref dummy);
            if (sc.Game == MEGame.ME1)
            {
                sc.Serialize(ref DecalManager);
            }

            sc.Serialize(ref ExtraReferencedObjects);
        }
    }
}
