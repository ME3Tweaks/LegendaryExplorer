using ME3ExplorerCore.Packages;
using ME3ExplorerCore.SharpDX;
using ME3ExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer.PackageEditor.Experiments
{
    /// <summary>
    /// Class for 'Others' package experiments who aren't main devs
    /// </summary>
    public class PackageEditorExperimentsO
    {
        public static void DumpPackageToT3D(IMEPackage package)
        {
            var levelExport = package.Exports.FirstOrDefault(x => x.ObjectName == "Level" && x.ClassName == "PersistentLevel");
            if (levelExport != null)
            {
                var level = ObjectBinary.From<Level>(levelExport);
                foreach (var actoruindex in level.Actors)
                {
                    if (package.TryGetUExport(actoruindex.value, out var actorExport))
                    {
                        switch (actorExport.ClassName)
                        {
                            case "StaticMesh":
                                var sm = ObjectBinary.From<StaticMesh>(actorExport);
                                
                                // Look at vars in sm to find what you need
                                //ExportT3D(sm, "FILENAMEHERE.txt", null); //??
                                break;
                            case "StaticMeshCollectionActor":

                                break;
                        }

                    }
                }
            }
        }

        // Might already be in ME3EXP?
        //A function for converting radians to unreal rotation units (necessary for UDK)
        private static float RadianToUnrealDegrees(float Angle)
        {
            return Angle * (32768 / 3.1415f);
        }

        // Might already be in ME3EXP?
        //A function for converting radians to degrees
        private static float RadianToDegrees(float Angle)
        {
            return Angle * (180 / 3.1415f);
        }

        public static void ExportT3D(StaticMesh staticMesh, string Filename, Matrix m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3((float)Math.Atan2(m.M32, m.M33), (float)Math.Asin(-1 * m.M31), (float)Math.Atan2(-1 * m.M21, m.M11));
            float RotatorX = Rotator.X;
            RotatorX = RadianToDegrees(RotatorX);
            float RotatorY = Rotator.Y;
            RotatorY = RadianToDegrees(RotatorY);
            float RotatorZ = Rotator.Z;
            RotatorZ = RadianToDegrees(RotatorZ);

            Vector3 Location = new Vector3(m.M41, m.M42, m.M43);

            //Only rotation, location, scale, actor name and model name are needed for a level recreation, everything else is just a placeholder
            //Need to override ToString to use US CultureInfo to avoid "commas instead of dots" bug
            //Indexes here is just to make names unique
            if (staticMesh != null)
            {
                Writer.WriteLine($"Begin Actor Class=StaticMeshActor Name={staticMesh.Export.ObjectName} Archetype=StaticMeshActor'/Script/Engine.Default__StaticMeshActor'");
                Writer.WriteLine("        Begin Object Class=StaticMeshComponent Name=\"StaticMeshComponent0\" Archetype=StaticMeshComponent'/Script/Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                Writer.WriteLine("        End Object");
                Writer.WriteLine("        Begin Object Name=\"StaticMeshComponent0\"");
                Writer.WriteLine("            StaticMesh=StaticMesh'/Game/ME3/ME3Architecture/Static/" + staticMesh.Export.ObjectName + "." + staticMesh.Export.ObjectName + "'"); //oriignal code was duplicated
                Writer.WriteLine("            RelativeLocation=(X=" + Location.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Y=" + Location.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Z=" + Location.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("            RelativeRotation=(Pitch=" + RotatorY.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Yaw=" + RotatorZ.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Roll=" + RotatorX.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("            RelativeScale3D=(X=" + IncScale3D.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Y=" + IncScale3D.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Z=" + IncScale3D.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("        End Object");
                Writer.WriteLine("        StaticMeshComponent=StaticMeshComponent0");
                Writer.WriteLine($"        ActorLabel=\"{staticMesh.Export.ObjectName}\"");
                Writer.WriteLine("End Actor");
            }
            Writer.Close();
        }


        //UDK version, need to figure out how to apply rotation properly
        public static void ExportT3D_UDK(StaticMesh STM, string Filename, Matrix m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3((float)Math.Atan2(m.M32, m.M33), (float)Math.Asin(-1 * m.M31), (float)Math.Atan2(-1 * m.M21, m.M11));
            float RotatorX = Rotator.X;
            RotatorX = RadianToUnrealDegrees(RotatorX);
            float RotatorY = Rotator.Y;
            RotatorY = RadianToUnrealDegrees(RotatorY);
            float RotatorZ = Rotator.Z;
            RotatorZ = RadianToUnrealDegrees(RotatorZ);

            Vector3 Location = new Vector3(m.M41, m.M42, m.M43);

            if (STM != null)
            {
                Writer.WriteLine("      Begin Actor Class=StaticMeshActor Name=STMC_" + STM.Export.ObjectName.Number + " Archetype=StaticMeshActor'Engine.Default__StaticMeshActor'");
                Writer.WriteLine("          Begin Object Class=StaticMeshComponent Name=STMC_" + STM.Export.ObjectName.Number + " ObjName=" + STM.Export.ObjectName.Instanced + " Archetype=StaticMeshComponent'Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                Writer.WriteLine("              StaticMesh=StaticMesh'A_Cathedral.Static." + STM.Export.ObjectName + "'");
                Writer.WriteLine("              LODData(0)=");
                Writer.WriteLine("              VertexPositionVersionNumber=1");
                Writer.WriteLine("              ReplacementPrimitive=None");
                Writer.WriteLine("              bAllowApproximateOcclusion=True");
                Writer.WriteLine("              bForceDirectLightMap=True");
                Writer.WriteLine("              bUsePrecomputedShadows=True");
                Writer.WriteLine("              LightingChannels=(bInitialized=True,Static=True)");
                Writer.WriteLine("              Name=\"" + STM.Export.ObjectName + "_" + STM.Export.ObjectName.Number + "\"");
                Writer.WriteLine("              ObjectArchetype=StaticMeshComponent'Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                Writer.WriteLine("          End Object");
                Writer.WriteLine("          StaticMeshComponent=StaticMeshComponent'" + STM.Export.ObjectName.Instanced + "'");
                Writer.WriteLine("          Components(0)=StaticMeshComponent'" + STM.Export.ObjectName.Instanced + "'");
                Writer.WriteLine("          Location=(X=" + Location.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Y=" + Location.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Z=" + Location.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("          Rotation=(Pitch=" + RotatorY.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                "Yaw=" + RotatorZ.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Roll=" + RotatorX.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("          DrawScale=(X=" + IncScale3D.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Y=" + IncScale3D.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "," +
                "Z=" + IncScale3D.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + ")");
                Writer.WriteLine("          CreationTime=1.462282");
                Writer.WriteLine("          Tag=\"StaticMeshActor\"");
                Writer.WriteLine("          CollisionComponent=StaticMeshComponent'" + STM.Export.ObjectName + "'");
                Writer.WriteLine("          Name=\"STMC_" + STM.Export.ObjectName.Number.ToString("D") + "\"");
                Writer.WriteLine("          ObjectArchetype=StaticMeshActor'Engine.Default__StaticMeshActor'");
                Writer.WriteLine("      End Actor");
            }
            Writer.Close();
        }



        //an attempt to recreate the assembling process in MaxScript similar to unreal t3d
        //Rotation is buggy, doesn't properly for now
        public static void ExportT3D_MS(StaticMesh STM, string Filename, Matrix m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3((float)Math.Atan2(m.M32, m.M33), (float)Math.Asin(-1 * m.M31), (float)Math.Atan2(-1 * m.M21, m.M11));
            float RotatorX = Rotator.X;
            RotatorX = RadianToDegrees(RotatorX);
            float RotatorY = Rotator.Y;
            RotatorY = RadianToDegrees(RotatorY);
            float RotatorZ = Rotator.Z;
            RotatorZ = RadianToDegrees(RotatorZ);

            Vector3 Location = new Vector3(m.M41, m.M42, m.M43);

            if (STM != null)
            {
                Writer.WriteLine($"{STM.Export.ObjectName} = instance ${STM.Export.ObjectName}");
                Writer.WriteLine($"{STM.Export.ObjectName}.name = \"{STM.Export.ObjectName}\" --name the copy as \"{STM.Export.ObjectName}\"");
                Writer.WriteLine("$" + STM.Export.ObjectName + ".Position=[" + Location.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    ", " + Location.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    ", " + Location.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "]");
                Writer.WriteLine("$" + STM.Export.ObjectName + ".scale=[" + IncScale3D.X.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    ", " + IncScale3D.Y.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    ", " + IncScale3D.Z.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) + "]");
                Writer.WriteLine("--Setting the rotation");
                Writer.WriteLine("fn SetObjectRotation obj rx ry rz =");
                Writer.WriteLine("(");
                Writer.WriteLine("-- Reset the object's transformation matrix so that");
                Writer.WriteLine("-- it only includes position and scale information.");
                Writer.WriteLine("-- Doing this clears out any previous object rotation.");
                Writer.WriteLine("local translateMat = transMatrix obj.transform.pos");
                Writer.WriteLine("local scaleMat = scaleMatrix obj.transform.scale");
                Writer.WriteLine("obj.transform = scaleMat * translateMat");
                Writer.WriteLine("-- Perform each axis rotation individually");
                Writer.WriteLine("rotate obj (angleaxis rx [1,0,0])");
                Writer.WriteLine("rotate obj (angleaxis ry [0,1,0])");
                Writer.WriteLine("rotate obj (angleaxis rz [0,0,1])");
                Writer.WriteLine(")");
                Writer.WriteLine("-- Set currently selected Object's rotation to " + RotatorX.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    " " + RotatorY.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    " " + RotatorZ.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("SetObjectRotation $" + STM.Export.ObjectName +
                    " " + RotatorX.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    " " + RotatorY.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")) +
                    " " + RotatorZ.ToString("F3", System.Globalization.CultureInfo.GetCultureInfo("en-US")));
                Writer.WriteLine("-------------------------------------------------------");
                Writer.WriteLine("-------------------------------------------------------");
            }
            Writer.Close();
        }
    }
}