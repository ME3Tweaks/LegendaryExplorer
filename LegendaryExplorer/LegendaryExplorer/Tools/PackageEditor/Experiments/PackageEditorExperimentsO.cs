﻿using LegendaryExplorer.Dialogs;
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
        /// Removes SMC references to a SkeletalMesh or StaticMesh within a given distance.
        /// </summary>
        /// <param name="pew">Current PE instance.</param>
        public static void SMRefRemover(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem == null || pew.SelectedItem.Entry == null) { return; }

            if (!(pew.SelectedItem.Entry.ClassName is "SkeletalMesh" or "StaticMesh"))
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
                float origin;
                if (!float.TryParse(strOrigin, out origin))
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
                float dist;
                if (!float.TryParse(strDist, out dist))
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

            ExportEntry mesh = (ExportEntry)pew.SelectedItem.Entry;
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
            foreach (ExportEntry reference in references)
            {
                if (reference.ClassName == "StaticMeshComponent")
                {
                    switch (reference.Parent.ClassName)
                    {
                        case "StatichMeshCollectionActor":
                            StaticMeshCollectionActor parent = ObjectBinary.From<StaticMeshCollectionActor>((ExportEntry)reference.Parent);
                            UIndex uindex = new(reference.UIndex);
                            int smcaIndex = parent.Components.IndexOf(uindex);
                            float destX, destY, destZ;
                            ((destX, destY, destZ), _, _) = parent.LocalToWorldTransforms[smcaIndex].UnrealDecompose();

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

                if (reference.ClassName == "SkeletalMeshComponent")
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

    }
}