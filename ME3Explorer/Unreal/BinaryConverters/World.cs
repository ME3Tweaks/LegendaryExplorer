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
    public class World
    {
        private int PersistentLevel;
        private int PersistentFaceFXAnimSet;
        private byte[] EditorViews; //112
        private int DecalManager;
        private int[] ExtraReferencedObjects;

        public World(ExportEntry export)
        {
            var ms = new MemoryStream(export.getBinaryData());
            PersistentLevel = ms.ReadInt32();
            if (export.Game == MEGame.ME3)
            {
                PersistentFaceFXAnimSet = ms.ReadInt32();
            }

            EditorViews = ms.ReadToBuffer(112);
            ms.SkipInt32();
            if (export.Game == MEGame.ME1)
            {
                DecalManager = ms.ReadInt32();
            }

            int count = ms.ReadInt32();
            ExtraReferencedObjects = new int[count];
            for (int i = 0; i < count; i++)
            {
                ExtraReferencedObjects[i] = ms.ReadInt32();
            }
        }

        public byte[] Write(MEGame game)
        {
            var ms = new MemoryStream();
            ms.WriteInt32(PersistentLevel);
            if (game == MEGame.ME3)
            {
                ms.WriteInt32(PersistentFaceFXAnimSet);
            }
            ms.WriteFromBuffer(EditorViews);
            ms.WriteInt32(0);
            if (game == MEGame.ME1)
            {
                ms.WriteInt32(DecalManager);
            }
            ms.WriteInt32(ExtraReferencedObjects.Length);
            foreach (int i in ExtraReferencedObjects)
            {
                ms.WriteInt32(i);
            }

            return ms.ToArray();
        }
    }
}
