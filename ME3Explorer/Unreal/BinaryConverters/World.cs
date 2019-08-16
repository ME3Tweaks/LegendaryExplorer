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
            Serialize(new SerializingContainer2(new MemoryStream(export.getBinaryData()), true), export.Game);
        }

        public static World From(ExportEntry export)
        {
            return new World(export);
        }

        private void Serialize(SerializingContainer2 sc, MEGame game)
        {
            sc.Serialize(ref PersistentLevel);
            if (game == MEGame.ME3)
            {
                sc.Serialize(ref PersistentFaceFXAnimSet);
            }
            sc.Serialize(ref EditorViews, 112);
            int dummy = 0;
            sc.Serialize(ref dummy);
            if (game == MEGame.ME1)
            {
                sc.Serialize(ref DecalManager);
            }

            sc.Serialize(ref ExtraReferencedObjects);
        }

        public byte[] Write(MEGame game)
        {
            var ms = new MemoryStream();
            Serialize(new SerializingContainer2(ms), game);

            return ms.ToArray();
        }
    }
}
