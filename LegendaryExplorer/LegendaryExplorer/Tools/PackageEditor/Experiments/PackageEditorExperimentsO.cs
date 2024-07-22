using LegendaryExplorer.Dialogs;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.SharpDX;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows;
using static LegendaryExplorer.Misc.ExperimentsTools.PackageAutomations;
using static LegendaryExplorer.Misc.ExperimentsTools.SequenceAutomations;
using static LegendaryExplorer.Misc.ExperimentsTools.SharedMethods;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    /// <summary>
    /// Class for 'Others' package experiments who aren't main devs
    /// </summary>
    static class PackageEditorExperimentsO
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
                        var camActor = PromptForStr("Name of camera actor:", "Not a valid camera actor name.");
                        if (!string.IsNullOrEmpty(camActor))
                        {
                            MatineeHelper.AddPreset(preset, interp, game, camActor);
                        }
                        break;

                    case "Actor":
                        var actActor = PromptForStr("Name of actor:", "Not a valid actor name.");
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
                        var actor = PromptForStr("Name of gesture actor:", "Not a valid gesture actor name.");
                        if (!string.IsNullOrEmpty(actor))
                        {
                            MatineeHelper.AddPreset(preset, interp, game, actor);
                        }
                        break;
                }
            }
            return;
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
                    if (!s.Contains(':'))
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
                                    NameProperty nameProperty = (NameProperty)property.Properties.First(prop => prop.Name.Equals("ParameterName"));
                                    string name = nameProperty.Value;
                                    if (string.IsNullOrEmpty(name)) { return false; }
                                    return !name.Equals(pAv.Key, StringComparison.OrdinalIgnoreCase);
                                }).ToList();

                                PropertyCollection props = [];

                                // Generate and add the ExpressionGUID
                                PropertyCollection expressionGUIDprops =
                                [
                                    new IntProperty(0, "A"),
                                    new IntProperty(0, "B"),
                                    new IntProperty(0, "C"),
                                    new IntProperty(0, "D"),
                                ];

                                props.Add(new StructProperty("Guid", expressionGUIDprops, "ExpressionGUID", true));

                                if (parameterType.Equals("Vector"))
                                {
                                    PropertyCollection color =
                                    [
                                        new FloatProperty(pAv.Value[0], "R"),
                                        new FloatProperty(pAv.Value[1], "G"),
                                        new FloatProperty(pAv.Value[2], "B"),
                                        new FloatProperty(pAv.Value[3], "A"),
                                    ];

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

                bool addBool = MessageBoxResult.Yes == MessageBox.Show(
                        "Add boolean property if an object does not not have it?",
                        "Add bool", MessageBoxButton.YesNo);

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
                        if ((currProp == null) && !addBool) { continue; }
                        export.WriteProperty(new BoolProperty(state, boolName));
                    }
                    pcc.Save();
                }
            }
            MessageBox.Show("All bools were sucessfully set", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Modify the hair morph targets of a male headmorph to make it bald.
        /// </summary>
        /// <param name="pew">Current PE window.</param>
        public static void Baldinator(PackageEditorWindow pew)
        {
            if (pew.SelectedItem == null || pew.SelectedItem.Entry == null || pew.Pcc == null) { return; }

            MEGame game = pew.Pcc.Game;

            string baldPccPath = Path.Combine(MEDirectories.GetCookedPath(game),
                game.IsGame3() ? "BioD_CitHub_Underbelly.pcc" : game.IsGame2() ? "BioH_Wilson.pcc" : "BIOA_FRE32_00_DSG.pcc");

            string baldMorphName = game.IsGame3() ? "BioChar_CitHub.Faces.HMM_Deco_1" :
                game.IsGame2() ? "BIOG_Hench_FAC.HMM.hench_wilson" : "BIOA_UNC_FAC.HMM.Plot.FRE32_BioticLeader";

            MorphMaleHair(pew, "bald", baldPccPath, baldMorphName);
        }

        /// <summary>
        /// Modify the hair morph targets of a male headmorph to make it rollins.
        /// </summary>
        /// <param name="pew">Current PE window.</param>
        public static void Rollinator(PackageEditorWindow pew)
        {
            if (pew.SelectedItem == null || pew.SelectedItem.Entry == null || pew.Pcc == null) { return; }

            MEGame game = pew.Pcc.Game;

            string rollinPccPath = Path.Combine(MEDirectories.GetCookedPath(game),
                game.IsGame3() ? "BioD_CitHub_Dock.pcc" : game.IsGame2() ? "BIOA_STA60_07A_DSG.pcc" : "BIOA_STA60_07A_DSG.pcc");

            string rollinMorphName = game.IsGame3() ? "BioChar_CitHub.Faces.cit_news_announcer" :
                game.IsGame2() ? "BIOA_STA_FAC.HMM.Plot.rp107_keeler" : "BIOA_STA_FAC.HMM.Plot.rp107_keeler";

            MorphMaleHair(pew, "rollins", rollinPccPath, rollinMorphName);
        }

        /// <summary>
        /// Modify the hair morph targets of a male headmorph to make it like the parameters.
        /// </summary>
        /// <param name="pew">Current PE window.</param>
        /// <param name="name">Name of the modification, used for messages.</param>
        /// <param name="modelPccPath">Path to the pcc containing the morph to copy.</param>
        /// <param name="morphName">Instanced name of the morph in the modelPccPath pcc.</param>
        public static void MorphMaleHair(PackageEditorWindow pew, string name, string modelPccPath, string morphName)
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
            if (!File.Exists(modelPccPath))
            {
                ShowError($"Could not find the {Path.GetFileNameWithoutExtension(modelPccPath)} file. Please ensure the vanilla game files have not been modified.");
                return;
            }
            using IMEPackage modelPcc = MEPackageHandler.OpenMEPackage(modelPccPath);

            ExportEntry modelMorph = modelPcc.FindExport(morphName);
            if (modelMorph == null || modelMorph.ClassName != "BioMorphFace")
            {
                ShowError($"Could not find the {name} headmorph. Please ensure the vanilla game files have not been modified.");
                return;
            }

            ExportEntry targetMorph = (ExportEntry)pew.SelectedItem.Entry;

            BioMorphFace modelMorphFace = ObjectBinary.From<BioMorphFace>(modelMorph);
            BioMorphFace targetMorphFace = ObjectBinary.From<BioMorphFace>(targetMorph);

            if (modelMorphFace.LODs[0].Length != targetMorphFace.LODs[0].Length)
            {
                ShowError($"The selected headmorph differs from the expected one. This experiment only works for male human headmorphs.");
                return;
            }

            List<string> targetNames = new()
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
                    targetMorphFace.LODs[l][i].X = modelMorphFace.LODs[l][i].X;
                    targetMorphFace.LODs[l][i].Y = modelMorphFace.LODs[l][i].Y;
                    targetMorphFace.LODs[l][i].Z = modelMorphFace.LODs[l][i].Z;
                }
            }

            targetMorph.WriteBinary(targetMorphFace);
            MessageBox.Show($"The selected morph's hair has been cut and offered as a sacrifice to the Reape-- The morph is now {name}.", "Success", MessageBoxButton.OK);
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

            int newConvResRefID = PromptForInt("New ConvResRefID:", "Not a valid ref id. It must be positive integer", -1, "New ConvResRefID");
            if (newConvResRefID == -1) { return; }

            int convNodeIDBase = PromptForInt("New ConvNodeID base range:", "Not a valid base. It must be positive integer", -1, "New NodeID range");
            if (convNodeIDBase == -1) { return; }

            bool updateAllAudioIDs = false;
            bool updateOnlyWwiseBankID = false;
            if (!pew.Pcc.Game.IsGame1())
            {
                updateAllAudioIDs = PromptForBool("Update all audio IDs?\n" +
                    "This will give the give the conversation fully unique IDs," +
                    "hashing the names for the WwiseBank and Stream IDs, and using random generation for the Event IDs," +
                    "then doing a blind replacement in the binary of the bank. There is a very small chance it may replace something" +
                    "it shouldn't, so do this at your own risk.", "Update all audio IDs");

                if (!updateAllAudioIDs)
                {
                    updateOnlyWwiseBankID = PromptForBool("Update the ID of the WwiseBank?\n" +
                        "In general it's safe and better to do so, but there may be edge cases" +
                        "where doing so may overwrite parts of the WwiseBank binary that are not its ID.",
                        "Update WwiseBank ID");
                }
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

            // Check that the conversation has a normal package structure. Otherwise it can lead to unexpected edge cases when trying to gather
            // the different sound elements.
            string structureCheckResult = CheckConversationStructure(pew.Pcc, bioConversation, conversation);
            if (!string.IsNullOrEmpty(structureCheckResult))
            {
                ShowError(structureCheckResult);
                return;
            }

            // Rename the conversation, its package, the FXAs, and the related audio elements
            string conversationResult = RenameConversation(pew.Pcc, bioConversation, conversation, newName, updateAllAudioIDs, updateOnlyWwiseBankID);
            if (!string.IsNullOrEmpty(conversationResult))
            {
                ShowError(conversationResult);
                return;
            }

            // Change the conversations ResRefID an the ConvNodes IDs
            ChangeConvoIDandConvNodeIDs(bioConversation, (ExportEntry)conversation.Sequence, newConvResRefID, convNodeIDBase);

            // Clean the sequence of unneeded objects and keep only Conversation InterpGroups and VOElements InterpTracks
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
        /// <param name="sequence">Sequence to clean.</param>
        /// <param name="trashItems">Whether to trash unused groups and tracks.</param>
        /// <param name="bringTrash">Whether to keep all objects that point to conversation classes, or only essential ones.</param>
        /// <param name="cleanGroups">Whether to clean InterpGroups and InterpTracks.</param>
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
                    List<List<OutputLink>> outboundLinks = KismetHelper.GetOutputLinksOfNode(seqObj);
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
                        IEnumerable<StructProperty> varLinks = seqObj.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");

                        if (varLinks != null)
                        {
                            List<StructProperty> newVarLinks = new();

                            StructProperty dataLink = varLinks.FirstOrDefault(link =>
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
                    List<List<OutputLink>> outboundLinks = KismetHelper.GetOutputLinksOfNode(seqObj);
                    if (outboundLinks.Count > 0)
                    {
                        List<List<OutputLink>> filteredOutboundLinks = new();
                        foreach (List<OutputLink> outboundLink in outboundLinks)
                        {
                            // Add to filteredOutboundLinks the elements of this outbound link that connect to a valid class
                            filteredOutboundLinks.Add(outboundLink
                                .Where(link =>
                                    link == null || validClasses.Contains(link.LinkedOp.ClassName, StringComparer.OrdinalIgnoreCase)
                                ).ToList());
                        }
                        KismetHelper.WriteOutputLinksToNode(seqObj, filteredOutboundLinks);
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
        /// <param name="sequence">Sequence to clean.</param>
        public static List<IEntry> CleanSequenceInterpDatas(IMEPackage pcc, ExportEntry sequence)
        {
            List<IEntry> itemsToTrash = new();

            List<IEntry> interpDatas = new(KismetHelper.GetAllSequenceElements(sequence)
                .Where(el => el.ClassName == "InterpData"));

            // Keep only InterpGroups named "Conversation" and only the BioEvtSysTrackVOElements InterpTracks
            foreach (ExportEntry interpData in interpDatas.Cast<ExportEntry>())
            {
                ArrayProperty<ObjectProperty> interpGroupsRefs = interpData.GetProperty<ArrayProperty<ObjectProperty>>("InterpGroups");
                if (interpGroupsRefs == null) { continue; }
                List<ObjectProperty> filteredGroupsRefs = [];

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

        /// <summary>
        /// Wrapper for ChangeConvoIDandConvNodeIDs, so it can used as a full experiment on its own.
        /// </summary>
        /// <param name="pew">Current PE instance.</param>
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

            int newConvResRefID = PromptForInt("New ConvResRefID:", "Not a valid ref id. It must be positive integer", -1, "New ConvResRefID");
            if (newConvResRefID == -1) { return; }

            int convNodeIDBase = PromptForInt("New ConvNodeID base range:", "Not a valid base. It must be positive integer", -1, "New NodeID range");
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
        /// <param name="bioConversation">Conversation to edit.</param>
        /// <param name="sequence">Conversation's sequence.</param>
        /// <param name="newConvResRefID">newConvResRefID to set.</param>
        /// <param name="convNodeIDBase">convNodeIDBase to use.</param>
        public static void ChangeConvoIDandConvNodeIDs(ExportEntry bioConversation, ExportEntry sequence, int newConvResRefID, int convNodeIDBase)
        {
            PropertyCollection conversationProps = bioConversation.GetProperties();

            // Update the conversation's refId
            IntProperty m_nResRefID = new(newConvResRefID, "m_nResRefID");
            conversationProps.AddOrReplaceProp(m_nResRefID);

            // Update the convNodes nodeId and convResRefId
            int count = 0;
            List<IEntry> convNodes = new(KismetHelper.GetAllSequenceElements(sequence)
                .Where(el => el.ClassName == "BioSeqEvt_ConvNode"));

            Dictionary<int, int> remappedIDs = new(); // Save references of old id for update of entry and reply lists
            foreach (ExportEntry convNode in convNodes.Cast<ExportEntry>())
            {
                PropertyCollection nodeProps = convNode.GetProperties();

                IntProperty oldNodeID = nodeProps.GetProp<IntProperty>("m_nNodeID");
                if (oldNodeID == null) { continue; }

                remappedIDs.Add(oldNodeID.Value, convNodeIDBase + count);

                IntProperty m_nNodeID = new(convNodeIDBase + count, "m_nNodeID");
                IntProperty m_nConvResRefID = new(newConvResRefID, "m_nConvResRefID");
                nodeProps.AddOrReplaceProp(m_nNodeID);
                nodeProps.AddOrReplaceProp(m_nConvResRefID);

                convNode.WriteProperties(nodeProps);
                count++;
            }

            // Update the nExportIDs of the Entry list
            ArrayProperty<StructProperty> entryNodes = conversationProps.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
            if (entryNodes != null)
            {
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
            }

            // Update the nExportIDs of the Reply list
            ArrayProperty<StructProperty> replyNodes = conversationProps.GetProp<ArrayProperty<StructProperty>>("m_ReplyList");
            if (replyNodes != null)
            {
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
            }

            bioConversation.WriteProperties(conversationProps);
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

            string newName = PromptDialog.Prompt(null, "New conversation name:", "New name");
            // Check that the new name is not empty, no longe than 255, and doesn't contain white-spaces or symbols aside from _ or -
            if (string.IsNullOrEmpty(newName) || newName.Length > 240 || newName.Any(c => char.IsWhiteSpace(c) || (!(c is '_' or '-') && !char.IsLetterOrDigit(c))))
            {
                ShowError("Invalid new name. It must not be empty, be longer than 240 characters, or contain whitespaces or symbols aside from '-' and '_'");
                return;
            }

            bool updateAllAudioIDs = false;
            bool updateOnlyWwiseBankID = false;
            if (!pew.Pcc.Game.IsGame1())
            {
                updateAllAudioIDs = PromptForBool("Update all audio IDs?\n" +
                    "This will give the give the conversation fully unique IDs," +
                    "hashing the names for the WwiseBank and Stream IDs, and using random generation for the Event IDs," +
                    "then doing a blind replacement in the binary of the bank. There is a very small chance it may replace something" +
                    "it shouldn't, so do this at your own risk.", "Update all audio IDs");

                if (!updateAllAudioIDs)
                {
                    updateOnlyWwiseBankID = PromptForBool("Update the ID of the WwiseBank?\n" +
                        "In general it's safe and better to do so, but there may be edge cases" +
                        "where doing so may overwrite parts of the WwiseBank binary that are not its ID.",
                        "Update WwiseBank ID");
                }
            }

            ExportEntry bioConversation = (ExportEntry)pew.SelectedItem.Entry;

            // Load the conversation. We use ConversationExtended since it aggregates most of the elements we'll need to
            // operate on. Is it overkill? Yes. Does it get the job done more cleanly and safely? yes.
            ConversationExtended conversation = new(bioConversation);
            conversation.LoadConversation(TLKManagerWPF.GlobalFindStrRefbyID, true);

            // Check that the conversation has a normal package structure. Otherwise it can lead to unexpected edge cases when trying to gather
            // the different sound elements.
            string structureCheckResult = CheckConversationStructure(pew.Pcc, bioConversation, conversation);
            if (!string.IsNullOrEmpty(structureCheckResult))
            {
                ShowError(structureCheckResult);
                return;
            }

            string conversationResult = RenameConversation(pew.Pcc, bioConversation, conversation, newName, updateAllAudioIDs, updateOnlyWwiseBankID);
            if (!string.IsNullOrEmpty(conversationResult))
            {
                ShowError(conversationResult);
                return;
            }

            MessageBox.Show("Conversation renamed successfully.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Rename a conversation, changing the WwiseBank name and FXAs and related elements too.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">BioConversation entry to edit.</param>
        /// <param name="conversation">Loaded conversation to edit.</param>
        /// <param name="newName">New name.</param>
        /// <param name="updateAllAudioIDs">Whether to update the IDs of Bank, Events, and Streams.</param>
        /// <param name="updateOnlyWwiseBankID">Whether to only update the ID of the Bank.</param>
        /// <returns>Empty string if no errors, otherwise an error message to display to the user.</returns>
        public static string RenameConversation(IMEPackage pcc, ExportEntry bioConversation, ConversationExtended conversation, string newName, bool updateAllAudioIDs, bool updateOnlyWwiseBankID)
        {
            string oldBioConversationName = bioConversation.ObjectName;
            string oldName = GetOldName(pcc, bioConversation, conversation);

            if (string.IsNullOrEmpty(oldName))
            {
                string nameSource = pcc.Game.IsGame1() ? "TlkSet" : "WwiseBank";
                return $"A common name could not be found between the conversation name and the {nameSource} name.\n" +
                    $"Make sure that the elements related to the conversation follow the conventional naming scheme for the game.";
            }

            RenamePackages(pcc, bioConversation, conversation, oldName, newName);

            if (pcc.Game.IsGame1())
            {
                ObjectProperty m_oTlkFileSet = bioConversation.GetProperty<ObjectProperty>("m_oTlkFileSet");
                ExportEntry tlkFileSet = null;
                if (m_oTlkFileSet != null && m_oTlkFileSet.Value != 0 && pcc.TryGetUExport(m_oTlkFileSet.Value, out tlkFileSet))
                {
                    string name = tlkFileSet.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                    tlkFileSet.ObjectName = new NameReference(name, tlkFileSet.ObjectName.Number);
                }

                RenameISACTAudio(pcc, bioConversation, tlkFileSet, oldName, newName, conversation);
            }
            else
            {
                RenameWwiseAudio(pcc, conversation.WwiseBank, bioConversation, oldName, newName, updateAllAudioIDs, updateOnlyWwiseBankID, conversation);
            }

            // Rename bioConversation after the audio, since it needs the old name
            string newBioConversationName = oldBioConversationName.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
            bioConversation.ObjectName = newBioConversationName;

            return "";
        }

        /// <summary>
        /// Get the old name found in all pieces of the conversation by getting the union of the bioconversation and the wwise bank names,
        /// or tlk file set in ME1.
        /// Assumes that both begin the same, since that's the behavior in all vanilla occurences, and helps keep the logic simple.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">BioConversation to get the name from.</param>
        /// <param name="conversation">Loaded Conversation, to avoid copy/pasting some its code.</param>
        /// <returns>Old name found in the bioConversation.</returns>
        private static string GetOldName(IMEPackage pcc, ExportEntry bioConversation, ConversationExtended conversation)
        {
            string oldBioConversationName = bioConversation.ObjectName;
            string oldName;

            if (pcc.Game.IsGame1())
            {
                ObjectProperty m_oTlkFileSet = bioConversation.GetProperty<ObjectProperty>("m_oTlkFileSet");
                if (m_oTlkFileSet != null && pcc.TryGetUExport(m_oTlkFileSet.Value, out ExportEntry tlkFileSet))
                {
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
                oldName = GetCommonPrefix(conversation.WwiseBank.ObjectName, bioConversation.ObjectName);
            }

            return oldName;
        }

        /// <summary>
        /// Rename the packages associated with the bioConversation.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">BioConveration to get the packages from.</param>
        /// <param name="conversation">Loaded Conversation, to avoid copy/pasting some its code.</param>
        /// <param name="oldName">Old name to replace in packages.</param>
        /// <param name="newName">Name to replace old name with.</param>
        private static void RenamePackages(IMEPackage pcc, ExportEntry bioConversation, ConversationExtended conversation,
            string oldName, string newName)
        {
            // Replace package's name
            if (bioConversation.idxLink != 0)
            {
                ExportEntry link = pcc.GetUExport(bioConversation.idxLink);
                if (link.ClassName == "Package")
                {
                    string name = link.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                    link.ObjectName = new NameReference(name, link.ObjectName.Number);
                }
            }

            // Replace name of the sound, sequence, and FXA package, which is separate in ME1
            if (pcc.Game.IsGame1())
            {
                ExportEntry link = pcc.GetUExport(conversation.Sequence.idxLink);
                if (link.ClassName == "Package")
                {
                    string name = link.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                    link.ObjectName = new NameReference(name, link.ObjectName.Number);
                }
            }
            // Replace name of the sounds package, which is separate in ME2
            if (pcc.Game.IsGame2())
            {
                ExportEntry link = pcc.GetUExport(conversation.WwiseBank.idxLink);
                if (link.ClassName == "Package")
                {
                    string name = link.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                    link.ObjectName = new NameReference(name, link.ObjectName.Number);
                }
            }
        }

        /// <summary>
        /// Wrapper for RenameAudio so it can used as a full experiment on its own.
        /// </summary>
        /// <param name="pew">Current PE instance.</param>
        public static void RenameAudioExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.SelectedItem.Entry.ClassName is not "BioConversation")
            {
                ShowError("Selected export is not a BioConversation");
                return;
            }

            bool updateAllAudioIDs = false;
            bool updateOnlyWwiseBankID = false;
            if (!pew.Pcc.Game.IsGame1())
            {
                updateAllAudioIDs = PromptForBool("Update all audio IDs?\n" +
                    "This will give the give the conversation fully unique IDs," +
                    "hashing the names for the WwiseBank and Stream IDs, and using random generation for the Event IDs," +
                    "then doing a blind replacement in the binary of the bank. There is a very small chance it may replace something" +
                    "it shouldn't, so do this at your own risk.", "Update all audio IDs");

                if (!updateAllAudioIDs)
                {
                    updateOnlyWwiseBankID = PromptForBool("Update the ID of the WwiseBank?\n" +
                        "In general it's safe and better to do so, but there may be edge cases" +
                        "where doing so may overwrite parts of the WwiseBank binary that are not its ID.",
                        "Update WwiseBank ID");
                }
            }

            string newName = PromptDialog.Prompt(null, "New common name:", "New name");
            // Check that the new name is not empty, no longe than 255, and doesn't contain white-spaces or symbols aside from _ or -
            if (string.IsNullOrEmpty(newName) || newName.Length > 240 || newName.Any(c => char.IsWhiteSpace(c) || (!(c is '_' or '-') && !char.IsLetterOrDigit(c))))
            {
                ShowError("Invalid new name. It must not be empty, be longer than 240 characters, or contain whitespaces or symbols aside from '-' and '_'");
                return;
            }

            ExportEntry bioConversation = (ExportEntry)pew.SelectedItem.Entry;

            // Load the conversation. We use ConversationExtended since it aggregates most of the elements we'll need to
            // operate on. Is it overkill? Yes. Does it get the job done more cleanly and safely? yes.
            ConversationExtended conversation = new(bioConversation);
            conversation.LoadConversation(TLKManagerWPF.GlobalFindStrRefbyID, true);

            // Check that the conversation has a normal package structure. Otherwise it can lead to unexpected edge cases when trying to gather
            // the different sound elements.
            string structureCheckResult = CheckConversationStructure(pew.Pcc, bioConversation, conversation);
            if (!string.IsNullOrEmpty(structureCheckResult))
            {
                ShowError(structureCheckResult);
                return;
            }

            string oldName = GetOldName(pew.Pcc, bioConversation, conversation);

            if (pew.Pcc.Game.IsGame1())
            {
                ObjectProperty m_oTlkFileSet = bioConversation.GetProperty<ObjectProperty>("m_oTlkFileSet");
                ExportEntry tlkFileSet = null;
                if (m_oTlkFileSet != null)
                {
                    pew.Pcc.TryGetUExport(m_oTlkFileSet.Value, out tlkFileSet);
                }
                RenameISACTAudio(pew.Pcc, bioConversation, tlkFileSet, oldName, newName, conversation);
            }
            else
            {
                RenameWwiseAudio(pew.Pcc, conversation.WwiseBank, bioConversation, oldName, newName, updateAllAudioIDs, updateOnlyWwiseBankID, conversation);
            }

            MessageBox.Show($"Audio renamed successfully.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Change the WwiseBank, WwiseEvents, and WwiseStreams' names, and update their IDs accordingly.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="wwiseBankEntry">WwiseBank entry to edit.</param>
        /// <param name="bioConversation">Selected BioConversation.</param>
        /// <param name="oldName">Old name to replace.</param>
        /// <param name="newName">New name for the elements.</param>
        /// <param name="updateAllAudioIDs">Whether to update the IDs of the Bank, Events, and Streams.</param>
        /// <param name="updateOnlyWwiseBankID">Whether to only update the ID of the Bank.</param>
        /// <param name="conversation">Loaded Conversation, to avoid copy/pasting some its code.</param>
        public static void RenameWwiseAudio(IMEPackage pcc, ExportEntry wwiseBankEntry, ExportEntry bioConversation,
            string oldName, string newName, bool updateAllAudioIDs, bool updateOnlyWwiseBankID, ConversationExtended conversation)
        {
            string oldWwiseBankName = wwiseBankEntry.ObjectName;
            string newWwiseBankName = oldWwiseBankName.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);

            List<ExportEntry> wwiseEvents = GetAudioExports(pcc, bioConversation, AudioClass.WwiseEvent, conversation);
            List<ExportEntry> wwiseStreams = GetAudioExports(pcc, bioConversation, AudioClass.WwiseStream, conversation);

            // RenameWwiseEvents(pcc, wwiseEvents, newName);
            RenameWwiseStreams(pcc, wwiseStreams, oldName, newName);

            if (updateAllAudioIDs)
            {
                Random random = new();

                Dictionary<uint, uint> idPairs = new();
                idPairs.AddRange(UpdateIDs_EXPERIMENTAL(wwiseEvents, random));
                idPairs.AddRange(UpdateIDs_EXPERIMENTAL(wwiseStreams));

                UpdateAudioIDs_EXPERIMENTAL(wwiseBankEntry, newWwiseBankName, idPairs, random);
            }
            else
            {
                if (updateOnlyWwiseBankID)
                {
                    // Dictionary<uint, uint> wwiseEventsIDs = UpdateIDs(wwiseEvents);
                    // Dictionary<uint, uint> wwiseStreamsIDs = UpdateIDs(wwiseStreams);

                    UpdateAudioIDs_LEGACY(wwiseBankEntry, newWwiseBankName, null, null);
                }
            }

            wwiseBankEntry.ObjectName = newWwiseBankName;

            RenameFXAs(pcc, bioConversation, oldName, newName);
        }

        /// <summary>
        /// Rename the SoundCues, and SoundNodeWaves' names.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">Selected BioConversation.</param>
        /// <param name="tlkFileSet">TLK FileSet export.</param>
        /// <param name="oldName">Old name to replace.</param>
        /// <param name="newName">New name for the elements.</param>
        /// <param name="conversation">Loaded Conversation, to avoid copy/pasting some its code.</param>
        public static void RenameISACTAudio(IMEPackage pcc, ExportEntry bioConversation, ExportEntry tlkFileSet,
            string oldName, string newName, ConversationExtended conversation)
        {
            List<ExportEntry> nodeWaves = GetAudioExports(pcc, bioConversation, AudioClass.SoundNodeWave, conversation);

            RenameSoundNodeWaves(pcc, nodeWaves, oldName, newName);

            RenameFXAs(pcc, bioConversation, oldName, newName);
        }

        /// <summary>
        /// Update a WwiseBank's ID by hashing a new name and changing it in the binary data where appropriate.
        /// </summary>
        /// <param name="wwiseBankEntry">The WwiseBank entry to edit.</param>
        /// <param name="newWwiseBankName">The new wwise bank name. Needed for ME3 ReferencedBanks.</param>
        /// <param name="idPairs">Dictionary<oldID, newID> Found IDs to update.</param>
        /// <param name="random">Random object to generate IDs.</param>
        public static void UpdateAudioIDs_EXPERIMENTAL(ExportEntry wwiseBankEntry, string newWwiseBankName,
            Dictionary<uint, uint> idPairs, Random random)
        {
            string oldBankName = wwiseBankEntry.ObjectName;

            (uint oldBankID, uint newBankID) = UpdateID_EXPERIMENTAL(wwiseBankEntry, null, newWwiseBankName);

            WwiseBank wwiseBank = wwiseBankEntry.GetBinaryData<WwiseBank>();
            // Update the bank id
            wwiseBank.ID = newBankID;

            idPairs.Add(oldBankID, newBankID);

            // Update referenced banks kvp that reference the old bank name
            IEnumerable<KeyValuePair<uint, string>> updatedBanks = wwiseBank.ReferencedBanks
                .Select(referencedBank =>
                {
                    if (referencedBank.Value.Equals(oldBankName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new KeyValuePair<uint, string>(newBankID, newWwiseBankName);
                    }
                    return referencedBank;
                });
            wwiseBank.ReferencedBanks = new(updatedBanks);

            // Gather all the IDs we don't know about yet
            foreach (WwiseBank.HIRCObject hirc in wwiseBank.HIRCObjects.Values)
            {
                if (hirc.ID != 0 && !idPairs.ContainsKey(hirc.ID))
                {
                    idPairs.Add(hirc.ID, GenerateRandomID(random));
                }
            }

            // Update references to old wwiseEvents' hashes, which are the ID of Event HIRCs.
            // Update references to old wwiseStreams' hashes, which are in the unknown bytes of Sound HIRCs.
            // Update references to old bank hash, which I'm certain is at the end of Event Action HIRCs,
            // but we check in all of them just in case.
            // byte[] bankIDArr = BitConverter.GetBytes(oldBankID);
            // byte[] newBankIDArr = BitConverter.GetBytes(newBankID);

            //foreach (WwiseBank.HIRCObject hirc in wwiseBank.HIRCObjects.Values)
            //{
            //if (hirc.Type == HIRCType.Event) // References a WwiseEvent
            //{
            //    if (wwiseEventIDs.TryGetValue(hirc.ID, out uint newEventID))
            //    {
            //        hirc.ID = newEventID;
            //    }
            //}
            //else if (hirc.Type == HIRCType.SoundSXFSoundVoice) // References a WwiseStream
            //{
            //    // 4 bytes ID is located at the start after 14 bytes
            //    Span<byte> streamIDSpan = hirc.unparsed.AsSpan(5..9);
            //    uint streamIDUInt = BitConverter.ToUInt32(streamIDSpan);

            //    if (wwiseStreamIDs.TryGetValue(streamIDUInt, out uint newStreamIDUInt))
            //    {
            //        byte[] newStreamIDArr = BitConverter.GetBytes(newStreamIDUInt);
            //        newStreamIDArr.CopyTo(streamIDSpan);
            //    }
            //}

            // Check for bank ID in all HIRCs, even though I'm almost certain it only appears
            // in Event Actions
            //    if (hirc.unparsed != null && hirc.unparsed.Length >= 4) // Only replace if not null and at least width of hash
            //    {
            //        Span<byte> bankIDSpan = hirc.unparsed.AsSpan(^4..);
            //        if (bankIDSpan.SequenceEqual(bankIDArr))
            //        {
            //            newBankIDArr.CopyTo(bankIDSpan);
            //        }
            //    }
            //}

            string bankBinaryAsString = Convert.ToHexString(wwiseBankEntry.GetBinaryData());

            // Blindly replace all the IDs found in the WwiseBank
            foreach (KeyValuePair<uint, uint> id in idPairs)
            {
                bankBinaryAsString = bankBinaryAsString.Replace(
                    BigToLittleEndian(string.Format("{0:X2}", id.Key).PadLeft(8, '0')),
                    BigToLittleEndian(string.Format("{0:X2}", id.Value).PadLeft(8, '0')),
                    StringComparison.OrdinalIgnoreCase);
            }

            wwiseBankEntry.WriteBinary(Convert.FromHexString(bankBinaryAsString));

        }

        /// <summary>
        /// Update a WwiseBank's ID by hashing a new name and changing it in the binary data where appropriate.
        /// </summary>
        /// <param name="wwiseBankEntry">The WwiseBank entry to edit.</param>
        /// <param name="newWwiseBankName">The new wwise bank name. Needed for ME3 ReferencedBanks.</param>
        /// <param name="wwiseEventIDs">Dictionary<oldID, newID> The event IDs to update</param>
        /// <param name="wwiseStreamIDs">Dictionary<oldID, newID> The stream IDs to update</param>
        public static void UpdateAudioIDs_LEGACY(ExportEntry wwiseBankEntry, string newWwiseBankName,
            Dictionary<uint, uint> wwiseEventIDs, Dictionary<uint, uint> wwiseStreamIDs)
        {
            string oldBankName = wwiseBankEntry.ObjectName;

            (uint oldBankID, uint newBankID) = UpdateID_LEGACY(wwiseBankEntry, newWwiseBankName);

            WwiseBank wwiseBank = wwiseBankEntry.GetBinaryData<WwiseBank>();
            // Update the bank id
            wwiseBank.ID = newBankID;

            // Update referenced banks kvp that reference the old bank name
            IEnumerable<KeyValuePair<uint, string>> updatedBanks = wwiseBank.ReferencedBanks
                .Select(referencedBank =>
                {
                    if (referencedBank.Value.Equals(oldBankName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new KeyValuePair<uint, string>(newBankID, newWwiseBankName);
                    }
                    return referencedBank;
                });
            wwiseBank.ReferencedBanks = new(updatedBanks);

            // DISABLED: Update references to old wwiseEvents' hashes, which are the ID of Event HIRCs.
            // DISABLED: Update references to old wwiseStreams' hashes, which are in the unknown bytes of Sound HIRCs.
            // Update references to old bank hash, which I'm certain is at the end of Event Action HIRCs,
            // but we check in all of them just in case.
            byte[] bankIDArr = BitConverter.GetBytes(oldBankID);
            byte[] newBankIDArr = BitConverter.GetBytes(newBankID);
            foreach (WwiseBank.HIRCObject hirc in wwiseBank.HIRCObjects.Values)
            {
                //if (hirc.Type == HIRCType.Event) // References a WwiseEvent
                //{
                //    if (wwiseEventIDs.TryGetValue(hirc.ID, out uint newEventID))
                //    {
                //        hirc.ID = newEventID;
                //    }
                //}
                //else if (hirc.Type == HIRCType.SoundSXFSoundVoice) // References a WwiseStream
                //{
                //    // 4 bytes ID is located at the start after 14 bytes
                //    Span<byte> streamIDSpan = hirc.unparsed.AsSpan(5..9);
                //    uint streamIDUInt = BitConverter.ToUInt32(streamIDSpan);
                //    if (wwiseStreamIDs.TryGetValue(streamIDUInt, out uint newStreamIDUInt))
                //    {
                //        byte[] newStreamIDArr = BitConverter.GetBytes(newStreamIDUInt);
                //        newStreamIDArr.CopyTo(streamIDSpan);
                //    }
                //}

                // Check for bank ID in all HIRCs, even though I'm almost certain it only appears
                // in Event Actions
                if (hirc.unparsed != null && hirc.unparsed.Length >= 4) // Only replace if not null and at least width of hash
                {
                    Span<byte> bankIDSpan = hirc.unparsed.AsSpan(^4..);
                    if (bankIDSpan.SequenceEqual(bankIDArr))
                    {
                        newBankIDArr.CopyTo(bankIDSpan);
                    }
                }
            }

            wwiseBankEntry.WriteBinary(wwiseBank);
        }

        /// <summary>
        /// Rename a list of WwiseEvents by addig a prefix.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="wwiseEvents">WwiseEvents to rename.</param>
        /// <param name="prefix">Prefix to add to the names.</param>
        private static void RenameWwiseEvents(IMEPackage pcc, List<ExportEntry> wwiseEvents, string prefix)
        {
            foreach (ExportEntry wwiseEvent in wwiseEvents)
            {
                if (!pcc.Game.IsGame1())
                {
                    wwiseEvent.ObjectName = new NameReference($"I{wwiseEvent.ObjectName.Name}", wwiseEvent.ObjectName.Number);
                }
            }
        }

        /// <summary>
        /// Rename a list of WwiseStreams.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="wwiseStreams">WwiseStreams to rename.</param>
        /// <param name="oldName">Old name to replace.</param>
        /// <param name="newName">New name to replace with.</param>
        private static void RenameWwiseStreams(IMEPackage pcc, List<ExportEntry> wwiseStreams, string oldName, string newName)
        {
            foreach (ExportEntry wwiseStream in wwiseStreams)
            {
                if (!pcc.Game.IsGame1())
                {
                    string name = wwiseStream.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                    wwiseStream.ObjectName = new NameReference(name, wwiseStream.ObjectName.Number);

                    if (pcc.Game.IsGame2())
                    {
                        NameProperty bankName = wwiseStream.GetProperty<NameProperty>("BankName");
                        if (bankName == null) { continue; }
                        bankName.Value = bankName.Value.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                        wwiseStream.WriteProperty(bankName);
                    }
                }
            }
        }

        /// <summary>
        /// Rename a list of SoundNodeWaves.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="nodeWaves">SoundNodeWaves to rename.</param>
        /// <param name="oldName">Old name to replace.</param>
        /// <param name="newName">New name to replace with.</param>
        private static void RenameSoundNodeWaves(IMEPackage pcc, List<ExportEntry> nodeWaves, string oldName, string newName)
        {
            foreach (ExportEntry nodeWave in nodeWaves)
            {
                string name = nodeWave.ObjectName.Name.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);
                nodeWave.ObjectName = new NameReference(name, nodeWave.ObjectName.Number);
            }
        }

        /// <summary>
        /// Update the IDs of a list of ExportEntries with hashes of their names.
        /// </summary>
        /// <param name="entries">WwiseStreams to update.</param>
        /// <param name="random">If not null, the random object to generate ids, instead of the name.</param>
        /// <returns>KVP of old and new IDs. Used to update references.</returns>
        private static Dictionary<uint, uint> UpdateIDs_EXPERIMENTAL(List<ExportEntry> entries, Random random = null)
        {
            Dictionary<uint, uint> oldAndNewIDs = new();

            foreach (ExportEntry entry in entries)
            {
                (uint oldID, uint newID) = UpdateID_EXPERIMENTAL(entry, random);

                if (newID == 0) { continue; }

                oldAndNewIDs.Add(oldID, newID);
            }

            return oldAndNewIDs;
        }

        /// <summary>
        /// Update the ID of an ExportEntry with a hash of the provided name.
        /// </summary>
        /// <param name="entry">Entry to update.</param>
        /// <param name="random">Random object used for generating random IDs, otherwise use the object's name.</param>
        /// <param name="name">Name to use for the name generation, if not random. If not random and empty, use the object's name.</param>
        /// <returns>KVP of old and new ID. Used to update references.</returns>
        private static (uint, uint) UpdateID_EXPERIMENTAL(ExportEntry entry, Random random = null, string name = "")
        {
            IntProperty IDProp = entry.GetProperty<IntProperty>("Id");
            if (IDProp == null) { return (0, 0); }

            // Get/Generate IDs and little endian hashes
            uint oldID = unchecked((uint)IDProp.Value);
            uint newID = random != null
                ? GenerateRandomID(random)
                : CalculateFNV132Hash(string.IsNullOrEmpty(name) ? entry.ObjectName.Name : name);

            // Update the ID property
            IDProp.Value = unchecked((int)newID);
            entry.WriteProperty(IDProp);

            return (oldID, newID);
        }

        /// <summary>
        /// Update the IDs of a list of ExportEntries with hashes of their names.
        /// </summary>
        /// <param name="entries">WwiseStreams to update.</param>
        /// <returns>KVP of old and new IDs. Used to update references.</returns>
        private static Dictionary<uint, uint> UpdateIDs_LEGACY(List<ExportEntry> entries)
        {
            Dictionary<uint, uint> oldAndNewIDs = new();
            foreach (ExportEntry entry in entries)
            {
                (uint oldID, uint newID) = UpdateID_LEGACY(entry);

                if (newID == 0) { continue; }
                oldAndNewIDs.Add(oldID, newID);
            }

            return oldAndNewIDs;
        }

        /// <summary>
        /// Update the ID of an ExportEntry with a hash of its name.
        /// </summary>
        /// <param name="entry">Entry to update.</param>
        /// <returns>KVP of old and new ID. Used to update references.</returns>
        private static (uint, uint) UpdateID_LEGACY(ExportEntry entry)
        {
            string name = entry.ObjectName.Name;
            return UpdateID_LEGACY(entry, name);
        }

        /// <summary>
        /// Update the ID of an ExportEntry with a hash of the provided name.
        /// </summary>
        /// <param name="entry">Entry to update.</param>
        /// <param name="name">Name to use for hash.</param>
        /// <returns>KVP of old and new ID. Used to update references.</returns>
        private static (uint, uint) UpdateID_LEGACY(ExportEntry entry, string name)
        {
            IntProperty IDProp = entry.GetProperty<IntProperty>("Id");

            if (IDProp == null) { return (0, 0); }

            // Get/Generate IDs and little endian hashes
            uint oldID = unchecked((uint)IDProp.Value);
            uint newID = CalculateFNV132Hash(name);

            // Update the ID property
            IDProp.Value = unchecked((int)newID);
            entry.WriteProperty(IDProp);

            return (oldID, newID);
        }

        /// <summary>
        /// Rename a bioConversation's FXAs.
        /// Not useful as a standalone experiment, since other semi-unrelated elements need to be renamed too.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">Conversation the elements belong to.</param>
        /// <param name="oldName">Old name to replace in the elements.</param>
        /// <param name="newName">New name to replace in the elements.</param>
        private static void RenameFXAs(IMEPackage pcc, ExportEntry bioConversation, string oldName, string newName)
        {
            if (pcc == null || (pcc.Game is MEGame.ME1)) { return; }

            List<ExportEntry> fxas = GetFXAs(pcc, bioConversation);
            RenameFXAs(pcc, bioConversation, fxas, oldName, newName);
        }

        /// <summary>
        /// Rename a bioConversation's FXAs.
        /// Not useful as a standalone experiment, since other semi-unrelated elements need to be renamed too.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">Conversation the elements belong to.</param>
        /// <param name="fxas">List of FXAs to rename.</param>
        /// <param name="oldName">Old name to replace in the elements.</param>
        /// <param name="newName">New name to replace in the elements.</param>
        private static void RenameFXAs(IMEPackage pcc, ExportEntry bioConversation, List<ExportEntry> fxas, string oldName, string newName)
        {
            if (pcc == null || (pcc.Game is MEGame.ME1)) { return; }

            foreach (ExportEntry fxaExport in fxas)
            {
                string oldFxaFullName = fxaExport.ObjectName; // May contain _M, _F, or _NonSpkr
                string oldFxaName = oldFxaFullName; // Full name minus _M/_F, or including _NonSpkr

                if (oldFxaFullName[^2..].ToLower() is "_m" or "_f")
                {
                    oldFxaName = oldFxaFullName.Remove(oldFxaFullName.Length - 2);
                }
                else
                {
                    // Most likely a NonSpkr, in which case we'll use the full name
                    if (oldFxaFullName.EndsWith("_nonspkr", StringComparison.OrdinalIgnoreCase))
                    {
                        oldFxaName = oldFxaFullName;
                    }
                }

                string newFxaName = oldFxaName.Replace(oldName, newName);
                fxaExport.ObjectName = oldFxaFullName.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase);

                FaceFXAnimSet faceFXAnimSet = fxaExport.GetBinaryData<FaceFXAnimSet>();
                // Replace the old name in the name chunk
                List<string> names = faceFXAnimSet.Names.Select(name => name == oldFxaName ? newFxaName : name).ToList();
                faceFXAnimSet.Names = names;

                ArrayProperty<ObjectProperty> eventRefs = fxaExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues");
                // Set the paths with the new names and update the names of WwiseStreams
                if (eventRefs != null)
                {
                    foreach (FaceFXLine line in faceFXAnimSet.Lines)
                    {
                        // Update the ID for ME1
                        if (pcc.Game.IsGame1())
                        {
                            line.ID = line.ID.Replace(oldName, newName);
                        }

                        ExportEntry soundEvent;
                        if (pcc.Game is MEGame.ME2)
                        {
                            if (string.IsNullOrEmpty(line.Path)) { continue; }
                            soundEvent = pcc.FindExport(line.Path.Replace(oldName, newName, StringComparison.OrdinalIgnoreCase));
                        }
                        else
                        {
                            if (line.Index < 0 || eventRefs[line.Index].Value <= 0) { continue; }
                            soundEvent = pcc.GetUExport(eventRefs[line.Index].Value);
                        }

                        // We can't really do anything else if the sound event is null
                        if (soundEvent == null) { continue; }

                        line.Path = soundEvent.FullPath;
                    }
                }

                fxaExport.WriteBinary(faceFXAnimSet);
            }
        }

        /// <summary>
        /// Get a list containing all FXAs in a BioConversation.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">Conversation to get FXAs from.</param>
        /// <returns>List containing found FXAs.</returns>
        private static List<ExportEntry> GetFXAs(IMEPackage pcc, ExportEntry bioConversation)
        {
            List<ExportEntry> fxas = new();
            List<ObjectProperty> fxaRefs = new();

            ArrayProperty<ObjectProperty> maleFXAs = bioConversation.GetProperty<ArrayProperty<ObjectProperty>>("m_aMaleFaceSets");
            if (maleFXAs != null) { fxaRefs.AddRange(maleFXAs); }
            ArrayProperty<ObjectProperty> femaleFXAs = bioConversation.GetProperty<ArrayProperty<ObjectProperty>>("m_aFemaleFaceSets");
            if (femaleFXAs != null) { fxaRefs.AddRange(femaleFXAs); }
            ObjectProperty nonSpkrFxa = bioConversation.GetProperty<ObjectProperty>("m_pNonSpeakerFaceFXSet");
            if (nonSpkrFxa != null) { fxaRefs.Add(nonSpkrFxa); }

            foreach (ObjectProperty fxa in fxaRefs)
            {
                if (fxa.Value == 0) { continue; }

                ExportEntry fxaExport = pcc.GetUExport(fxa.Value);
                if (fxaExport == null) { continue; }

                fxas.Add(fxaExport);
            }

            return fxas;
        }

        /// <summary>
        /// Get a list of SoundCues, SoundNodeWaves, WwiseStreams, or WwiseEvents related to the given BioConversation.
        /// Assumes that found IEntries are ExportEntries.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">BioConversation the exports are referenced by.</param>
        /// <param name="targetClass">Class of exports to get.</param>
        /// <param name="conversation">Loaded Conversation, to avoid copy/pasting some its code.</param>
        /// <returns>List of ExportEntries of the given class.</returns>
        private static List<ExportEntry> GetAudioExports(IMEPackage pcc, ExportEntry bioConversation, AudioClass targetClass, ConversationExtended conversation = null)
        {
            if (pcc == null || bioConversation.ClassName != "BioConversation") { return null; }

            if (conversation == null)
            {
                conversation = new(bioConversation);
                conversation.LoadConversation(TLKManagerWPF.GlobalFindStrRefbyID, true);
            }

            ExportEntry package;

            // Try to get the parent package that contains the audio elements, which varies between games.
            if (pcc.Game.IsGame1())
            {
                if (!pcc.TryGetUExport(conversation.Sequence.idxLink, out package)) { return null; }
            }
            else if (pcc.Game.IsGame2())
            {
                if (!pcc.TryGetUExport(conversation.WwiseBank.idxLink, out package)) { return null; }
            }
            else
            {
                if (!pcc.TryGetUExport(bioConversation.idxLink, out package)) { return null; }
            }

            if (package != null && package.ClassName != "Package") { return new(); }

            // Avoid trying to get exports that belong to a different game
            if (pcc.Game.IsGame1())
            {
                if (targetClass is not (AudioClass.SoundCue or AudioClass.SoundNodeWave)) { return null; }
            }
            else
            {
                if (targetClass is (AudioClass.SoundCue or AudioClass.SoundNodeWave)) { return null; }
            }

            switch (targetClass)
            {
                case AudioClass.SoundCue:
                    return package.GetAllDescendants().Where(e => e.ClassName == "SoundCue")
                        .Select(e => (ExportEntry)e).ToList();
                case AudioClass.SoundNodeWave:
                    return package.GetAllDescendants().Where(e => e.ClassName == "SoundNodeWave")
                        .Select(e => (ExportEntry)e).ToList();
                case AudioClass.WwiseStream:
                    return package.GetAllDescendants().Where(e => e.ClassName == "WwiseStream")
                        .Select(e => (ExportEntry)e).ToList();
                case AudioClass.WwiseEvent:
                    return package.GetAllDescendants().Where(e => e.ClassName == "WwiseEvent")
                        .Select(e => (ExportEntry)e).ToList();
            }

            return null;
        }

        /// <summary>
        /// Check that the bioConversation follows the games' normal conversation structure.
        /// It does not display error messages to the user, as that's handled by the experiment callers.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="bioConversation">BioConversation to check.</param>
        /// <param name="conversation">Loaded Conversation, to avoid copy/pasting some its code.</param>
        /// <returns>Error message resulting of the check, if one is found; empty string otherwise.</returns>
        private static string CheckConversationStructure(IMEPackage pcc, ExportEntry bioConversation, ConversationExtended conversation = null)
        {
            if (bioConversation.ClassName != "BioConversation")
            {
                return "Selected export is not a BioConversation";
            }

            if (conversation == null)
            {
                conversation = new(bioConversation);
                conversation.LoadConversation(TLKManagerWPF.GlobalFindStrRefbyID, true);
            }

            if (bioConversation.idxLink == 0 || !pcc.TryGetUExport(bioConversation.idxLink, out ExportEntry dLink) || dLink.ClassName != "Package")
            {
                return "BioConversation not children of a Package export. Make sure to keep the normal structure of the game's conversations.";
            }

            // ME1 and ME2 have the audio elements in a separate package.
            if (pcc.Game.IsGame1() && (!pcc.TryGetUExport(conversation.Sequence.idxLink, out ExportEntry sLink) || sLink.ClassName != "Package"))
            {
                return "BioConversation's audio package not children of a Package export. Make sure to keep the normal structure of ME1's conversations.";
            }
            else if (pcc.Game.IsGame2() && (!pcc.TryGetUExport(conversation.WwiseBank.idxLink, out ExportEntry wLink) || wLink.ClassName != "Package"))
            {
                return "BioConversation's audio package not children of a Package export. Make sure to keep the normal structure of ME2's conversations.";
            }

            return "";
        }

        /// <summary>
        /// Wrapper for UpdateAmbPerfClass so it can used as a full experiment on its own.
        /// </summary>
        /// <param name="pew">Current PE window.</param>
        public static void UpdateAmbPerfClassExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.SelectedItem.Entry.ClassName is not "SFXAmbPerfGameData")
            {
                ShowError("Selected export is not an SFXAmbPerfGameData");
                return;
            }

            int propResourceID = PromptForInt("PropResource export number:", "Not a valid export number. It must be positive integer", -1, "PropResouce export number");
            if (propResourceID == -1) { return; }
            if (!pew.Pcc.TryGetUExport(propResourceID, out ExportEntry propResource))
            {
                ShowError("Could not find the export number.");
                return;
            }
            if (propResource.ClassName != "Class")
            {
                ShowError("Provided export is not a class.");
                return;
            }

            UpdateAmbPerfClass(pew.Pcc, (ExportEntry)pew.SelectedItem.Entry, propResource);

            MessageBox.Show("Properties of SFXAmbPerfGameData updated successfully.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Batch update WepPropClass, PropName, and PropResource props of an AmbPerfs in a selected Package.
        /// </summary>
        /// <param name="pew">Current PE window.</param>
        public static void BatchUpdateAmbPerfClassExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.SelectedItem.Entry.ClassName is not "Package")
            {
                ShowError("Selected export is not an Package");
                return;
            }

            int propResourceID = PromptForInt("PropResource export number:", "Not a valid export number. It must be positive integer", -1, "PropResouce export number");
            if (propResourceID == -1) { return; }
            if (!pew.Pcc.TryGetUExport(propResourceID, out ExportEntry propResource))
            {
                ShowError("Could not find the export number.");
                return;
            }
            if (propResource.ClassName != "Class")
            {
                ShowError("Provided export is not a class.");
                return;
            }

            string propClass = propResource.InstancedFullPath;
            string propName = propResource.ObjectName.Name.Replace("SFXWeapon_", "", StringComparison.OrdinalIgnoreCase);

            pew.SelectedItem.Entry.GetAllDescendants().ForEach(entry =>
            {
                if (entry.ClassName == "SFXAmbPerfGameData")
                {
                    UpdateAmbPerfClass(pew.Pcc, (ExportEntry)entry, propResource, propClass, propName);
                }
            });

            MessageBox.Show("Properties of children SFXAmbPerfGameDatas updated successfully.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Update WepPropClass, PropName, and PropResource props of an AmbPerf.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="ambPerfGameData">Export to update.</param>
        /// <param name="propResource">Weapon class export entry, from which all the needed information is derived.</param>
        /// <param name="propClass">Prop class instantiated named. Passed when used in a batch.</param>
        /// <param name="propName">Prop class name minus the SFXWeapon_ part. Passed when used in a batch.</param>
        public static void UpdateAmbPerfClass(IMEPackage pcc, ExportEntry ambPerfGameData, ExportEntry propResource, string propClass = "", string propName = "")
        {
            if (string.IsNullOrEmpty(propClass))
            {
                propClass = propResource.InstancedFullPath;
            }

            if (string.IsNullOrEmpty(propName))
            {
                propName = propResource.ObjectName.Name.Replace("SFXWeapon_", "", StringComparison.OrdinalIgnoreCase);
            }

            PropertyCollection props = ambPerfGameData.GetProperties();

            props.AddOrReplaceProp(new StrProperty(propClass, "m_sWepPropClass"));
            props.AddOrReplaceProp(new NameProperty(propName, "m_nmPropName"));
            props.AddOrReplaceProp(new ObjectProperty(propResource, "m_pPropResource"));

            ambPerfGameData.WriteProperties(props);
        }

        /// <summary>
        /// Replaces the colors in the DirectionalSamples of the 1D Lightmap of a StaticMeshComponent
        /// </summary>
        /// <param name="pew">Current PE window.</param>
        public static void Replace1DLightMapColors(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            IMEPackage pcc = pew.Pcc;
            if (pew.SelectedItem.Entry.ClassName is not "StaticMeshComponent")
            {
                ShowError("Selected export is not an StaticMeshComponent");
                return;
            }

            ExportEntry component = pew.SelectedItem.Entry as ExportEntry;

            if (ObjectBinary.From(component) is not StaticMeshComponent sc)
            {
                ShowError("Wrong binary data.");
                return;
            }

            Dictionary<int, string> colorMap = GetColorMap(sc.LODData);

            string colors = string.Join("; ", colorMap.Select(pair => $"{pair.Key}: {pair.Value}"));

            MessageBoxResult response = MessageBox.Show($"Found {colorMap.Count} colors.\n\nDo you want to copy them to the clipboard?\nThey'll appear in the form of <color id>: <hexadecimal>.", "", MessageBoxButton.YesNo);
            if (response == MessageBoxResult.Yes) { Clipboard.SetText(colors); }

            string newColorsString = PromptDialog.Prompt(null,
                "Paste a ; separated list of color IDs and the new hexadecimal color values to patch, in the following form:\n" +
                "<color id 1>: <hexadecimal 1>; <color id 2>: <hexadecimal 2>; ...\n\n");

            if (string.IsNullOrEmpty(newColorsString))
            {
                ShowError("Invalid colors list.");
                return;
            }

            Dictionary<int, (int, int, int)> newColorMap = new();

            foreach (string s in newColorsString.Split(";"))
            {
                if (!s.Contains(':'))
                {
                    ShowError("Wrong formatting for color ids and hex.");
                    return;
                }

                // Validate values
                string[] temp = s.Trim().Split(":");
                if (!int.TryParse(temp[0], out int id))
                {
                    ShowError($"The {s}'s id is not an int.");
                    return;
                }

                newColorMap.Add(id, HexToRGB(temp[1].Trim()));
            }

            ApplyColorMap(newColorMap, sc.LODData);

            component.WriteBinary(sc);

            MessageBox.Show("The colors were sucessfully replaced", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Replaces the colors in the DirectionalSamples of the 1D Lightmap of the StaticMeshComponent of the given export IDs.
        /// /// </summary>
        /// <param name="pew">Current PE window</param>
        public static void Replace1DLightMapColorsOfExports(PackageEditorWindow pew)
        {
            if (pew.Pcc == null) { return; }

            IMEPackage pcc = pew.Pcc;

            // Get IDs of exports to edit
            string exportsString = PromptDialog.Prompt(null, "Write a comma separated list of export IDs of the StaticMeshComponents to edit");
            if (string.IsNullOrEmpty(exportsString))
            {
                ShowError("Invalid list");
                return;
            }

            List<ExportEntry> components = new();

            foreach (string exportStr in exportsString.Split(','))
            {
                if (!int.TryParse(exportStr, out int id))
                {
                    ShowError($"{exportStr} is not a valid ID");
                    return;
                }

                if (!pcc.TryGetUExport(id, out ExportEntry export))
                {
                    ShowError($"{exportStr} is not a valid export");
                    return;
                }

                if (export.ClassName != "StaticMeshComponent")
                {
                    ShowError($"{exportStr} is not a StaticMeshComponent");
                    return;
                }

                components.Add(export);
            }

            string colors = "";
            int mapCount = 0;

            foreach (ExportEntry component in components)
            {
                if (ObjectBinary.From(component) is not StaticMeshComponent sc) { continue; }

                Dictionary<int, string> colorMap = GetColorMap(sc.LODData);
                if (colorMap.Count == 0) { continue; } // Takes care of components with no 1D LightMaps

                colors += $"IDX{component.UIndex} - {string.Join("; ", colorMap.Select(pair => $"{pair.Key}: {pair.Value}"))}@\n";

                mapCount++;
            }

            if (string.IsNullOrEmpty(colors))
            {
                ShowError("No 1D LightMaps were found in the file.");
                return;
            }

            MessageBoxResult response = MessageBox.Show($"Found {mapCount} 1D LightMaps.\n\nDo you want to copy the colors to the clipboard?\n\nThey'll appear in the following form:\nIDX<lightMap index> - <color id>: <hexadecimal>; ...", "", MessageBoxButton.YesNo);
            if (response == MessageBoxResult.Yes) { Clipboard.SetText(colors); }

            string newColorsString = PromptDialog.Prompt(null,
                "Paste a list of light map IDs and a ; separated list of their color IDs and the new hexadecimal color values to patch, in the following form:\n" +
                "IDX<map export index 1> - <color id 1>: <hexadecimal 1>; <color id 2>: <hexadecimal 2>; ...\n" +
                "IDX<map export index n> - <color id n>: <hexadecimal n>; <color id n>: <hexadecimal n>; ...\n\n");

            if (string.IsNullOrEmpty(newColorsString))
            {
                ShowError("Invalid colors list.");
                return;
            }

            foreach (string line in newColorsString.Split('@'))
            {
                if (string.IsNullOrEmpty(line)) { continue; }

                string[] parts = line.Split("-");
                if (!int.TryParse(parts[0].Trim()[3..], out int idx)) { continue; }
                string newColors = parts[1].Trim();

                if (!pcc.TryGetUExport(idx, out ExportEntry component)) { continue; }
                if (ObjectBinary.From(component) is not StaticMeshComponent sc) { continue; }

                Dictionary<int, (int, int, int)> newColorMap = new();

                foreach (string s in newColors.Split(";"))
                {
                    if (!s.Contains(':')) { continue; }

                    // Validate values
                    string[] temp = s.Trim().Split(":");
                    if (!int.TryParse(temp[0], out int id)) { continue; }

                    newColorMap.Add(id, HexToRGB(temp[1].Trim()));
                }

                ApplyColorMap(newColorMap, sc.LODData);

                component.WriteBinary(sc);
            }

            MessageBox.Show("The colors were sucessfully replaced", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Replaces the colors in the DirectionalSamples of the 1D Lightmap of all StaticMeshComponent
        /// </summary>
        /// <param name="pew">Current PE window</param>
        public static void BatchReplace1DLightMapColors(PackageEditorWindow pew)
        {
            if (pew.Pcc == null) { return; }

            IMEPackage pcc = pew.Pcc;

            List<ExportEntry> components = pcc.Exports.Where(exp => exp.ClassName == "StaticMeshComponent").ToList();

            if (components.Count == 0)
            {
                ShowError("No StaticMeshComponents found in the file.");
                return;
            }

            string colors = "";
            int mapCount = 0;

            foreach (ExportEntry component in components)
            {
                if (ObjectBinary.From(component) is not StaticMeshComponent sc) { continue; }

                Dictionary<int, string> colorMap = GetColorMap(sc.LODData);
                if (colorMap.Count == 0) { continue; } // Takes care of components with no 1D LightMaps

                colors += $"IDX{component.UIndex} - {string.Join("; ", colorMap.Select(pair => $"{pair.Key}: {pair.Value}"))}@\n";

                mapCount++;
            }

            if (string.IsNullOrEmpty(colors))
            {
                ShowError("No 1D LightMaps were found in the file.");
                return;
            }

            MessageBoxResult response = MessageBox.Show($"Found {mapCount} 1D LightMaps.\n\nDo you want to copy the colors to the clipboard?\n\nThey'll appear in the following form:\nIDX<lightMap index> - <color id>: <hexadecimal>; ...", "", MessageBoxButton.YesNo);
            if (response == MessageBoxResult.Yes) { Clipboard.SetText(colors); }

            string newColorsString = PromptDialog.Prompt(null,
                "Paste a list of light map IDs and a ; separated list of their color IDs and the new hexadecimal color values to patch, in the following form:\n" +
                "IDX<map export index 1> - <color id 1>: <hexadecimal 1>; <color id 2>: <hexadecimal 2>; ...\n" +
                "IDX<map export index n> - <color id n>: <hexadecimal n>; <color id n>: <hexadecimal n>; ...\n\n");

            if (string.IsNullOrEmpty(newColorsString))
            {
                ShowError("Invalid colors list.");
                return;
            }

            foreach (string line in newColorsString.Split('@'))
            {
                if (string.IsNullOrEmpty(line)) { continue; }

                string[] parts = line.Split("-");
                if (!int.TryParse(parts[0].Trim()[3..], out int idx)) { continue; }
                string newColors = parts[1].Trim();

                if (!pcc.TryGetUExport(idx, out ExportEntry component)) { continue; }
                if (ObjectBinary.From(component) is not StaticMeshComponent sc) { continue; }

                Dictionary<int, (int, int, int)> newColorMap = new();

                foreach (string s in newColors.Split(";"))
                {
                    if (!s.Contains(':')) { continue; }

                    // Validate values
                    string[] temp = s.Trim().Split(":");
                    if (!int.TryParse(temp[0], out int id)) { continue; }

                    newColorMap.Add(id, HexToRGB(temp[1].Trim()));
                }

                ApplyColorMap(newColorMap, sc.LODData);

                component.WriteBinary(sc);
            }

            MessageBox.Show("The colors were sucessfully replaced", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Adds the ForcedExport flag to all descendant exports of the selected export, including itself
        /// </summary>
        /// <param name="pew">Current PE window</param>
        public static void MakeExportsForced(PackageEditorWindow pew)
        {
            if (pew.Pcc == null) { return; }

            IMEPackage pcc = pew.Pcc;

            if (pew.SelectedItem == null) { return; }

            if (pew.SelectedItem.Entry is not ExportEntry)
            {
                ShowError("The selected entry is not an export");
                return;
            }

            List<IEntry> entries = pew.SelectedItem.Entry.GetAllDescendants();
            entries.Add(pew.SelectedItem.Entry);

            foreach (IEntry entry in entries)
            {
                if (entry is not ExportEntry export) { continue; }

                if (export.ClassName == "ObjectReferencer") { continue; }

                if ((export.ExportFlags & UnrealFlags.EExportFlags.ForcedExport) == 0)
                {
                    export.ExportFlags |= UnrealFlags.EExportFlags.ForcedExport;
                }
            }

            MessageBox.Show("Set the ForcedExport flag in all exports", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Gathers all the colors in the DirectionalSamples of a the 1D LightMap in the LODData.
        /// </summary>
        /// <param name="LODData">LODs to get the samples from.</param>
        /// <returns>A dictionary of packged RGBA integer keys and its corresponding hex color.</returns>
        private static Dictionary<int, string> GetColorMap(StaticMeshComponentLODInfo[] LODData)
        {
            Dictionary<int, string> colorMap = new();

            foreach (StaticMeshComponentLODInfo lod in LODData)
            {
                if (lod.LightMap.LightMapType == ELightMapType.LMT_1D && lod.LightMap is LightMap_1D lm1)
                {
                    foreach (QuantizedDirectionalLightSample qds in lm1.DirectionalSamples)
                    {
                        List<string> sample = new();
                        colorMap[qds.Coefficient1.ToRgba()] = RGBToHex(qds.Coefficient1);
                        colorMap[qds.Coefficient2.ToRgba()] = RGBToHex(qds.Coefficient2);
                        colorMap[qds.Coefficient3.ToRgba()] = RGBToHex(qds.Coefficient3);
                    }
                }
            }

            return colorMap;
        }

        /// <summary>
        /// Applies a color map of packed rgba int keys and (r, g, b) colors to the directional samples in the 1D maps.
        /// </summary>
        /// <param name="newColorMap">New colors to apply.</param>
        /// <param name="LODData">LOD to apply the colors to.</param>
        private static void ApplyColorMap(Dictionary<int, (int, int, int)> newColorMap, StaticMeshComponentLODInfo[] LODData)
        {
            foreach (StaticMeshComponentLODInfo lod in LODData)
            {
                if (lod.LightMap.LightMapType == ELightMapType.LMT_1D && lod.LightMap is LightMap_1D lm1)
                {
                    foreach (QuantizedDirectionalLightSample qds in lm1.DirectionalSamples)
                    {
                        List<string> sample = new();
                        qds.Coefficient1 = EditColor(qds.Coefficient1, newColorMap);
                        qds.Coefficient2 = EditColor(qds.Coefficient2, newColorMap);
                        qds.Coefficient3 = EditColor(qds.Coefficient3, newColorMap);
                    }
                }
            }
        }

        /// <summary>
        /// Converts the RGB values in a Color to hexadecimal.
        /// </summary>
        /// <param name="coefficient">Color to edit.</param>
        /// <returns>The hexadecimal string.</returns>
        private static string RGBToHex(Color coefficient)
        {
            byte red = Convert.ToByte(coefficient.R);
            byte green = Convert.ToByte(coefficient.G);
            byte blue = Convert.ToByte(coefficient.B);

            return $"#{red:X2}{green:X2}{blue:X2}";
        }

        /// <summary>
        /// Converts a hexadecimal color into R,G,B values.
        /// </summary>
        /// <param name="hexColor">Hexadecimal color.</param>
        /// <returns>R,G,B tuple.</returns>
        private static (int, int, int) HexToRGB(string hexColor)
        {
            if (hexColor.Length == 7 && hexColor[0] == '#')
            {
                byte red = Convert.ToByte(hexColor.Substring(1, 2), 16);
                byte green = Convert.ToByte(hexColor.Substring(3, 2), 16);
                byte blue = Convert.ToByte(hexColor.Substring(5, 2), 16);


                return (red, green, blue);
            }
            else
            {
                return (0, 0, 0);
            }
        }

        /// <summary>
        /// Edit the R,G,B values of a Color with the ones provided in the colorMap.
        /// </summary>
        /// <param name="color">Color to edit.</param>
        /// <param name="colorMap">Map containing the new color.</param>
        /// <returns>Edited color.</returns>
        private static Color EditColor(Color color, Dictionary<int, (int, int, int)> colorMap)
        {
            if (colorMap.TryGetValue(color.ToRgba(), out (int, int, int) rgb))
            {
                color.R = (byte)rgb.Item1;
                color.G = (byte)rgb.Item2;
                color.B = (byte)rgb.Item3;
            }
            return color;
        }

        /// <summary>
        /// Collect all StaticMeshComponents, referenced by StatichMeshActors, and add them into a new StaticMeshCollectionActor
        /// </summary>
        /// <param name="pew">Current PE window</param>
        public static void CollectSMCsintoSMCA(PackageEditorWindow pew)
        {
            if (pew.Pcc == null) { return; }

            IMEPackage pcc = pew.Pcc;

            bool trashActors = MessageBoxResult.Yes == MessageBox.Show(
                "Trash actors that referenced the StaticMeshComponents?\n" +
                "References to the actors in the PersistentLevel will be removed, but other references may remain.",
                "Trash actors", MessageBoxButton.YesNo);

            List<ExportEntry> actors = pcc.Exports.Where(exp => exp.ClassName == "StaticMeshActor").ToList();

            if (actors.Count == 0)
            {
                ShowError("No StaticMeshActors found in the file.");
                return;
            }

            Dictionary<ExportEntry, (Vector3, Rotator, Vector3)> SMCsAndPos = [];
            List<IEntry> actorsToTrash = [];

            // Collect components referenced by actors and the location, rotation, and draw scale values
            foreach (ExportEntry actor in actors)
            {
                PropertyCollection actorProps = actor.GetProperties();

                ObjectProperty smcRef = actorProps.GetProp<ObjectProperty>("StaticMeshComponent");

                if (smcRef == null || !pcc.TryGetUExport(smcRef.Value, out ExportEntry smc))
                {
                    continue;
                }

                Vector3 loc = GetTransformationData(actor, "Location");
                Vector3 rot = GetTransformationData(actor, "Rotation");
                Vector3 draw = GetTransformationData(actor, "DrawScale3D");

                SMCsAndPos.Add(smc, (loc, new Rotator((int)rot.X, (int)rot.Y, (int)rot.Z), draw));

                actor.RemoveProperty("StaticMeshComponent");
                actorsToTrash.Add(actor);
            }

            // Prepare the props for the new StaticMeshCollectionActor
            PropertyCollection collectionProps = [
                new ArrayProperty<ObjectProperty>(
                    SMCsAndPos.Select(kvp => new ObjectProperty(kvp.Key)), "StaticMeshComponents"),
                new NameProperty("StaticMeshCollectionActor", "Tag")
            ];

            ExportEntry persistentLevel = pcc.FindExport("TheWorld.PersistentLevel");

            // Create the actor
            ExportEntry smca = CreateExport(pcc, pcc.GetNextIndexedName("StaticMeshCollectionActor"), "StaticMeshCollectionActor",
                persistentLevel, collectionProps);

            // Prepare the binary for the new StaticMeshCollectionActor
            StaticMeshCollectionActor collectionBinary = new()
            {
                Export = smca,
                Components = SMCsAndPos.Select(kvp => kvp.Key.UIndex).ToList(),
                LocalToWorldTransforms = SMCsAndPos.Select(kvp =>
                {
                    (Vector3 loc, Rotator rot, Vector3 draw) = kvp.Value;
                    return ActorUtils.ComposeLocalToWorld(loc, rot, draw);
                }).ToList()
            };
            smca.WriteBinary(collectionBinary);

            // Add the new collection actor reference to the PersistentLevel
            Level plBinary = ObjectBinary.From<Level>(persistentLevel);
            plBinary.Actors.Add(smca.UIndex);
            if (trashActors)
            {
                foreach (IEntry actor in actorsToTrash)
                {
                    plBinary.Actors.TryRemove((uidx => uidx == actor.UIndex), out _);
                }
            }
            persistentLevel.WriteBinary(plBinary);

            // Relink the components to the new collection actor
            foreach (KeyValuePair<ExportEntry, (Vector3, Rotator, Vector3)> kvp in SMCsAndPos)
            {
                kvp.Key.idxLink = smca.UIndex;
            }

            if (trashActors)
            {
                EntryPruner.TrashEntries(pcc, actorsToTrash);
            }

            MessageBox.Show($"Created {smca.InstancedFullPath} with {SMCsAndPos.Count} StaticMeshComponents", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Get the transformation data that matches the given propName.
        /// </summary>
        /// <param name="actor">Actor to find the data on.</param>
        /// <param name="propName">Name of the prop to get the data of.</param>
        /// <returns>Vector3 containing the three transformation values of the prop.</returns>
        private static Vector3 GetTransformationData(ExportEntry actor, string propName)
        {
            StructProperty prop = actor.GetProperty<StructProperty>(propName);

            // Try to get the data from the archetype.
            // Useful in case of prefabs.
            if (prop == null)
            {
                IEntry archetype = actor.Archetype;

                if (archetype == null) { return new Vector3(); } // Exit if no prop in actor or archeytpe

                if (archetype is ImportEntry entry)
                {
                    archetype = EntryImporter.ResolveImport(entry);
                }

                prop = ((ExportEntry)archetype).GetProperty<StructProperty>(propName);
            }

            switch (propName)
            {
                case "Location":
                case "DrawScale3D":
                    return prop != null
                        ? new Vector3(prop.GetProp<FloatProperty>("X"), prop.GetProp<FloatProperty>("Y"), prop.GetProp<FloatProperty>("Z"))
                        : new Vector3(0, 0, 0);
                case "Rotation":
                    return prop != null
                        ? new Vector3(prop.GetProp<FloatProperty>("Pitch"), prop.GetProp<FloatProperty>("Yaw"), prop.GetProp<FloatProperty>("Roll"))
                        : new Vector3(0, 0, 0);
                default:
                    return new Vector3(0, 0, 0);
            }
        }

        /// <summary>
        /// Creates instances of all the prefab archetypes of a Prefab into the file's persistent level.
        /// </summary>
        /// <param name="pew">Current PE window.</param>
        public static void AddPrefabToLevel(PackageEditorWindow pew)
        {
            if (pew.Pcc == null || pew.SelectedItem?.Entry == null) { return; }

            if (pew.SelectedItem.Entry.ClassName is not "Prefab" || !(pew.SelectedItem.Entry is ExportEntry prefab))
            {
                ShowError("Selected export is not Prefab");
                return;
            }

            ArrayProperty<ObjectProperty> prefabArchetypes = prefab.GetProperty<ArrayProperty<ObjectProperty>>("PrefabArchetypes");
            if (prefabArchetypes == null)
            {
                ShowError("The Prefab contains no PrefabArchetypes property");
                return;
            }

            if (!(pew.Pcc.FindExport("TheWorld.PersistentLevel") is ExportEntry { ClassName: "Level" } levelExport))
            {
                ShowError("No PersistentLevel found in the file.");
                return;
            }

            Level level = ObjectBinary.From<Level>(levelExport);

            foreach (ObjectProperty archRef in prefabArchetypes)
            {
                // Either cast the archetype or resolve it, so we clone an ExportEntry
                IEntry entry = pew.Pcc.GetEntry(archRef.Value);
                ExportEntry archetype = ResolveEntryToExport(entry);

                ExportEntry newExport;

                // General creation
                switch (archetype.ClassName)
                {
                    case "BioDoor":
                        continue; // Skip BioDoors, as adding the causes crashes
                    case "PointLight":
                    case "SpotLight":
                    case "StaticMeshActor":
                        newExport = CreateExport(pew.Pcc, archetype.ObjectName, archetype.ClassName, levelExport, archetype.GetProperties(),
                            null, null, null, archetype); // Create the export
                        AddStack(newExport); // Add preProps binary
                        break;
                    default:
                        newExport = ClonePrefabArchetype(entry, archetype, levelExport);
                        break;
                }

                // Specific operations on the different classes. We could have a single switch case, but given that they need the export to already
                // be created, and the specificities, it's cleaner to have two separate steps.
                switch (archetype.ClassName)
                {
                    case "PointLight":
                    case "SpotLight":
                        AddLightingChannelsToComponent(pew.Pcc, archetype);
                        break;
                    case "StaticMeshActor":
                        AddComponentsToActor(pew.Pcc, archetype, newExport); // Copy relevant components
                        break;
                    case "BioSunActor":
                        FixPackageOfSunSprites(pew.Pcc, archetype.GetProperties());
                        break;
                    default:
                        break;
                }

                level.Actors.Add(newExport.UIndex);
            }

            levelExport.WriteBinary(level);

            MessageBox.Show("Added instances of all the archetypes in the Prefab to the PersistentLevel.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Add the StaticMeshComponent and CollisionComponent of the source into the target, adding their proper binary in the process.
        /// </summary>
        /// <param name="pcc">Package to operate on.</param>
        /// <param name="source">Source export to clone the components from.</param>
        /// <param name="target">Target export to clone the components into.</param>
        private static void AddComponentsToActor(IMEPackage pcc, ExportEntry source, ExportEntry target)
        {
            PropertyCollection sourceProps = source.GetProperties();
            PropertyCollection targetProps = target.GetProperties();

            switch (target.ClassName)
            {
                case "StaticMeshActor":
                    CopyComponentToProps(pcc, "StaticMeshComponent", target, sourceProps, targetProps);
                    CopyComponentToProps(pcc, "CollisionComponent", target, sourceProps, targetProps);
                    break;
                default:
                    break;
            }

            target.WriteProperties(targetProps);
        }

        /// <summary>
        /// Copy a source component into a new object, and add the new property to the target collection.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="componentName">Name of the component to copy.</param>
        /// <param name="parent">Parent export to link the component to.</param>
        /// <param name="sourceProps">Props possibly containing the component to copy.</param>
        /// <param name="targetProps">Props to copy the component into.</param>
        /// <returns></returns>
        private static void CopyComponentToProps(IMEPackage pcc, string componentName, ExportEntry parent, PropertyCollection sourceProps, PropertyCollection targetProps)
        {
            ObjectProperty componentRef = sourceProps.GetProp<ObjectProperty>(componentName);

            if (componentRef != null)
            {
                ExportEntry sourceComponent = ResolveEntryToExport(pcc.GetEntry(componentRef.Value));
                PropertyCollection sourceComponentProps = sourceComponent.GetProperties();
                ExportEntry newComponent = CreateExport(pcc, sourceComponent.ObjectName, sourceComponent.ClassName, parent, sourceComponentProps,
                    sourceComponent.GetBinaryData(), new byte[8]);

                targetProps.AddOrReplaceProp(new ObjectProperty(newComponent.UIndex, componentName));
            }
        }

        /// <summary>
        /// Clone an archetype from a prefab into the PersistentLevel.
        /// </summary>
        /// <param name="archetypeEntry">Archetype entry, may be an import or an export.</param>
        /// <param name="archetype">Resolved export of the archetype, in case it was an import.</param>
        /// <param name="persistentLevel">PersistentLevel to add the clone to.</param>
        /// <returns></returns>
        private static ExportEntry ClonePrefabArchetype(IEntry archetypeEntry, ExportEntry archetype, ExportEntry persistentLevel)
        {
            ExportEntry newExport = EntryCloner.CloneEntry(archetype);
            newExport.Archetype = archetypeEntry;
            newExport.idxLink = persistentLevel.UIndex;

            // Set the proper flags
            newExport.ObjectFlags &= ~UnrealFlags.EObjectFlags.ArchetypeObject;
            newExport.ObjectFlags &= ~UnrealFlags.EObjectFlags.Public;
            newExport.ObjectFlags |= UnrealFlags.EObjectFlags.Transactional;

            ObjectBinary binary = ObjectBinary.From(newExport);
            if (binary == null) { newExport.WriteBinary(ObjectBinary.Create(newExport.ClassName, newExport.FileRef.Game, newExport.GetProperties())); }

            return newExport;
        }

        /// <summary>
        /// Add lighting channels to the LightComponents, in case they don't have them, as otherwise they won't appear.
        /// You'd think that you'd be better copying the component and adding it in the PersistentLevel,
        /// but that seems to do nothing, they need to be added to the archetype in the Prefab.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="parent">Parent export of the LightComponent.</param>
        private static void AddLightingChannelsToComponent(IMEPackage pcc, ExportEntry parent)
        {
            ObjectProperty componentRef = parent.GetProperties().GetProp<ObjectProperty>("LightComponent");

            if (componentRef != null)
            {
                ExportEntry lightComponent = ResolveEntryToExport(pcc.GetEntry(componentRef.Value));

                StructProperty lightingChannels = lightComponent.GetProperty<StructProperty>("LightingChannels")
                    ?? new StructProperty("LightingChannelContainer", new PropertyCollection(), "LightingChannels");

                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Static"));
                lightingChannels.Properties.AddOrReplaceProp(new BoolProperty(true, "Dynamic"));

                lightComponent.WriteProperty(lightingChannels);
            }
        }

        /// <summary>
        /// Find the SunSprite components that are ImportEntries, if any, then change their package file to SFXGame, if it's BIOC_Base.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="props">Props containing the components references.</param>
        private static void FixPackageOfSunSprites(IMEPackage pcc, PropertyCollection props)
        {
            foreach (Property prop in props)
            {
                if (prop.PropType != PropertyType.ObjectProperty && !prop.Name.Name.Contains("Sprite")) { continue; }
                // if (prop.PropType != PropertyType.ObjectProperty || (!prop.Name.Name.Contains("HaloSprite") && !prop.Name.Name.Contains("FlareSprite"))) { continue; }

                IEntry flareEntry = pcc.GetEntry(((ObjectProperty)prop).Value);

                ImportEntry flareImport;

                // Try to find if the component or its archetype are an import,
                // so we can operate on them
                if (flareEntry is ExportEntry export)
                {
                    IEntry flareArchetype = export.Archetype;

                    if (flareArchetype is ImportEntry import) { flareImport = import; }
                    else { continue; } // Neither the component nor its archetype were an import, so we skip it
                }
                else
                {
                    flareImport = (ImportEntry)flareEntry;
                }

                if (flareImport.PackageFile.CaseInsensitiveEquals("BIOC_Base"))
                {
                    flareImport.PackageFile = "SFXGame";
                }
            }
        }

        /// <summary>
        /// Adds a level streaming kismet to either TheWorld or PersistentLevel.
        /// </summary>
        /// <param name="pew">Current PE window</param>
        public static void AddStreamingKismetExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null) { return; }

            string filename = PromptForStr("Name of file to set in the streaming kismet:", "Not a valid file name.");
            if (string.IsNullOrEmpty(filename))
            {
                ShowError("Not a valid file name.");
                return;
            }
            bool inPersistentLevel = PromptForBool("Yes adds the object to the PersistentLevel. No adds the object to TheWorld", "To PersistentLevel?");

            AddStreamingKismet(pew.Pcc, filename, inPersistentLevel);

            ShowSuccess($"{filename} streaming kismet added to {(inPersistentLevel ? "the PersistentLevel" : "TheWorld")}");
        }

        /// <summary>
        /// Set the loading and streaming of the given file name in all the BioTriggerStreams where the conditional file is present.
        /// </summary>
        /// <param name="pew">Current PE window</param>
        public static void StreamFileExperiment(PackageEditorWindow pew)
        {
            if (pew.Pcc == null) { return; }

            string filename = PromptForStr("Name of file to stream:", "Not a valid file name.");
            if (string.IsNullOrEmpty(filename))
            {
                ShowError("Not a valid file name.");
                return;
            }
            string conditionalFile = PromptForStr("Name of conditional file:", "Not a valid file name.");
            if (string.IsNullOrEmpty(conditionalFile))
            {
                ShowError("Not a valid file name.");
                return;
            }

            StreamFile(pew.Pcc, filename, conditionalFile);

            ShowSuccess($"Added loading and streaming for {filename} wherever {conditionalFile} is present");
        }

        // HELPER FUNCTIONS
        #region Helper functions
        /// <summary>
        /// Indicates a class that is related to the audio elements of a BioConversation.
        /// </summary>
        private enum AudioClass
        {
            WwiseStream, WwiseEvent, SoundCue, SoundNodeWave
        }
        #endregion
    }
}