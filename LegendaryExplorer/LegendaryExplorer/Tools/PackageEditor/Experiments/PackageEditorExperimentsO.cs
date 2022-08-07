using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
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

        private static void ShowError(string errMsg)
        {
            MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
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
    }
}