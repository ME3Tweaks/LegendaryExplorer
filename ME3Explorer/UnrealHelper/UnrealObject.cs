using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.UnrealHelper
{
    public class UnrealObject
    {
        public byte[] memory;
        public int memsize;
        public string objectclass;
        public string[] names;

        public UDecalActor UDecA;
        public UDecalComponent UDecC;
        public UInterpActor UIntA;
        public ULevel ULv;
        public USkelMesh USkel;
        public UStaticMesh UStat;
        public UStaticMeshActor UStatA;
        public UStaticMeshComponent UStatC;
        public UStaticMeshCollectionActor UStatCA;
        public UTexture2D UTex2D;
        public UMaterial UMat;
        public UWwiseStream UWws;
        public UnknownObject UUkn;
        
        
        public UnrealObject(byte[] mem, string Uclass,string[] Names)
        {
            objectclass = Uclass;
            names = Names;
            switch (objectclass)
            {
                case "DecalActor":
                    UDecA = new UDecalActor(mem, names);
                    break;
                case "DecalComponent":
                    UDecC = new UDecalComponent(mem, names);
                    break;
                case "InterpActor":
                    UIntA = new UInterpActor(mem, names);
                    break;
                case "StaticMesh":
                    UStat = new UStaticMesh(mem,names);
                    break;
                case "StaticMeshActor":
                    UStatA = new  UStaticMeshActor(mem, names);
                    break;
                case "StaticMeshComponent":
                    UStatC = new UStaticMeshComponent(mem, names);
                    break;                
                case "SkeletalMesh":
                    USkel = new USkelMesh(mem, names);
                    break;
                case "Texture2D":
                    UTex2D = new UTexture2D(mem, names);
                    break;       
                case "Material":
                    UMat = new UMaterial(mem, names);
                    break;
                case "WwiseStream":
                    UWws = new UWwiseStream(mem, names);
                    break;
                default:
                    UUkn = new UnknownObject(mem, objectclass, names);
                    break;
            }
        }

        public UnrealObject(byte[] mem, string Uclass, PCCFile pcc)
        {
            objectclass = Uclass;
            names = pcc.names;
            switch (objectclass)
            {
                case "Level":
                    ULv = new ULevel(mem, pcc);
                    break;
                case "StaticMeshCollectionActor":
                    UStatCA = new UStaticMeshCollectionActor(mem, pcc);
                    break;
                default:
                    UUkn = new UnknownObject(mem, objectclass, names);
                    break;
            }
        }

        public void Export(string path)
        {
            PSKFile PSKf;
            switch (objectclass)
            {
                case "StaticMesh":
                    if (UStat == null)
                        return;
                    PSKf = UStat.ExportToPsk();
                    PSKf.SaveToFile(path);
                    break;
                case "SkeletalMesh":
                    if (USkel == null)
                        return;
                    int max = USkel.Mesh.LODs.Count - 1;
                    string LODr = Microsoft.VisualBasic.Interaction.InputBox("Which LOD would you like to export (0-" + max.ToString() + ")", "ME3 Explorer", "0");
                    int LODs = Convert.ToInt32(LODr);
                    if(LODs>=0 && LODs <= max)
                        PSKf = USkel.ExportToPSK(LODs);
                    else
                        PSKf = USkel.ExportToPSK(LODs);
                    PSKf.SaveToFile(path);
                    break;
                case "Texture2D":
                    if (UTex2D == null)
                        return;
                    UTex2D.ExportToFile(path);
                    break;
            }

        }
    }
}
