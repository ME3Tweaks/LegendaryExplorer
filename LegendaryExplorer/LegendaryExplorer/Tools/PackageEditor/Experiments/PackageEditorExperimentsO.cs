using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Windows;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Class for 'Others' package experiments who aren't main devs
    /// </summary>
    class PackageEditorExperimentsO
    {
        public static void DumpPackageToT3D(IMEPackage package)
        {
            var levelExport = package.Exports.FirstOrDefault(x => x.ObjectName == "Level" && x.ClassName == "PersistentLevel");
            if (levelExport != null)
            {
                var level = ObjectBinary.From<Level>(levelExport);
                foreach (var actoruindex in level.Actors)
                {
                    if (package.TryGetUExport(actoruindex, out var actorExport))
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

        public static void ExportT3D(StaticMesh staticMesh, string Filename, Matrix4x4 m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3(MathF.Atan2(m.M32, m.M33), MathF.Asin(-1 * m.M31), MathF.Atan2(-1 * m.M21, m.M11));
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
        public static void ExportT3D_UDK(StaticMesh STM, string Filename, Matrix4x4 m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3(MathF.Atan2(m.M32, m.M33), MathF.Asin(-1 * m.M31), MathF.Atan2(-1 * m.M21, m.M11));
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
        public static void ExportT3D_MS(StaticMesh STM, string Filename, Matrix4x4 m, Vector3 IncScale3D)
        {
            StreamWriter Writer = new StreamWriter(Filename, true);

            Vector3 Rotator = new Vector3(MathF.Atan2(m.M32, m.M33), MathF.Asin(-1 * m.M31), MathF.Atan2(-1 * m.M21, m.M11));
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

        public static void AddPresetGroup(string preset, PackageEditorWindow pew)
        {
            if (pew.SelectedItem != null && pew.SelectedItem.Entry != null && pew.Pcc != null)
            {
                var game = pew.Pcc.Game;

                if (!(game.IsGame3() || game.IsGame2()))
                {
                    MessageBox.Show("This experiment is not available for ME1/LE1 files.", "Warning", MessageBoxButton.OK);
                    return;
                }

                if (pew.SelectedItem.Entry.ClassName != "InterpData")
                {
                    MessageBox.Show("InterpData not selected.", "Warning", MessageBoxButton.OK);
                    return;
                }

                if (pew.SelectedItem.Entry is not ExportEntry interp)
                    return;

                switch (preset)
                {
                    case "Director":
                        MatineeHelper.AddPreset(preset, interp, game);
                        break;

                    case "Camera":
                        var camActor = promptForActor("Name of camera actor:", "Not a valid camera actor name.");
                        if (!string.IsNullOrEmpty(camActor))
                        {
                            MatineeHelper.AddPreset(preset, interp, game, camActor);
                        }
                        break;

                    case "Actor":
                        var actActor = promptForActor("Name of actor:", "Not a valid actor name.");
                        if (!string.IsNullOrEmpty(actActor))
                        {
                            MatineeHelper.AddPreset(preset, interp, game, actActor);
                        }
                        break;
                }
            }
            return;
        }

        public static void AddPresetTrack(string preset, PackageEditorWindow pew)
        {
            if (pew.SelectedItem != null && pew.SelectedItem.Entry != null && pew.Pcc != null)
            {
                var game = pew.Pcc.Game;

                if (!(game.IsGame3() || game.IsGame2()))
                {
                    MessageBox.Show("This experiment is not available for ME1/LE1 files.", "Warning", MessageBoxButton.OK);
                    return;
                }

                if (pew.SelectedItem.Entry.ClassName != "InterpGroup")
                {
                    MessageBox.Show("InterpGroup not selected.", "Warning", MessageBoxButton.OK);
                    return;
                }

                if (pew.SelectedItem.Entry is not ExportEntry interp)
                    return;

                switch (preset)
                {
                    case "Gesture":
                    case "Gesture2":
                        var actor = promptForActor("Name of gesture actor:", "Not a valid gesture actor name.");
                        if (!string.IsNullOrEmpty(actor))
                        {
                            MatineeHelper.AddPreset(preset, interp, game, actor);
                        }
                        break;
                }
            }
            return;
        }

        private static string promptForActor(string msg, string err)
        {
            if (PromptDialog.Prompt(null, msg) is string actor)
            {
                if (string.IsNullOrEmpty(actor))
                {
                    MessageBox.Show(err, "Warning", MessageBoxButton.OK);
                    return null;
                }
                return actor;
            }
            return null;
        }

        /// <summary>
        /// Batch patch parameters in a list of materials
        /// </summary>
        /// <param name="pew">Current PE window</param>
        public static void BatchPatchMaterialsParameters(PackageEditorWindow pew)
        {
            // --DATA GATHERING--
            // Get game to patch
            string gameString = InputComboBoxDialog.GetValue(null, "Choose game to patch a material for:", "Select game to patch",
                new[] { "LE3", "LE2", "LE1", "ME3", "ME2", "ME1" }, "LE3");
            if (string.IsNullOrEmpty(gameString)) { return; }

            if (Enum.TryParse(gameString, out MEGame game))
            {
                // Get DLC to patch
                // The user must put the files to patch in the DLC folder. This helps avoid mount priority headaches
                string dlc = PromptDialog.Prompt(null, "Write the name of the DLC containing the files to patch");
                if (string.IsNullOrEmpty(dlc))
                {
                    ShowError("Invalid DLC name");
                    return;
                }
                string dlcPath = Path.Combine(MEDirectories.GetDLCPath(game), $@"{dlc}\CookedPCConsole");
                if (!Directory.Exists(dlcPath))
                {
                    ShowError($"The {dlc} DLC could not be found in the {game} directory.");
                    return;
                }

                // Get materials to patch
                string materialsString = PromptDialog.Prompt(null, "Write a comma separated list of materials to patch");
                if (string.IsNullOrEmpty(materialsString))
                {
                    ShowError("Invalid material list");
                    return;
                }
                string[] materials = materialsString.Split(",");

                // Get whether to patch vector or scalar parameters
                string parameterType = InputComboBoxDialog.GetValue(null, "Patch vector or scalar parameters?", "Patch vector or scalar",
                    new[] { "Vector", "Scalar" }, "Vector");
                if (string.IsNullOrEmpty(parameterType)) { return; }

                // Get parameters and values to patch
                string paramsAndValsString = PromptDialog.Prompt(null,
                    "Write a list of parameters and values to patch, in the following form:\n" +
                    "<paramName1>:<values(comma separated)>;<paramName2>:<values(comma separated)>...\n\n" +
                    "Example: HighlightColor1: 1.2, 3, 4, 5.32; HED_HAIR_Colour_Vector: 1, 0.843, 1, 1\n" +
                    "Use periods for decimals, not commas."
                    );
                if (string.IsNullOrEmpty(paramsAndValsString))
                {
                    ShowError("Invalid material list");
                    return;
                }

                Dictionary<string, List<float>> paramsAndVals = new();

                foreach (string s in paramsAndValsString.Split(";"))
                { // Check that all strings are <parameter>:<values>
                    if (!s.Contains(":"))
                    {
                        ShowError("Wrong formatting for parameter and values");
                        return;
                    }

                    // Validate values
                    string[] temp = s.Split(":");
                    string param = temp[0].Trim();
                    string[] valsString = temp[1].Split(",");
                    // Check that the values are correct
                    if (parameterType.Equals("Vector") && valsString.Length != 4)
                    {
                        ShowError("Vector parameter values must be 4 per parameter, in the form of \"R,G,B,A\"");
                        return;
                    }
                    else if (parameterType.Equals("Scalar") && valsString.Length != 1)
                    {
                        ShowError("Scalar parameter values must be 1 per parameter");
                        return;
                    }
                    List<float> vals = new();
                    foreach (string val in valsString)
                    {
                        bool res = float.TryParse(val.Trim(), out float d);
                        if (!res)
                        {
                            ShowError($"Error parsing the value \"{val.Trim()}\" for the \"{param}\" parameter");
                            return;
                        }
                        vals.Add(d);
                    }

                    paramsAndVals.Add(param, vals);
                }

                // --PATCHING--
                // Iterate through the files
                foreach (string file in Directory.EnumerateFiles(dlcPath, "*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f).Equals(".pcc")))
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(file);
                    // Check if it the file contains the materials to patch
                    foreach (string targetMat in materials)
                    {
                        List<ExportEntry> pccMats = pcc.Exports.Where(exp => exp.ClassName == "MaterialInstanceConstant" && exp.ObjectName == targetMat.Trim()).ToList();

                        // Iterate through the usages of the material
                        foreach (ExportEntry mat in pccMats)
                        {
                            string paramTypeName = $"{(parameterType.Equals("Vector") ? "VectorParameterValues" : "ScalarParameterValues")}";
                            ArrayProperty<StructProperty> oldList = mat.GetProperty<ArrayProperty<StructProperty>>(paramTypeName);
                            mat.RemoveProperty(paramTypeName);

                            // Iterate through the parameters to patch
                            foreach (KeyValuePair<string, List<float>> pAv in paramsAndVals)
                            {
                                // Filter the parameter from the properties list
                                List<StructProperty> filtered = oldList.Where(property =>
                                {
                                    NameProperty nameProperty = (NameProperty)property.Properties.Where(prop => prop.Name.Equals("ParameterName")).First();
                                    string name = nameProperty.Value;
                                    if (string.IsNullOrEmpty(name)) { return false; };
                                    return !name.Equals(pAv.Key, StringComparison.OrdinalIgnoreCase);
                                }).ToList();

                                PropertyCollection props = new();

                                // Generate and add the ExpressionGUID
                                PropertyCollection expressionGUIDprops = new();
                                expressionGUIDprops.Add(new IntProperty(0, "A"));
                                expressionGUIDprops.Add(new IntProperty(0, "B"));
                                expressionGUIDprops.Add(new IntProperty(0, "C"));
                                expressionGUIDprops.Add(new IntProperty(0, "D"));

                                props.Add(new StructProperty("Guid", expressionGUIDprops, "ExpressionGUID", true));

                                if (parameterType.Equals("Vector"))
                                {
                                    PropertyCollection color = new();
                                    color.Add(new FloatProperty(pAv.Value[0], "R"));
                                    color.Add(new FloatProperty(pAv.Value[1], "G"));
                                    color.Add(new FloatProperty(pAv.Value[2], "B"));
                                    color.Add(new FloatProperty(pAv.Value[3], "A"));

                                    props.Add(new StructProperty("LinearColor", color, "ParameterValue", true));
                                    props.Add(new NameProperty(pAv.Key, "ParameterName"));

                                    filtered.Add(new StructProperty("VectorParameterValue", props));
                                }
                                else
                                {
                                    props.Add(new NameProperty(pAv.Key, "ParameterName"));
                                    props.Add(new FloatProperty(pAv.Value[0], "ParameterValue"));

                                    filtered.Add(new StructProperty("ScalarParameterValue", props));
                                }

                                ArrayProperty<StructProperty> newList = new(paramTypeName);
                                foreach (StructProperty prop in filtered) { newList.Add(prop); }
                                mat.WriteProperty(newList);
                                oldList = newList;
                            }
                        }
                    }
                    pcc.Save();
                }
            }

            MessageBox.Show("All materials were sucessfully patched", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Batch set the value of a property
        /// </summary>
        /// <param name="pew">Current PE window</param>
        public static void BatchSetBoolPropVal(PackageEditorWindow pew)
        {
            if (pew.Pcc == null) { return; }

            // Get game to patch
            string gameString = InputComboBoxDialog.GetValue(null, "Choose game to set bools for:", "Batch set bools",
                new[] { "LE3", "LE2", "LE1", "ME3", "ME2", "ME1" }, "LE3");
            if (string.IsNullOrEmpty(gameString)) { return; }

            if (Enum.TryParse(gameString, out MEGame game))
            {
                // Get DLC to patch
                // The user must put the files to patch in the DLC folder. This helps avoid mount priority headaches
                string dlc = PromptDialog.Prompt(null, "Write the name of the DLC containing the files to patch");
                if (string.IsNullOrEmpty(dlc))
                {
                    ShowError("Invalid DLC name");
                    return;
                }
                string dlcPath = Path.Combine(MEDirectories.GetDLCPath(game), $@"{dlc}\CookedPCConsole");
                if (!Directory.Exists(dlcPath))
                {
                    ShowError($"The {dlc} DLC could not be found in the {game} directory");
                    return;
                }

                // Get class name containing the property to set
                string className = PromptDialog.Prompt(null, "Write the name of the class containing the bool property. It is case sensitive");
                if (string.IsNullOrEmpty(className))
                {
                    ShowError("Invalid class name");
                    return;
                }

                // Get name of bool property to set
                string boolName = PromptDialog.Prompt(null, "Write the name of the bool property to set. It is case sensitive");
                if (string.IsNullOrEmpty(boolName))
                {
                    ShowError("Invalid bool property name");
                    return;
                }

                // Get the state to set the booleans to
                string stateString = InputComboBoxDialog.GetValue(null, "State to set the bools to", "Bool state",
                    new[] { "True", "False" }, "True");
                if (string.IsNullOrEmpty(stateString)) { return; }
                bool state = stateString.Equals("True");


                foreach (string file in Directory.EnumerateFiles(dlcPath, "*", SearchOption.AllDirectories).Where(f => Path.GetExtension(f).Equals(".pcc")))
                {
                    using IMEPackage pcc = MEPackageHandler.OpenMEPackage(file);
                    List<ExportEntry> exports = pcc.Exports.Where(export => export.ClassName.Equals(className)).ToList();

                    foreach (ExportEntry export in exports)
                    {
                        BoolProperty currProp = export.GetProperty<BoolProperty>(boolName);
                        if (currProp == null) { continue; }
                        export.RemoveProperty(boolName);
                        export.WriteProperty(new BoolProperty(state, boolName));
                    }
                    pcc.Save();
                }
            }
            MessageBox.Show("All bools were sucessfully set", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Modify the hair morph targets of a male headmorph to make it bald
        /// </summary>
        /// <param name="pew">Current PE window</param>
        public static void Baldinator(PackageEditorWindow pew)
        {
            if (pew.SelectedItem == null || pew.SelectedItem.Entry == null || pew.Pcc == null) { return; }

            if (pew.SelectedItem.Entry.ClassName != "BioMorphFace")
            {
                ShowError("Selected item is not a BioMorphFace");
                return;
            }

            MEGame game = pew.Pcc.Game;

            // Get the export that we'll use to find the vertices to modify
            string morphTargetsPccPath = Path.Combine(MEDirectories.GetCookedPath(game), "BIOG_HMM_HED_PROMorph.pcc");
            if (!File.Exists(morphTargetsPccPath))
            {
                ShowError("Could not find the BIOG_HMM_HED_PROMorph file. Please ensure the vanilla game files have not been modified.");
                return;
            }
            using IMEPackage morphTargetsPcc = MEPackageHandler.OpenMEPackage(morphTargetsPccPath);

            ExportEntry morphTargetSet = morphTargetsPcc.FindExport("HMM_BaseMorphSet");
            if (morphTargetSet == null || morphTargetSet.ClassName != "MorphTargetSet")
            {
                ShowError("Could not find the morph targets in BIOG_HMM_HED_PROMorph. Please ensure the vanilla game files have not been modified.");
                return;
            }

            // Get the export that we'll use as the values to set the target like
            string baldPccPath = Path.Combine(MEDirectories.GetCookedPath(game),
                game.IsGame3() ? "BioD_CitHub_Underbelly.pcc" : game.IsGame2() ? "BioH_Wilson.pcc" : "BIOA_FRE32_00_DSG.pcc");
            if (!File.Exists(baldPccPath))
            {
                ShowError($"Could not find the {Path.GetFileNameWithoutExtension(baldPccPath)} file. Please ensure the vanilla game files have not been modified.");
                return;
            }
            using IMEPackage baldPcc = MEPackageHandler.OpenMEPackage(baldPccPath);

            ExportEntry baldMorph = baldPcc.FindExport(game.IsGame3() ? "BioChar_CitHub.Faces.HMM_Deco_1" :
                game.IsGame2() ? "BIOG_Hench_FAC.HMM.hench_wilson" : "BIOA_UNC_FAC.HMM.Plot.FRE32_BioticLeader");
            if (baldMorph == null || baldMorph.ClassName != "BioMorphFace")
            {
                ShowError($"Could not find the bald headmorph. Please ensure the vanilla game files have not been modified.");
                return;
            }

            ExportEntry targetMorph = (ExportEntry)pew.SelectedItem.Entry;

            BioMorphFace baldMorphFace = ObjectBinary.From<BioMorphFace>(baldMorph);
            BioMorphFace targetMorphFace = ObjectBinary.From<BioMorphFace>(targetMorph);

            if (baldMorphFace.LODs[0].Length != targetMorphFace.LODs[0].Length)
            {
                ShowError($"The selected headmorph differs from the expected one. This experiment only works for male human headmorphs.");
                return;
            }

            List<string> targetNames = new List<string>()
                {
                    "Afro",
                    "Buzzcut",
                    "BuzzCut_WidowsPeak",
                    "Deiter",
                    "Greezer",
                    "HAIR_pulledbackslick",
                    "HAIR_sidepart",
                    "HAIR_slickWidowsPeak",
                    "HAIR_pulledBackBig",
                    "Hair_splitSide",
                    "HAIR_centerPart",
                    "Flattop",
                    "flattop_widowspeak",
                    "Eastwood",
                    "widowsPeak",
                    "rollins",
                    "straightHairLine",
                };

            // Get a list of the hair morph targets that exist in the targetMorph morphFeatures property.
            // If the property does not exist or no feature matches the targetNames, we'll use all the default hair targets.
            List<string> targetsInMorph = targetNames.Where(targetName =>
            {
                ArrayProperty<StructProperty> features = targetMorph.GetProperty<ArrayProperty<StructProperty>>("m_aMorphFeatures");
                if (features.Any())
                {
                    // Compare the targetName against the names of the features
                    List<string> featureNames = features.Select(feature => feature.GetProp<NameProperty>("sFeatureName").Value.Name).ToList();
                    return featureNames.Exists(featureName => string.Equals(featureName, targetName, StringComparison.OrdinalIgnoreCase));
                }
                else { return true; }
            }).ToList();

            if (targetsInMorph.Count == 0) { targetsInMorph = targetNames; }

            // Collect the vertex indices from the targets
            List<int>[] indices = morphTargetSet.GetProperty<ArrayProperty<ObjectProperty>>("Targets")
                .Select(prop => morphTargetsPcc.GetUExport(prop.Value))
                .Where(entry => targetsInMorph.Exists(name => string.Equals(entry.ObjectName, name, StringComparison.OrdinalIgnoreCase)))
                .Aggregate(new List<int>[2], (lods, t) =>
                {
                    MorphTarget target = ObjectBinary.From<MorphTarget>(t);
                    for (int i = 0; i < 2; i++) // Adds indices for two lods, since the morphTargets only have two lod models
                    {
                        List<int> lod = new();
                        foreach (MorphTarget.MorphVertex vertex in target.MorphLODModels[i].Vertices)
                        {
                            if (!lod.Contains(vertex.SourceIdx) && (vertex.SourceIdx != 495)) // 495 is a nose vertex
                            {
                                lod.Add(vertex.SourceIdx);
                            }
                        }
                        lods[i] = lod;
                    }
                    return lods;
                });

            // Modify the targetMorph based on the baldMorph
            for (int l = 0; l < indices.Length; l++)
            {
                List<int> lod = indices[l];
                foreach (int i in lod)
                {
                    targetMorphFace.LODs[l][i].X = baldMorphFace.LODs[l][i].X;
                    targetMorphFace.LODs[l][i].Y = baldMorphFace.LODs[l][i].Y;
                    targetMorphFace.LODs[l][i].Z = baldMorphFace.LODs[l][i].Z;
                }

            }

            targetMorph.WriteBinary(targetMorphFace);
            MessageBox.Show("The selected morph's hair has been cut and offered as a sacrifice to the Reape-- The morph is now bald.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Removes SMC references to a SkeletalMesh or StaticMesh within a given distance.
        /// </summary>
        /// <param name="pew">Current PE instance.</param>
        public static void SMRefRemover(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.SelectedItem.Entry.ClassName is not ("SkeletalMesh" or "StaticMesh"))
            {
                ShowError("Selected export is not a SkeletalMesh or StaticMesh");
                return;
            }

            // Prompt for and validate origin position
            string promptOrigin = PromptDialog.Prompt(null, "Write the origin coordinates from which to remove references to the mesh. It must be a comma separated list: X, Y, Z");
            if (string.IsNullOrEmpty(promptOrigin))
            {
                ShowError("Invalid origin");
                return;
            }

            string[] strOrigins = promptOrigin.Split(",");
            if (strOrigins.Length != 3)
            {
                ShowError("Wrong number of coordinates. You must provide 3 coordinates in the form of: X, Y, Z");
                return;
            }
            List<float> origins = new();
            foreach (string strOrigin in strOrigins)
            {
                if (!float.TryParse(strOrigin, out float origin))
                {
                    ShowError($"Distance {strOrigin} is not a valid decimal");
                    return;
                }
                origins.Add(origin);
            }

            // Prompt for and validate distance
            string promptDist = PromptDialog.Prompt(null, "Write the distance from each origin coordinate in which to remove references. It must be a comma separated list: distX, distY, distZ");
            if (string.IsNullOrEmpty(promptDist))
            {
                ShowError("Invalid distance");
                return;
            }

            string[] strDists = promptDist.Split(",");
            if (strDists.Length != 3)
            {
                ShowError("Wrong number of distances. You must provide 3 distances, in the form of: distX, distY, distZ");
                return;
            }
            List<float> dists = new();
            foreach (string strDist in strDists)
            {
                if (!float.TryParse(strDist, out float dist))
                {
                    ShowError($"Distance {strDist} is not a valid decimal");
                    return;
                }
                if (dist < 0)
                {
                    ShowError($"Distance {strDist} must be a positive value.");
                    return;
                }
                dists.Add(dist);
            }

            var mesh = (ExportEntry)pew.SelectedItem.Entry;
            // Get a list of SMC/SMAs referencing the selected mesh
            List<IEntry> references = mesh.GetEntriesThatReferenceThisOne()
                .Where(kvp => kvp.Key.ClassName is "SkeletalMeshComponent" or "StaticMeshComponent")
                .Select(kvp => kvp.Key).ToList();

            if (references.Count == 0)
            {
                ShowError("Found no SMCs referencing the selected mesh");
                return;
            }

            List<string> removedReferences = new();
            foreach (var entry in references)
            {
                var reference = entry as ExportEntry;
                if (reference?.ClassName == "StaticMeshComponent")
                {
                    switch (reference.Parent.ClassName)
                    {
                        case "StaticMeshCollectionActor":
                            var parent = ObjectBinary.From<StaticMeshCollectionActor>((ExportEntry)reference.Parent);
                            int smcaIndex = parent.Components.IndexOf(reference.UIndex);
                            ((float destX, float destY, float destZ), _, _) = parent.LocalToWorldTransforms[smcaIndex].UnrealDecompose();

                            // Check if the component is within the given distance, and if so remove the reference
                            if (InDist(origins[0], destX, dists[0]) && InDist(origins[1], destY, dists[1]) && InDist(origins[2], destZ, dists[2]))
                            {
                                ObjectProperty prop = new ObjectProperty(0, "StaticMesh");
                                reference.WriteProperty(prop);
                                removedReferences.Add($"{reference.ObjectName}_{reference.indexValue}");
                            }
                            break;
                        case "StaticMeshActor":
                            StructProperty location = ((ExportEntry)reference.Parent).GetProperty<StructProperty>("location");
                            if (location == null) { continue; }
                            // Check if the component is within the given distance, and if so remove the reference
                            if (InDist(origins[0], location.GetProp<FloatProperty>("X"), dists[0])
                                && InDist(origins[1], location.GetProp<FloatProperty>("Y"), dists[1])
                                && InDist(origins[2], location.GetProp<FloatProperty>("Z"), dists[2]))
                            {
                                ObjectProperty prop = new ObjectProperty(0, "StaticMesh");
                                reference.WriteProperty(prop);
                                removedReferences.Add($"{reference.ObjectName}_{reference.indexValue}");
                            }
                            break;
                        default:
                            continue;
                    }
                }
                else if (reference?.ClassName == "SkeletalMeshComponent")
                {
                    ExportEntry parent = (ExportEntry)reference.Parent;
                    StructProperty location = parent.GetProperty<StructProperty>("Location");
                    if (location == null) { continue; }

                    // Check if the component is within the given distance, and if so remove the reference
                    if (InDist(origins[0], location.GetProp<FloatProperty>("X"), dists[0])
                        && InDist(origins[1], location.GetProp<FloatProperty>("Y"), dists[1])
                        && InDist(origins[2], location.GetProp<FloatProperty>("Z"), dists[2]))
                    {
                        ObjectProperty prop = new ObjectProperty(0, "SkeletalMesh");
                        reference.WriteProperty(prop);
                        removedReferences.Add($"{reference.ObjectName}_{reference.indexValue}");
                    }
                }
            }

            if (removedReferences.Count > 0)
            {
                MessageBox.Show($"Removed references to the mesh in the following objects: {string.Join(", ", removedReferences.ToArray())}",
                    "Success", MessageBoxButton.OK);

            }
            else
            {
                MessageBox.Show("No references were found within the given distance");
            }
        }

        /// <summary>
        /// Copy the selected property to another export of the same class.
        /// </summary>
        /// <param name="pew">Current PE window.</param>
        public static void CopyProperty(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem == null || pew.SelectedItem.Entry == null) { return; }

            ExportEntry selectedEntry = (ExportEntry)pew.SelectedItem.Entry;
            if (selectedEntry == null)
            {
                ShowError("Invalid entry");
                return;
            }

            // Get an array of available properties so the user can select one
            string[] properties = selectedEntry.GetProperties().Select(property => property.Name.Name).ToArray();
            if (properties.Length == 0)
            {
                ShowError("Export contains no properties");
                return;
            }

            string sourcePropName = InputComboBoxDialog.GetValue(null, "Choose the property to copy:", "Copy property",
                properties);
            if (string.IsNullOrEmpty(sourcePropName))
            {
                ShowError("Invalid property");
                return;
            }

            int targetID;
            string strID = PromptDialog.Prompt(null, "Export ID of the export to copy into");
            if (string.IsNullOrEmpty(strID) || !int.TryParse(strID, out targetID))
            {
                ShowError("Invalid ID");
                return;
            }

            ExportEntry targetExport;
            if (!pew.Pcc.TryGetUExport(targetID, out targetExport))
            {
                ShowError("Target export not found");
                return;
            }
            if (targetExport.ClassName != selectedEntry.ClassName)
            {
                ShowError("Target export's class is different from the source");
                return;
            }

            Property sourceProp = selectedEntry.GetProperty<Property>(sourcePropName);
            targetExport.WriteProperty(sourceProp);

            MessageBox.Show($"Property {sourcePropName} copied succesfully", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Copies the texture, vector, and scalar properties of a BioMaterialOverride into [Bio]MaterialInstanceConstants, or vice-versa.
        /// </summary>
        /// <param name="pew">Current PE widow.</param>
        public static void CopyMatToBMOorMIC(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem == null || pew.SelectedItem.Entry == null) { return; }

            if (pew.SelectedItem == null || !(pew.SelectedItem.Entry.ClassName is "BioMaterialOverride" or "MaterialInstanceConstant" or "BioMaterialInstanceConstant"))
            {
                ShowError("Invalid selection. Select a BioMaterialOverride, a BioMaterialInstanceConstant, or a MaterialInstanceConstant to proceed");
                return;
            }

            // True if we copy from BMO to MIC, false if we copy from MIC to BMO
            bool isBMO = pew.SelectedItem.Entry.ClassName == "BioMaterialOverride";

            ExportEntry selectedEntry = (ExportEntry)pew.SelectedItem.Entry;
            ArrayProperty<StructProperty> textureProperty = selectedEntry.GetProperty<ArrayProperty<StructProperty>>
                ($"{(isBMO ? "m_aTextureOverrides" : "TextureParameterValues")}");
            ArrayProperty<StructProperty> vectorProperty = selectedEntry.GetProperty<ArrayProperty<StructProperty>>
                ($"{(isBMO ? "m_aColorOverrides" : "VectorParameterValues")}");
            ArrayProperty<StructProperty> scalarProperty = selectedEntry.GetProperty<ArrayProperty<StructProperty>>
                ($"{(isBMO ? "m_aScalarOverrides" : "ScalarParameterValues")}");
            if (textureProperty == null && vectorProperty == null && scalarProperty == null)
            {
                ShowError("No texture, vector, or scalar properties were found");
                return;
            }

            string strIDs = PromptDialog.Prompt(null, "Comma separated list of the Export ID of the target exports");
            if (string.IsNullOrEmpty(strIDs))
            {
                ShowError("Invalid IDs");
                return;
            }

            // Validate and load the provided export IDs.
            // We check first before running any operations on the actual properties.
            List<ExportEntry> targetExports = new();
            foreach (string id in strIDs.Split(","))
            {
                int targetID;
                if (!int.TryParse(id, out targetID))
                {
                    ShowError($"ID {id} is invalid");
                    return;

                }
                ExportEntry targetExport;
                if (!pew.Pcc.TryGetUExport(targetID, out targetExport))
                {
                    ShowError($"Target export with ID {id} not found");
                    return;
                }
                if (isBMO)
                {
                    if (!(targetExport.ClassName is "MaterialInstanceConstant" or "BioMaterialInstanceConstant"))
                    {
                        ShowError($"Target export {id}'s class is not MaterialInstanceConstant or BioMaterialInstanceConstant");
                        return;

                    }
                }
                else
                {
                    if (targetExport.ClassName != "BioMaterialOverride")
                    {
                        ShowError($"Target export {id}'s class is not BioMaterialOverride");
                        return;

                    }
                }
                targetExports.Add(targetExport);
            }


            ArrayProperty<StructProperty> TextureValues = new($"{(isBMO ? "TextureParameterValues" : "m_aTextureOverrides")}");
            ArrayProperty<StructProperty> VectorValues = new($"{(isBMO ? "VectorParameterValues" : "m_aColorOverrides")}");
            ArrayProperty<StructProperty> ScalarValues = new($"{(isBMO ? "ScalarParameterValues" : "m_aScalarOverrides")}");

            if (textureProperty != null)
            {
                foreach (StructProperty parameter in textureProperty)
                {
                    PropertyCollection props = new PropertyCollection();
                    if (isBMO) { props.Add(GenerateExpressionGUID()); }
                    props.Add(new NameProperty(parameter.GetProp<NameProperty>($"{(isBMO ? "nName" : "ParameterName")}").Value,
                        $"{(isBMO ? "ParameterName" : "nName")}"));
                    props.Add(new ObjectProperty(parameter.GetProp<ObjectProperty>($"{(isBMO ? "m_pTexture" : "ParameterValue")}").Value,
                        $"{(isBMO ? "ParameterValue" : "m_pTexture")}"));
                    TextureValues.Add(new StructProperty($"{(isBMO ? "TextureParameterValue" : "TextureParameter")}", props));
                }
            }

            if (vectorProperty != null)
            {
                foreach (StructProperty parameter in vectorProperty)
                {
                    PropertyCollection props = new();
                    PropertyCollection color = new();
                    color.Add(parameter.GetProp<StructProperty>($"{(isBMO ? "cValue" : "ParameterValue")}").GetProp<FloatProperty>("R"));
                    color.Add(parameter.GetProp<StructProperty>($"{(isBMO ? "cValue" : "ParameterValue")}").GetProp<FloatProperty>("G"));
                    color.Add(parameter.GetProp<StructProperty>($"{(isBMO ? "cValue" : "ParameterValue")}").GetProp<FloatProperty>("B"));
                    color.Add(parameter.GetProp<StructProperty>($"{(isBMO ? "cValue" : "ParameterValue")}").GetProp<FloatProperty>("A"));
                    StructProperty ParameterValue = new("LinearColor", color, $"{(isBMO ? "ParameterValue" : "cValue")}", true);
                    if (isBMO) { props.Add(GenerateExpressionGUID()); }
                    props.Add(ParameterValue);
                    props.Add(new NameProperty(parameter.GetProp<NameProperty>($"{(isBMO ? "nName" : "ParameterName")}").Value,
                        $"{(isBMO ? "ParameterName" : "nName")}"));
                    VectorValues.Add(new StructProperty($"{(isBMO ? "VectorParameterValue" : "ColorParameter")}", props));
                }
            }

            if (scalarProperty != null)
            {
                foreach (StructProperty parameter in scalarProperty)
                {
                    PropertyCollection props = new();
                    if (isBMO) { props.Add(GenerateExpressionGUID()); }
                    props.Add(new NameProperty(parameter.GetProp<NameProperty>($"{(isBMO ? "nName" : "ParameterName")}").Value,
                        $"{(isBMO ? "ParameterName" : "nName")}"));
                    props.Add(new FloatProperty(parameter.GetProp<FloatProperty>($"{(isBMO ? "sValue" : "ParameterValue")}").Value,
                        $"{(isBMO ? "ParameterValue" : "sValue")}"));
                    ScalarValues.Add(new StructProperty($"{(isBMO ? "ScalarParameterValue" : "ScalarParameter")}", props));
                }
            }

            foreach (ExportEntry targetExport in targetExports)
            {
                if (textureProperty != null) { targetExport.WriteProperty(TextureValues); }
                if (vectorProperty != null) { targetExport.WriteProperty(VectorValues); }
                if (scalarProperty != null) { targetExport.WriteProperty(ScalarValues); }
            }

            MessageBox.Show("Properties copied successfully", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Keeps only the things that are essential to the conversation and give its elements
        /// unique names and IDs so it can be used as a template for new ones.
        /// </summary>
        /// <param name="pew">Current PE instance.</param>
        public static void CleanConvoDonor(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.Pcc.Game is MEGame.ME1)
            {
                ShowError("Not available for ME1");
                return;
            }

            if (pew.SelectedItem.Entry.ClassName is not "BioConversation")
            {
                ShowError("Selected export is not a BioConversation");
                return;
            }

            string newName = PromptDialog.Prompt(null, "New conversation name:", "New name");
            // Check that the new name is not empty, no longe than 255, and doesn't contain white-spaces or symbols aside from _ or -
            if (string.IsNullOrEmpty(newName) || newName.Length > 240 || newName.Any(c => char.IsWhiteSpace(c) || (!(c is '_' or '-') && !char.IsLetterOrDigit(c))))
            {
                ShowError("Invalid new name. It must not be empty, be longer than 240 characters, or contain whitespaces or symbols aside from '-' and '_'");
                return;
            }

            int newConvResRefID = promptForInt("New ConvResRefID:", "Not a valid ref id. It must be positive integer", 1, "New ConvResRefID");
            if (newConvResRefID == -1) { return; }

            int convNodeIDBase = promptForInt("New ConvNodeID base range:", "Not a valid base. It must be positive integer", 1, "New NodeID range");
            if (convNodeIDBase == -1) { return; }

            bool setNewWwiseBankID = false;
            if (!pew.Pcc.Game.IsGame1())
            {
                setNewWwiseBankID = MessageBoxResult.Yes == MessageBox.Show(
                "Change the WwiseBank ID?\nIn general it's safe and better to do so, but there may be edge cases" +
                "where doing so may overwrite parts of the WwiseBank binary that are not the ID.",
                "Set new bank ID", MessageBoxButton.YesNo);
            }

            bool bringTrash = MessageBoxResult.No == MessageBox.Show(
                "Discard sequence objects that are not Interp, InterpData, ConvNode or EndConvNode but link to them?\n" +
                "In general it's better to discard them, but there may be edge cases where you may want to preserve them.",
                "Discard unneeded objects", MessageBoxButton.YesNo);

            ExportEntry bioConversation = (ExportEntry)pew.SelectedItem.Entry;

            // Load the conversation. We use ConversationExtended since it aggregates most of the elements we'll need to
            // operate on
            ConversationExtended conversation = new(bioConversation);
            conversation.LoadConversation(TLKManagerWPF.GlobalFindStrRefbyID, true);

            // Rename the conversation, its package, the WwiseBank, the FXAs, VOs and WwiseEvents
            RenameConversation(pew.Pcc, bioConversation, conversation, newName, setNewWwiseBankID);

            // Change the conversations ResRefID an the ConvNodes IDs
            ChangeConvoIDandConvNodeIDs(bioConversation, (ExportEntry)conversation.Sequence, newConvResRefID, convNodeIDBase);

            // Clean the sequence of unneeded objects and keep only Conversation INterpGroups and VOElements InterpTracks
            // We'll trash unnecessary InterpTracks, InterpGroups and no longer used objects. It's necessary so users of the experiment
            // can clone the package of a conversation instead of the BioConversation directly.
            List<string> disconnectedConvNodes = CleanSequence(pew.Pcc, (ExportEntry)conversation.Sequence, true, bringTrash, true);

            string successMessage = "Conversation cleaned successfully.";
            if (disconnectedConvNodes.Count > 0)
            {
                successMessage += $"\nThe following ConvNodes were found to be disconnected after the process: {string.Join(", ", disconnectedConvNodes)}";
            }

            MessageBox.Show(successMessage, "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Wrapper for CleanSequence so it can used as a full experiment on its own.
        /// </summary>
        /// <param name="pew">Current PE instance.</param>
        public static void CleanSequenceExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.Pcc.Game is MEGame.ME1)
            {
                ShowError("Not available for ME1");
                return;
            }

            if (pew.SelectedItem.Entry.ClassName is not "Sequence")
            {
                ShowError("Selected export is not a Sequence");
                return;
            }

            bool cleanGroups = MessageBoxResult.Yes == MessageBox.Show(
                "Clean sequence of unused InterpGroups and InterpTracks?", "Clean groups and tracks", MessageBoxButton.YesNo);

            bool trashItems = MessageBoxResult.Yes == MessageBox.Show(
                "Trash unused objects and elements?", "Trash unusued", MessageBoxButton.YesNo);

            bool bringTrash = MessageBoxResult.No == MessageBox.Show(
                "Discard sequence objects that are not Interp, InterpData, ConvNode or EndConvNode but link to them?\n" +
                "In general it's better to discard them, but there may be edge cases where you may want to preserve them.",
                "Discard unneeded objects", MessageBoxButton.YesNo);

            ExportEntry sequence = (ExportEntry)pew.SelectedItem.Entry;

            List<string> disconnectedConvNodes = CleanSequence(pew.Pcc, sequence, trashItems, bringTrash, cleanGroups);

            string successMessage = "Sequence cleaned successfully.";
            if (disconnectedConvNodes.Count > 0)
            {
                successMessage += $"\nThe following ConvNodes were found to be disconnected after the process: {string.Join(", ", disconnectedConvNodes)}";
            }
            MessageBox.Show(successMessage, "Success", MessageBoxButton.OK);
            return;
        }

        /// <summary>
        /// Cleans a sequence of objects that are not needed for a basic conversation.
        /// This experiment DOES NOT update the varLinks.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="sequence">If called, the sequence to clean.</param>
        /// <param name="trashItems">If called, whether to trash unused groups and tracks.</param>
        /// <param name="bringTrash">If called, whether to keep all objects that point to conversation classes, or only essential ones.</param>
        /// <param name="cleanGroups">If called, whether to clean InterpGroups and InterpTracks.</param>
        /// <returns>List of ConvNodes that may no longer connect to Interps.</returns>
        public static List<string> CleanSequence(IMEPackage pcc, ExportEntry sequence, bool trashItems, bool bringTrash, bool cleanGroups)
        {
            List<IEntry> itemsToTrash = new();

            if (cleanGroups)
            {
                List<IEntry> groupsTrash = CleanSequenceInterpDatas(pcc, sequence);
                if (groupsTrash != null) { itemsToTrash.AddRange(groupsTrash); }
            }

            // Remove animation sets
            string animsetsPropName = pcc.Game.IsGame3() ? "m_aSFXSharedAnimsets" : "m_aBioDynAnimSets";
            ArrayProperty<ObjectProperty> animSets = sequence.GetProperty<ArrayProperty<ObjectProperty>>(animsetsPropName);
            if (animSets != null)
            {
                // Store KYS objects to trash
                foreach (ObjectProperty kysRef in animSets)
                {
                    if (kysRef == null || kysRef.Value == 0) { continue; }
                    ExportEntry kys = pcc.GetUExport(kysRef.Value);
                    if (kys != null) { itemsToTrash.Add(kys); }
                }
                sequence.RemoveProperty(animsetsPropName);
            }

            // Keep only objects essential to the conversation
            ArrayProperty<ObjectProperty> seqObjRefs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
            // Keep a list of disconnected convNodes in case removal of certain objects breaks their links to Interps
            List<string> disconnectedConvNodes = new();
            if (seqObjRefs != null)
            {
                List<string> validClasses = new() { "BioSeqAct_EndCurrentConvNode", "BioSeqEvt_ConvNode", "InterpData", "SeqAct_Interp" };
                List<ObjectProperty> filteredObjRefs = new();
                List<ExportEntry> filteredObjs = new();

                foreach (ObjectProperty objRef in seqObjRefs)
                {
                    if (objRef == null || objRef.Value == 0) { continue; }

                    ExportEntry seqObj = pcc.GetUExport(objRef.Value);

                    if (seqObj == null) { continue; }

                    // Check if the object links to a valid class.
                    // We do this regardless of bringTrash in order to know whether a valid convNode may be
                    // an orphan.
                    List<List<SeqTools.OutboundLink>> outboundLinks = SeqTools.GetOutboundLinksOfNode(seqObj);
                    // Link is valid if it has outbound links, and at least 1 one is linked to 1 valid class
                    bool linksToValid = (outboundLinks.Count > 0) && outboundLinks
                        .Any(outboundLink => outboundLink
                            .Any(link =>
                                link != null && validClasses.Contains(link.LinkedOp.ClassName, StringComparer.OrdinalIgnoreCase)
                            )
                        );
                    // Keep a list of convNodes that link to nothing
                    if (!linksToValid && seqObj.ClassName == "BioSeqEvt_ConvNode")
                    {
                        disconnectedConvNodes.Add(seqObj.ObjectName.Instanced.Replace("BioSeqEvt_", ""));
                    }

                    // Override linkToValid if we want to ignore the object unless it's a valid class
                    if (!bringTrash) { linksToValid = false; }

                    // Skip objects that are not essential for the conversation
                    // If linktsToValid, we won't recalculate validClasses.Contains, so we're all good
                    if (!linksToValid && !validClasses.Contains(seqObj.ClassName, StringComparer.OrdinalIgnoreCase))
                    {
                        itemsToTrash.Add(seqObj);
                        continue;
                    }

                    // Save only Data var links of Interps
                    if (seqObj.ClassName == "SeqAct_Interp")
                    {
                        List<StructProperty> varLinks = seqObj.GetProperty<ArrayProperty<StructProperty>>("VariableLinks").ToList();

                        if (varLinks != null)
                        {
                            List<StructProperty> newVarLinks = new();

                            StructProperty dataLink = varLinks.Find(link =>
                                string.Equals(link.GetProp<StrProperty>("LinkDesc").Value, "Data", StringComparison.OrdinalIgnoreCase));

                            if (dataLink != null) { newVarLinks.Add(dataLink); }

                            newVarLinks.Add(CreateVarLink(pcc, "Anchor"));
                            newVarLinks.Add(CreateVarLink(pcc, "Conversation"));

                            seqObj.WriteProperty(new ArrayProperty<StructProperty>(newVarLinks, "VariableLinks"));
                        }
                    }

                    filteredObjRefs.Add(objRef);
                    filteredObjs.Add(seqObj);
                }

                sequence.WriteProperty(new ArrayProperty<ObjectProperty>(filteredObjRefs, "SequenceObjects"));

                // Remove links to objects no longer in the sequence
                foreach (ExportEntry seqObj in filteredObjs)
                {
                    // Keep only outbound links to valid classes
                    List<List<SeqTools.OutboundLink>> outboundLinks = SeqTools.GetOutboundLinksOfNode(seqObj);
                    if (outboundLinks.Count > 0)
                    {
                        List<List<SeqTools.OutboundLink>> filteredOutboundLinks = new();
                        foreach (List<SeqTools.OutboundLink> outboundLink in outboundLinks)
                        {
                            // Add to filteredOutboundLinks the elements of this outbound link that connect to a valid class
                            filteredOutboundLinks.Add(outboundLink
                                .Where(link =>
                                    link == null || validClasses.Contains(link.LinkedOp.ClassName, StringComparer.OrdinalIgnoreCase)
                                ).ToList());
                        }
                        SeqTools.WriteOutboundLinksToNode(seqObj, filteredOutboundLinks);
                    }
                }
            }

            if (trashItems) { EntryPruner.TrashEntries(pcc, itemsToTrash); }

            return disconnectedConvNodes;
        }

        /// <summary>
        /// Wrapper for CleanSequenceInterpDatas so it can used as a full experiment on its own.
        /// </summary>
        /// <param name="pew">Current PE instance.</param>
        public static void CleanSequenceInterpDatasExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.Pcc.Game is MEGame.ME1)
            {
                ShowError("Not available for ME1");
                return;
            }

            if (pew.SelectedItem.Entry.ClassName is not "Sequence")
            {
                ShowError("Selected export is not a Sequence");
                return;
            }

            bool trashItems = MessageBoxResult.Yes == MessageBox.Show(
                "Trash unused InterpGroups and InterpTracks?", "Trash unusued", MessageBoxButton.YesNo);

            ExportEntry sequence = (ExportEntry)pew.SelectedItem.Entry;

            List<IEntry> itemsToTrash = CleanSequenceInterpDatas(pew.Pcc, sequence);

            if (trashItems) { EntryPruner.TrashEntries(pew.Pcc, itemsToTrash); }

            MessageBox.Show("Sequence cleaned of non-Conversation InterpGroups and  non-VOElements InterpTracks.", "Success", MessageBoxButton.OK);
            return;

        }

        /// <summary>
        /// Cleans a sequence's InterpDatas of non-Conversation InterpGroups and non-VOElements InterpTracks.
        /// This experiment DOES NOT update the varLinks.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="sequence">If called, the sequence to clean.</param>
        /// <param name="trashItems">If not called, whether to trash unused groups and tracks.</param>
        public static List<IEntry> CleanSequenceInterpDatas(IMEPackage pcc, ExportEntry sequence)
        {
            List<IEntry> itemsToTrash = new();

            List<IEntry> interpDatas = new(SeqTools.GetAllSequenceElements(sequence)
                .Where(el => el.ClassName == "InterpData"));

            // Keep only InterpGroups named "Conversation" and only the BioEvtSysTrackVOElements InterpTracks
            foreach (ExportEntry interpData in interpDatas)
            {
                ArrayProperty<ObjectProperty> interpGroupsRefs = interpData.GetProperty<ArrayProperty<ObjectProperty>>("InterpGroups");
                if (interpGroupsRefs == null) { continue; }
                List<ObjectProperty> filteredGroupsRefs = new();

                // Save "Conversation" InterpGroup, trash the rest
                foreach (ObjectProperty groupRef in interpGroupsRefs)
                {
                    if (groupRef.Value == 0) { continue; }

                    ExportEntry group = pcc.GetUExport(groupRef.Value);
                    if (group == null) { continue; }

                    NameProperty name = group.GetProperty<NameProperty>("GroupName");

                    if (name != null && !string.IsNullOrEmpty(name.Value) && name.Value.Name.Equals("Conversation", StringComparison.OrdinalIgnoreCase))
                    {
                        filteredGroupsRefs.Add(groupRef);
                    }
                    else
                    {
                        itemsToTrash.Add(group);
                    }
                }

                // Keep only BioEvtSysTrackVOElements InterpTracks
                foreach (ObjectProperty interpGroupRef in filteredGroupsRefs)
                {
                    ExportEntry interpGroup = pcc.GetUExport(interpGroupRef.Value);

                    ArrayProperty<ObjectProperty> interpTracksRefs = interpGroup.GetProperty<ArrayProperty<ObjectProperty>>("InterpTracks");
                    if (interpTracksRefs == null) { continue; }
                    List<ObjectProperty> filteredTracksRefs = new();

                    foreach (ObjectProperty trackRef in interpTracksRefs)
                    {
                        if (trackRef.Value == 0) { continue; }

                        ExportEntry track = pcc.GetUExport(trackRef.Value);
                        if (track == null) { continue; }

                        if (track.ClassName == "BioEvtSysTrackVOElements")
                        {
                            filteredTracksRefs.Add(trackRef);
                        }
                        else
                        {
                            itemsToTrash.Insert(0, track); // Insert first so they are trashed first
                        }
                    }

                    interpGroup.WriteProperty(new ArrayProperty<ObjectProperty>(filteredTracksRefs, "InterpTracks"));
                }

                interpData.WriteProperty(new ArrayProperty<ObjectProperty>(filteredGroupsRefs, "InterpGroups"));
                interpData.RemoveProperty("m_aBioPreloadData"); // Make sure not to keep extra stuff here
            }

            return itemsToTrash;
        }

        public static void ChangeConvoIDandConvNodeIDsExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.Pcc.Game is MEGame.ME1)
            {
                ShowError("Not available for ME1");
                return;
            }

            if (pew.SelectedItem.Entry.ClassName is not "BioConversation")
            {
                ShowError("Selected export is not a BioConversation");
                return;
            }

            int newConvResRefID = promptForInt("New ConvResRefID:", "Not a valid ref id. It must be positive integer", 1, "New ConvResRefID");
            if (newConvResRefID == -1) { return; }

            int convNodeIDBase = promptForInt("New ConvNodeID base range:", "Not a valid base. It must be positive integer", 1, "New NodeID range");
            if (convNodeIDBase == -1) { return; }

            ExportEntry bioConversation = (ExportEntry)pew.SelectedItem.Entry;

            // Load the conversation. We use ConversationExtended since it aggregates most of the elements we'll need to
            // operate on. Is it overkill? Yes. Does it get the job done more cleanly and safely? yes.
            ConversationExtended conversation = new(bioConversation);
            conversation.LoadConversation(TLKManagerWPF.GlobalFindStrRefbyID, true);

            ExportEntry sequence = (ExportEntry)conversation.Sequence;

            ChangeConvoIDandConvNodeIDs(bioConversation, sequence, newConvResRefID, convNodeIDBase);

            MessageBox.Show("Conversation's ResRefID and ConvNodes' ID updated successfully.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Changes the conversation's ResRefID and its ConvNodes' ID.
        /// </summary>
        /// <param name="bioConversation">If called, the conversation to edit.</param>
        /// <param name="sequence">If called, the conversation's sequence.</param>
        /// <param name="newConvResRefID">If called, the newConvResRefID to set.</param>
        /// <param name="convNodeIDBase">If called, the convNodeIDBase to use.</param>
        public static void ChangeConvoIDandConvNodeIDs(ExportEntry bioConversation, ExportEntry sequence, int newConvResRefID, int convNodeIDBase)
        {
            // Update the conversation's refId
            IntProperty m_nResRefID = new(newConvResRefID, "m_nResRefID");
            bioConversation.WriteProperty(m_nResRefID);

            // Update the convNodes nodeId and convResRefId
            int count = 0;
            List<IEntry> convNodes = new(SeqTools.GetAllSequenceElements(sequence)
                .Where(el => el.ClassName == "BioSeqEvt_ConvNode"));

            Dictionary<int, int> remappedIDs = new(); // Save references of old id for update of entry and reply lists
            foreach (ExportEntry convNode in convNodes)
            {
                IntProperty oldNodeID = convNode.GetProperty<IntProperty>("m_nNodeID");
                if (oldNodeID == null) { continue; }

                remappedIDs.Add(oldNodeID.Value, convNodeIDBase + count);

                IntProperty m_nNodeID = new(convNodeIDBase + count, "m_nNodeID");
                IntProperty m_nConvResRefID = new(newConvResRefID, "m_nConvResRefID");
                convNode.WriteProperty(m_nNodeID);
                convNode.WriteProperty(m_nConvResRefID);
                count++;
            }

            // Update the nExportIDs of the Entry list
            ArrayProperty<StructProperty> entryNodes = bioConversation.GetProperty<ArrayProperty<StructProperty>>("m_EntryList");
            foreach (StructProperty entryNode in entryNodes)
            {
                IntProperty oldNodeID = entryNode.GetProp<IntProperty>("nExportID");
                if (oldNodeID == null) { continue; }

                if (!remappedIDs.ContainsKey(oldNodeID.Value))
                {
                    remappedIDs.Add(oldNodeID.Value, convNodeIDBase + count);
                    count++;
                }

                PropertyCollection properties = entryNode.Properties;
                IntProperty nExportID = new(remappedIDs[oldNodeID.Value], "nExportID");
                properties.AddOrReplaceProp(nExportID);
                entryNode.Properties = properties;
            }

            // Update the nExportIDs of the Reply list
            ArrayProperty<StructProperty> replyNodes = bioConversation.GetProperty<ArrayProperty<StructProperty>>("m_ReplyList");
            foreach (StructProperty replyNode in replyNodes)
            {
                IntProperty oldNodeID = replyNode.GetProp<IntProperty>("nExportID");
                if (oldNodeID == null) { continue; }
                if (!remappedIDs.ContainsKey(oldNodeID.Value))
                {
                    remappedIDs.Add(oldNodeID.Value, convNodeIDBase + count);
                    count++;
                }

                PropertyCollection properties = replyNode.Properties;
                IntProperty nExportID = new(remappedIDs[oldNodeID.Value], "nExportID");
                properties.AddOrReplaceProp(nExportID);
                replyNode.Properties = properties;
            }

            bioConversation.WriteProperty(entryNodes);
            bioConversation.WriteProperty(replyNodes);
        }

        /// <summary>
        /// Wrapper for RenameConversation so it can used as a full experiment on its own.
        /// </summary>
        /// <param name="pew">Current PE instance.</param>
        public static void RenameConversationExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.Pcc.Game is MEGame.ME1)
            {
                ShowError("Not available for ME1");
                return;
            }

            if (pew.SelectedItem.Entry.ClassName is not "BioConversation")
            {
                ShowError("Selected export is not a BioConversation");
                return;
            }

            string newName = PromptDialog.Prompt(null, "New WwiseBank name:", "New name");
            // Check that the new name is not empty, no longe than 255, and doesn't contain white-spaces or symbols aside from _ or -
            if (string.IsNullOrEmpty(newName) || newName.Length > 240 || newName.Any(c => char.IsWhiteSpace(c) || (!(c is '_' or '-') && !char.IsLetterOrDigit(c))))
            {
                ShowError("Invalid name. It must not be empty, be longer than 240 characters, or contain whitespaces or symbols aside from '-' and '_'");
                return;
            }

            bool updateID = true;
            if (!pew.Pcc.Game.IsGame1())
            {
                updateID = MessageBoxResult.Yes == MessageBox.Show(
                    "Change the WwiseBank ID?\nIn general it's safe and better to do so, but there may be edge cases" +
                    "where doing so may overwrite parts of the WwiseBank binary that are not the ID.",
                    "Set new bank ID", MessageBoxButton.YesNo);
            }

            ExportEntry bioConversation = (ExportEntry)pew.SelectedItem.Entry;

            // Load the conversation. We use ConversationExtended since it aggregates most of the elements we'll need to
            // operate on. Is it overkill? Yes. Does it get the job done more cleanly and safely? yes.
            ConversationExtended conversation = new(bioConversation);
            conversation.LoadConversation(TLKManagerWPF.GlobalFindStrRefbyID, true);

            RenameConversation(pew.Pcc, bioConversation, conversation, newName, updateID);

            MessageBox.Show("Conversation renamed successfully.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Rename a conversation, changing the WwiseBank name and FXAs and related elements too.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">If called, the bioConversation entry to edit.</param>
        /// <param name="conversation">If called, the loaded conversation edit.</param>
        /// <param name="newName">If called, the new name.</param>
        /// <param name="updateID">If called, Whether to update the ID.</param>
        public static void RenameConversation(IMEPackage pcc, ExportEntry bioConversation, ConversationExtended conversation, string newName, bool updateID)
        {
            string oldBioConversationName = bioConversation.ObjectName;
            string oldName;

            // Get the old name found in all pieces of the conversation by getting the union of the bioconversation
            // and the wwise bank names, or tlk file set in ME1.
            // Assumes that both begin the same, since that's the behavior in all vanilla occurences, and helps
            // keep the logic simple.
            if (pcc.Game.IsGame1())
            {
                ObjectProperty m_oTlkFileSet = bioConversation.GetProperty<ObjectProperty>("m_oTlkFileSet");
                if (m_oTlkFileSet != null && m_oTlkFileSet.Value != 0)
                {
                    ExportEntry tlkFileSet = pcc.GetUExport(m_oTlkFileSet.Value);
                    // All Tlk file sets begin with TlkSet_, followed by the name they have in common with the package
                    oldName = GetCommonPrefix(tlkFileSet.ObjectName.Name[7..], oldBioConversationName);
                }
                else
                {
                    // I've been told all LE1 conversations have a tlk file set, so this won't ever be reached,
                    // but better safe than sorry.
                    oldName = oldBioConversationName;
                }
            }
            else
            {
                string oldWwiseBankName = conversation.WwiseBank.ObjectName;
                oldName = GetCommonPrefix(oldWwiseBankName, oldBioConversationName);
                string newWwiseBankName = oldWwiseBankName.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);

                // Must happen before renaming the wwiseBank since it reads the old bank's name
                RenameWwiseBank(conversation.WwiseBank, newWwiseBankName, updateID);
                conversation.WwiseBank.ObjectName = newWwiseBankName;
            }

            string newBioConversationName = oldBioConversationName.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
            bioConversation.ObjectName = newBioConversationName;

            // Replace package's name
            if (bioConversation.idxLink != 0)
            {
                ExportEntry link = pcc.GetUExport(bioConversation.idxLink);
                if (link.ClassName == "Package")
                {
                    link.ObjectName = link.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                }
            }
            // Replace name of the sound, sequence, and FXA package, which is separate in ME1
            if (pcc.Game.IsGame1())
            {
                ExportEntry link = pcc.GetUExport(conversation.Sequence.idxLink);
                if (link.ClassName == "Package")
                {
                    link.ObjectName = link.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                }
            }
            // Replace name of the sounds package, which is separate in ME2
            if (pcc.Game.IsGame2())
            {
                ExportEntry link = pcc.GetUExport(conversation.WwiseBank.idxLink);
                if (link.ClassName == "Package")
                {
                    link.ObjectName = link.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                }
            }

            // Must be called after everything else has been renamed due to the need ot update FXA paths.
            RenameFXAsAndRelated(pcc, bioConversation, oldName, newName);
        }

        /// <summary>
        /// Wrapper for RenameWwiseBank so it can used as a full experiment on its own.
        /// </summary>
        /// <param name="pew">Current PE instance.</param>
        public static void RenameWwiseBankExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.Pcc.Game.IsGame1())
            {
                ShowError("Not available for ME1/LE1");
                return;
            }

            if (pew.SelectedItem.Entry.ClassName is not "WwiseBank")
            {
                ShowError("Selected export is not a WwiseBank");
                return;
            }

            ExportEntry wwiseBankEntry = (ExportEntry)pew.SelectedItem.Entry;

            string newWwiseBankName = PromptDialog.Prompt(null, "New WwiseBank name:", "New name");
            // Check that the new name is not empty, no longe than 255, and doesn't contain white-spaces or symbols aside from _ or -
            if (string.IsNullOrEmpty(newWwiseBankName) || newWwiseBankName.Length > 240 || newWwiseBankName.Any(c => char.IsWhiteSpace(c) || (!(c is '_' or '-') && !char.IsLetterOrDigit(c))))
            {
                ShowError("Invalid name. It must not be empty, be longer than 240 characters, or contain whitespaces or symbols aside from '-' and '_'");
                return;
            }

            RenameWwiseBank(wwiseBankEntry, newWwiseBankName, true);

            MessageBox.Show($"WwiseBank's name and ID updated successfully.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Changes a WwiseBank's name, and update its ID accordingly.
        /// </summary>
        /// <param name="wwiseBankEntry">If called, the WwiseBank entry to edit.</param>
        /// <param name="newWwiseBankName">If called, the new wwise bank name.</param>
        /// <param name="updateID">Whether to update the ID. Useful when you want to be extra careful when calling it from
        /// other experiments.</param>
        public static void RenameWwiseBank(ExportEntry wwiseBankEntry, string newWwiseBankName, bool updateID)
        {
            if (updateID) { UpdateWwiseBankID(wwiseBankEntry, newWwiseBankName); }
            wwiseBankEntry.ObjectName = newWwiseBankName;
        }

        /// <summary>
        /// Wrapper for UpdateWwiseBankID so it can used as a full experiment on its own.
        /// </summary>
        /// <param name="pew">Current PE instance.</param>
        public static void UpdateWwiseBankIDExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.Pcc.Game.IsGame1())
            {
                ShowError("Not available for ME1/LE1");
                return;
            }

            if (pew.SelectedItem.Entry.ClassName is not "WwiseBank")
            {
                ShowError("Selected export is not a WwiseBank");
                return;
            }

            ExportEntry wwiseBankEntry = (ExportEntry)pew.SelectedItem.Entry;

            string newWwiseBankName = PromptDialog.Prompt(null, "New WwiseBank name:", "New name");
            // Check that the new name is not empty, no longe than 255, and doesn't contain white-spaces or symbols aside from _ or -
            if (string.IsNullOrEmpty(newWwiseBankName) || newWwiseBankName.Length > 240 || newWwiseBankName.Any(c => char.IsWhiteSpace(c) || (!(c is '_' or '-') && !char.IsLetterOrDigit(c))))
            {
                ShowError("Invalid name. It must not be empty, be longer than 240 characters, or contain whitespaces or symbols aside from '-' and '_'");
                return;
            }

            UpdateWwiseBankID(wwiseBankEntry, newWwiseBankName);
            MessageBox.Show("WwiseBank's ID updated successfully.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Update a WwiseBank's ID by hashing a new name and changing it in the binary data where appropriate.
        /// </summary>
        /// <param name="wwiseBankEntry">If called, the WwiseBank entry to edit.</param>
        /// <param name="newWwiseBankName">If called, the new wwise bank name.</param>
        public static void UpdateWwiseBankID(ExportEntry wwiseBankEntry, string newWwiseBankName)
        {
            string oldBankName = wwiseBankEntry.ObjectName;
            IntProperty bankIDProp = wwiseBankEntry.GetProperty<IntProperty>("Id");

            // Get/Generate IDs and little endian hashes
            uint oldBankID = unchecked((uint)bankIDProp.Value);
            string oldBankHash = BigToLittleEndian(string.Format("{0:X2}", oldBankID).PadLeft(8, '0'));

            uint newBankID = GetBankId(newWwiseBankName);
            string newBankHash = BigToLittleEndian(string.Format("{0:X2}", newBankID).PadLeft(8, '0'));

            // Write the replaced ID property
            bankIDProp.Value = unchecked((int)newBankID);
            wwiseBankEntry.WriteProperty(bankIDProp);

            WwiseBank wwiseBank = wwiseBankEntry.GetBinaryData<WwiseBank>();
            // Update the bank id
            wwiseBank.ID = newBankID;

            // Update referenced banks kvp that reference the old name
            IEnumerable<KeyValuePair<uint, string>> updatedBanks = wwiseBank.ReferencedBanks
                .Select(referencedBank =>
                {
                    if (referencedBank.Value.Equals(oldBankName, StringComparison.OrdinalIgnoreCase)) { return new(newBankID, newWwiseBankName); }
                    else { return referencedBank; }
                });
            wwiseBank.ReferencedBanks = new OrderedMultiValueDictionary<uint, string>(updatedBanks);

            // Update references to the old hash at the end of HIRC objects when they are found.
            // This is mostly safe, since we know the reference appears at the end of the unparsed data,
            // and we only replace it there.
            foreach (WwiseBank.HIRCObject hirc in wwiseBank.HIRCObjects.Values())
            {
                byte[] unparsed = hirc.unparsed;
                if (unparsed != null && unparsed.Length >= 4) // Only replace if not null and at least width of hash
                {
                    byte[] oldArr = Convert.FromHexString(oldBankHash);
                    byte[] newArr = Convert.FromHexString(newBankHash);

                    int idBase = unparsed.Length - 4;

                    // Check if the last 4 bytes of unparsed match the old hash
                    bool equal = true;
                    for (int i = 0; i < 4; i++)
                    {
                        equal = equal && (unparsed[idBase + i] == oldArr[i]);
                    }

                    // Replace the hash
                    if (equal)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            unparsed[idBase + i] = newArr[i];
                        }

                        hirc.unparsed = unparsed;
                    }
                }
            }

            wwiseBankEntry.WriteBinary(wwiseBank);
        }

        /// <summary>
        /// Rename a bioConversation's FXAs, WwiseStreams, and VOElements names.
        /// Not useful as a standalone experiment, since other semi-unrelated elements need to be renamed too.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">Conversation the elements belong to.</param>
        /// <param name="oldName">Old name to replace in the elements.</param>
        /// <param name="newName">New name to replace in the elements.</param>
        private static void RenameFXAsAndRelated(IMEPackage pcc, ExportEntry bioConversation, string oldName, string newName)
        {
            if (pcc == null || (pcc.Game is MEGame.ME1)) { return; }

            List<ObjectProperty> fxas = new();
            ArrayProperty<ObjectProperty> maleFXAs = bioConversation.GetProperty<ArrayProperty<ObjectProperty>>("m_aMaleFaceSets");
            if (maleFXAs != null) { fxas.AddRange(maleFXAs); }
            ArrayProperty<ObjectProperty> femaleFXAs = bioConversation.GetProperty<ArrayProperty<ObjectProperty>>("m_aFemaleFaceSets");
            if (femaleFXAs != null) { fxas.AddRange(femaleFXAs); }
            ObjectProperty nonSpkrFxa = bioConversation.GetProperty<ObjectProperty>("m_pNonSpeakerFaceFXSet");
            if (nonSpkrFxa != null) { fxas.Add(nonSpkrFxa); }

            foreach (ObjectProperty fxa in fxas)
            {
                if (fxa.Value == 0) { continue; }

                ExportEntry fxaExport = pcc.GetUExport(fxa.Value);

                string oldFxaFullName = fxaExport.ObjectName; // May contain _M, _F, or _NonSpkr
                string oldFxaName = oldFxaFullName; // Full name minus _M/_F, or including _NonSpkr

                if (oldFxaFullName[^2..].ToLower() is "_m" or "_f")
                {
                    oldFxaName = oldFxaFullName.Remove(oldFxaFullName.Length - 2);
                }
                else
                {
                    // Most likely a NonSpkr, in which case we'll use the full name
                    if (oldFxaFullName.Length > 8 && oldFxaFullName[^8..].ToLower() is "_nonspkr")
                    {
                        oldFxaName = oldFxaFullName;
                    }
                }

                string newFxaName = oldFxaName.Replace(oldName, newName);
                fxaExport.ObjectName = oldFxaFullName.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);

                FaceFXAnimSet faceFXAnimSet = fxaExport.GetBinaryData<FaceFXAnimSet>();
                // Replace the old name in the name chunk
                List<string> names = faceFXAnimSet.Names.Select(name =>
                {
                    if (name == oldFxaName) { return newFxaName; }
                    else { return name; }
                }).ToList();
                faceFXAnimSet.Names = names;

                // Set the paths with the new names and update the names of WwiseStreams
                ArrayProperty<ObjectProperty> eventRefs = fxaExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues");
                if (eventRefs != null)
                {
                    foreach (FaceFXLine line in faceFXAnimSet.Lines)
                    {
                        ExportEntry soundEvent;
                        if (pcc.Game is MEGame.ME2)
                        {
                            if (string.IsNullOrEmpty(line.Path)) { continue; }
                            soundEvent = pcc.FindExport(line.Path.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            if (eventRefs[line.Index].Value == 0) { continue; }
                            soundEvent = pcc.GetUExport(eventRefs[line.Index].Value);
                        }

                        // We can't really do anything else if the sound event is null
                        if (soundEvent == null) { continue; }

                        line.Path = soundEvent.FullPath;

                        if (!pcc.Game.IsGame1())
                        {
                            if (pcc.Game is MEGame.LE2)
                            {
                                ArrayProperty<StructProperty> references = soundEvent.GetProperty<ArrayProperty<StructProperty>>("References");
                                if ((references == null) || (references.Count == 0)) { continue; }
                                StructProperty relationships = references[0].GetProp<StructProperty>("Relationships");
                                if (relationships == null) { continue; }
                                ArrayProperty<ObjectProperty> streams = relationships.GetProp<ArrayProperty<ObjectProperty>>("Streams");
                                if ((streams == null) || (streams.Count == 0)) { continue; }

                                foreach (ObjectProperty streamRef in streams)
                                {
                                    ExportEntry stream = pcc.GetUExport(streamRef.Value);
                                    if (stream == null) { continue; }

                                    stream.ObjectName = stream.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                                    NameProperty bankName = stream.GetProperty<NameProperty>("BankName");
                                    if (bankName == null) { continue; }
                                    bankName.Value = bankName.Value.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                                    stream.WriteProperty(bankName);
                                }
                            }
                            else
                            {
                                // Update the WwiseStreams
                                WwiseEvent wwiseEventBin = soundEvent.GetBinaryData<WwiseEvent>();
                                foreach (WwiseEvent.WwiseEventLink link in wwiseEventBin.Links)
                                {
                                    foreach (int stream in link.WwiseStreams)
                                    {
                                        if (stream == 0) { continue; }

                                        ExportEntry wwiseStream = pcc.GetUExport(stream);

                                        wwiseStream.ObjectName = wwiseStream.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);

                                        // This is similar to the step for LE2, but the general way of getting to it is more similar
                                        // to the LE3/ME3 way
                                        if (pcc.Game is MEGame.ME2)
                                        {
                                            NameProperty bankName = wwiseStream.GetProperty<NameProperty>("BankName");
                                            if (bankName == null) { continue; }
                                            bankName.Value = bankName.Value.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                                            wwiseStream.WriteProperty(bankName);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                fxaExport.WriteBinary(faceFXAnimSet);
            }
        }

        // HELPER FUNCTIONS
        #region Helper functions
        /// <summary>
        /// Checks if a dest position is within a given distance of the origin.
        /// </summary>
        /// <param name="origin">Origin position.</param>
        /// <param name="dest">Dest position to check.</param>
        /// <param name="dist">Max distance from origin.</param>
        /// <returns>True if the dest is within dist</returns>
        private static bool InDist(float origin, float dest, float dist)
        {
            return Math.Abs((dest - origin)) <= dist;
        }

        /// <summary>
        /// Generate a default ExpressionGUID
        /// </summary>
        /// <returns>ExpressionGUID StructProperty</returns>
        private static StructProperty GenerateExpressionGUID()
        {
            PropertyCollection props = new PropertyCollection();
            props.Add(new IntProperty(0, "A"));
            props.Add(new IntProperty(0, "B"));
            props.Add(new IntProperty(0, "C"));
            props.Add(new IntProperty(0, "D"));

            return new StructProperty("Guid", props, "ExpressionGUID", true);
        }

        private static void ShowError(string errMsg)
        {
            MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
        }

        /// <summary>
        /// Prompts the user for an int, verifying that the int is valid.
        /// </summary>
        /// <param name="msg">Message to display for the prompt.</param>
        /// <param name="err">Error message to display.</param>
        /// <param name="biggerThan">Number the input must be bigger than. If not provided -2,147,483,648 will be used.</param>
        /// <param name="title">Title for the prompt.</param>
        /// <returns>The input int.</returns>
        private static int promptForInt(string msg, string err, int biggerThan = -2147483648, string title = "")
        {
            if (PromptDialog.Prompt(null, msg, title) is string stringPrompt)
            {
                int intPrompt;
                if (string.IsNullOrEmpty(stringPrompt) || !int.TryParse(stringPrompt, out intPrompt) || !(intPrompt > biggerThan))
                {
                    MessageBox.Show(err, "Warning", MessageBoxButton.OK);
                    return -1;
                }
                return intPrompt;
            }
            return -1;
        }

        /// <summary>
        /// Gets the common prefix of two strings. Assumes both only difer at the end.
        /// </summary>
        /// <param name="s1">First string.</param>
        /// <param name="s2">Second string.</param>
        /// <returns>Union of input strings.</returns>
        private static string GetCommonPrefix(string s1, string s2)
        {
            string union = "";
            for (int w = 0, b = 0; w < s1.Length && b < s2.Length; w++, b++)
            {
                if (char.ToLower(s1[w]) == char.ToLower(s2[b]))
                {
                    union += char.ToLower(s1[w]);
                }
                else
                {
                    break;
                }
            }
            return union;
        }

        /// <summary>
        /// Generates a FNV132 hash of the given name.
        /// IMPORTANT: This may not be compeletely bug-free or may be missing a couple of details, but so far it works.
        /// </summary>
        /// <param name="name"></param>
        /// <returns>The decimal representation of the hash.</returns>
        private static uint GetBankId(string name)
        {
            byte[] bytedName = Encoding.ASCII.GetBytes(name.ToLower()); // Wwise automatically lowecases the input

            // FNV132 hashing algorithm
            uint hash = 2166136261;
            foreach (byte namebyte in bytedName)
            {
                hash = ((hash * 16777619) ^ namebyte) & 0xFFFFFFFF;
            }
            return hash;
        }

        /// <summary>
        /// Convert a big endian hex to its little endian representation.
        /// </summary>
        /// <param name="bigEndian">Endian to convert.</param>
        /// <returns>Little endian.</returns>
        private static string BigToLittleEndian(string bigEndian)
        {
            byte[] asCurrentEndian = new byte[4];
            string littleEndian = "";
            for (int i = 0; i < 4; i++)
            {
                asCurrentEndian[i] = Convert.ToByte(bigEndian.Substring(i * 2, 2), 16);
                littleEndian = $"{asCurrentEndian[i]:X2}{littleEndian}";
            }
            return littleEndian;
        }

        /// <summary>
        /// Create a var link with custom properties.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="name">LinkDesc.</param>
        /// <returns>The varLink StructProperty.</returns>
        private static StructProperty CreateVarLink(IMEPackage pcc, string name)
        {
            PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(pcc.Game, "SeqVarLink", true);

            int minVars = name == "Anchor" ? 1 : 0;
            int maxVars = name == "Anchor" ? 1 : 255;

            props.AddOrReplaceProp(new StrProperty(name, "LinkDesc"));
            int index = pcc.FindImport("Engine.SeqVar_Object").UIndex;
            props.AddOrReplaceProp(new ObjectProperty(index, "ExpectedType"));
            props.AddOrReplaceProp(new IntProperty(minVars, "MinVars"));
            props.AddOrReplaceProp(new IntProperty(maxVars, "MaxVars"));
            return new StructProperty("SeqVarLink", props);
        }
        #endregion
    }
}