using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using static LegendaryExplorer.Misc.ExperimentsTools.SharedMethods;

namespace LegendaryExplorer.Tools.InterpEditor.InterpExperiments
{
    /// <summary>
    /// Class for Exkywor's preset buttons and stuff
    /// </summary>
    class InterpEditorExperimentsE
    {
        /// <summary>
        /// Wrapper for InsertTrackMoveKey.
        /// </summary>
        /// <param name="iew">Current IE window.</param>
        public static void InsertTrackMoveKeyExperiment(InterpEditorWindow iew)
        {
            if (iew.Pcc == null || iew.Properties_InterpreterWPF == null || iew.Properties_InterpreterWPF.CurrentLoadedExport == null) { return; }

            ExportEntry trackMove = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (trackMove.ClassName is not "InterpTrackMove")
            {
                MessageBox.Show("Selected export is not an InterpTrackMove", "Warning", MessageBoxButton.OK);
                return;
            }

            // Gather all the values from the user
            string moveVals = PromptDialog.Prompt(null,
                "Write the x,y,z position values, the x(roll),y(pitch),z(yaw) rotation values, and the time at which to set the movement in the following form:\n" +
                "posX,posY,posZ;rotX,rotY,rotZ;time\n\n" +
                "Use periods for decimals, not commas."
                );
            if (string.IsNullOrEmpty(moveVals))
            {
                MessageBox.Show("Invalid movement values", "Warning", MessageBoxButton.OK);
                return;
            }

            string[] moveValsArr = moveVals.Trim().Split(";");
            if (moveValsArr.Length != 3)
            {
                MessageBox.Show("Did not provide one of the pos, rot, or time values.", "Warning", MessageBoxButton.OK);
                return;
            }

            string[] posArrAsStr = moveValsArr[0].Trim().Split(",");
            if (posArrAsStr.Length != 3)
            {
                MessageBox.Show("Did not provide one of the X, Y, or Z position values.", "Warning", MessageBoxButton.OK);
                return;
            }

            List<float> posArr = new();
            foreach (string val in posArrAsStr)
            {
                if (!float.TryParse(val.Trim(), out float f))
                {
                    MessageBox.Show($"Error parsing the value \"{val.Trim()}\" for the position.", "Warning", MessageBoxButton.OK);
                    return;
                }
                posArr.Add(f);
            }

            string[] rotArrAsStr = moveValsArr[1].Trim().Split(",");
            if (rotArrAsStr.Length != 3)
            {
                MessageBox.Show("Did not provide one of the X, Y, or Z rotation values.", "Warning", MessageBoxButton.OK);
                return;
            }

            List<float> rotArr = new();
            foreach (string val in rotArrAsStr)
            {
                if (!float.TryParse(val.Trim(), out float f))
                {
                    MessageBox.Show($"Error parsing the value \"{val.Trim()}\" for the rotation.", "Warning", MessageBoxButton.OK);
                    return;
                }
                rotArr.Add(f);
            }

            string timeStr = moveValsArr[2].Trim();
            if (!float.TryParse(timeStr, out float time))
            {
                MessageBox.Show("Error parsing the time.", "Warning", MessageBoxButton.OK);
                return;
            }

            InsertTrackMoveKey(trackMove, posArr, rotArr, time);

            MessageBox.Show($"Keys at {moveVals.Trim()} successfully added", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Inserts a position, rotation, and time keys to the InterpTrackMove at the specified index.
        /// </summary>
        /// <param name="trackMove">InterpTrackMove to operate on.</param>
        /// <param name="posArr">Position values array.</param>
        /// <param name="rotArr">Rotation values array.</param>
        /// <param name="time">Time to set the keys at.</param>
        private static void InsertTrackMoveKey(ExportEntry trackMove, List<float> posArr, List<float> rotArr, float time)
        {
            IMEPackage pcc = trackMove.FileRef;

            PropertyCollection props = trackMove.GetProperties();
            StructProperty posTrack = props.GetProp<StructProperty>("PosTrack");
            StructProperty eulerTrack = props.GetProp<StructProperty>("EulerTrack");
            StructProperty lookupTrack = props.GetProp<StructProperty>("LookupTrack");

            if (posTrack == null || eulerTrack == null || lookupTrack == null)
            {
                MessageBox.Show("The TrackMove is missing one of the three array properties.", "Warning", MessageBoxButton.OK);
                return;
            }

            int insertIdx = GetIndexByTime(lookupTrack.GetProp<ArrayProperty<StructProperty>>("Points"), time, trackMove.ClassName);

            StructProperty posProp = GenerateArrayStructProp(pcc.Game, "Points", "InterpCurveVector");
            posProp.GetProp<FloatProperty>("InVal").Value = time;
            StructProperty outVal = posProp.GetProp<StructProperty>("OutVal");
            outVal.GetProp<FloatProperty>("X").Value = posArr[0];
            outVal.GetProp<FloatProperty>("Y").Value = posArr[1];
            outVal.GetProp<FloatProperty>("Z").Value = posArr[2];
            posTrack.GetProp<ArrayProperty<StructProperty>>("Points").Insert(insertIdx, posProp);

            StructProperty eulerProp = GenerateArrayStructProp(pcc.Game, "Points", "InterpCurveVector");
            eulerProp.GetProp<FloatProperty>("InVal").Value = time;
            outVal = eulerProp.GetProp<StructProperty>("OutVal");
            outVal.GetProp<FloatProperty>("X").Value = rotArr[0];
            outVal.GetProp<FloatProperty>("Y").Value = rotArr[1];
            outVal.GetProp<FloatProperty>("Z").Value = rotArr[2];
            eulerTrack.GetProp<ArrayProperty<StructProperty>>("Points").Insert(insertIdx, eulerProp);

            StructProperty lookupProp = GenerateArrayStructProp(pcc.Game, "Points", "InterpLookupTrack");
            lookupProp.GetProp<FloatProperty>("Time").Value = time;
            lookupTrack.GetProp<ArrayProperty<StructProperty>>("Points").Insert(insertIdx, lookupProp);

            trackMove.WriteProperties(props);
        }

        /// <summary>
        /// Deletes the position, rotation, and time keys of the InterpTrackMove at the specific index.
        /// </summary>
        /// <param name="iew">Current IE window.</param>
        public static void DeleteTrackMoveKey(InterpEditorWindow iew)
        {
            if (iew.Pcc == null || iew.Properties_InterpreterWPF == null || iew.Properties_InterpreterWPF.CurrentLoadedExport == null) { return; }

            ExportEntry trackMove = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (trackMove.ClassName is not "InterpTrackMove")
            {
                MessageBox.Show("Selected export is not an InterpTrackMove", "Warning", MessageBoxButton.OK);
                return;
            }

            int idx = PromptForInt("Provide the 0-based index of the key to remove.", "Invalid index", -1);
            if (idx == -1) { return; }

            PropertyCollection props = trackMove.GetProperties();
            StructProperty posTrack = props.GetProp<StructProperty>("PosTrack");
            StructProperty eulerTrack = props.GetProp<StructProperty>("EulerTrack");
            StructProperty lookupTrack = props.GetProp<StructProperty>("LookupTrack");

            if (posTrack == null || eulerTrack == null || lookupTrack == null)
            {
                MessageBox.Show("The TrackMove is missing one of the three array properties.", "Warning", MessageBoxButton.OK);
                return;
            }

            try
            {
                posTrack.GetProp<ArrayProperty<StructProperty>>("Points").RemoveAt(idx);
                eulerTrack.GetProp<ArrayProperty<StructProperty>>("Points").RemoveAt(idx);
                lookupTrack.GetProp<ArrayProperty<StructProperty>>("Points").RemoveAt(idx);
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}", "Error", MessageBoxButton.OK);
                return;
            }

            trackMove.WriteProperties(props);

            MessageBox.Show($"Keys {idx} successfully deleted", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Inserts a DOF and time keys to the DOF track at the specified index.
        /// </summary>
        /// <param name="iew">Current IE window.</param>
        public static void InsertDOFKey(InterpEditorWindow iew)
        {
            if (iew.Pcc == null || iew.Properties_InterpreterWPF == null || iew.Properties_InterpreterWPF.CurrentLoadedExport == null) { return; }

            ExportEntry dofTrack = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (dofTrack.ClassName is not "BioEvtSysTrackDOF")
            {
                MessageBox.Show("Selected export is not an BioEvtSysTrackDOF", "Warning", MessageBoxButton.OK);
                return;
            }

            // Gather all the values from the user
            string dofVals = PromptDialog.Prompt(null,
                "Write the DOF inner radius, the focus distance, and the time at which to set it in the following form:\n" +
                "innerRadius;focusDistance;time\n\n" +
                "Use periods for decimals, not commas."
                );
            if (string.IsNullOrEmpty(dofVals))
            {
                MessageBox.Show("Invalid DOF values", "Warning", MessageBoxButton.OK);
                return;
            }

            string[] dofValsArr = dofVals.Trim().Split(";");
            if (dofValsArr.Length != 3)
            {
                MessageBox.Show("Did not provide one of the inner radius, focus distance, or time values.", "Warning", MessageBoxButton.OK);
                return;
            }

            if (!float.TryParse(dofValsArr[0].Trim(), out float innerRadius))
            {
                MessageBox.Show($"Error parsing the inner radius.", "Warning", MessageBoxButton.OK);
                return;
            }

            if (!float.TryParse(dofValsArr[1].Trim(), out float focusDistance))
            {
                MessageBox.Show($"Error parsing the focus distance.", "Warning", MessageBoxButton.OK);
                return;
            }

            if (!float.TryParse(dofValsArr[2].Trim(), out float time))
            {
                MessageBox.Show("Error parsing the time.", "Warning", MessageBoxButton.OK);
                return;
            }

            PropertyCollection props = dofTrack.GetProperties();
            ArrayProperty<StructProperty> m_aDOFData = props.GetProp<ArrayProperty<StructProperty>>("m_aDOFData");
            ArrayProperty<StructProperty> m_aTrackKeys = props.GetProp<ArrayProperty<StructProperty>>("m_aTrackKeys");

            if (m_aDOFData == null || m_aTrackKeys == null)
            {
                MessageBox.Show("The DOF track is missing either the DOFData or TrackKeys array properties.", "Warning", MessageBoxButton.OK);
                return;
            }

            int insertIdx = GetIndexByTime(m_aTrackKeys, time, dofTrack.ClassName);

            StructProperty dofProp = GenerateArrayStructProp(iew.Pcc.Game, "m_aDOFData", "BioEvtSysTrackDOF");
            dofProp.GetProp<FloatProperty>("fFocusInnerRadius").Value = innerRadius;
            dofProp.GetProp<FloatProperty>("fFocusDistance").Value = focusDistance;
            dofProp.GetProp<BoolProperty>("bEnableDOF").Value = true;
            m_aDOFData.Insert(insertIdx, dofProp);

            StructProperty timeProp = GenerateArrayStructProp(iew.Pcc.Game, "m_aTrackKeys", "BioEvtSysTrackDOF");
            timeProp.GetProp<FloatProperty>("fTime").Value = time;
            m_aTrackKeys.Insert(insertIdx, timeProp);

            dofTrack.WriteProperties(props);

            MessageBox.Show($"Keys at {dofVals.Trim()} successfully added", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Deletes the DOF and time keys of the DOF track at specific index.
        /// </summary>
        /// <param name="iew">Current IE window.</param>
        public static void DeleteDOFKey(InterpEditorWindow iew)
        {
            if (iew.Pcc == null || iew.Properties_InterpreterWPF == null || iew.Properties_InterpreterWPF.CurrentLoadedExport == null) { return; }

            ExportEntry dofTrack = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (dofTrack.ClassName is not "BioEvtSysTrackDOF")
            {
                MessageBox.Show("Selected export is not an BioEvtSysTrackDOF", "Warning", MessageBoxButton.OK);
                return;
            }

            int idx = PromptForInt("Provide the 0-based index of the key to remove.", "Invalid index", -1);
            if (idx == -1) { return; }

            PropertyCollection props = dofTrack.GetProperties();
            ArrayProperty<StructProperty> m_aDOFData = props.GetProp<ArrayProperty<StructProperty>>("m_aDOFData");
            ArrayProperty<StructProperty> m_aTrackKeys = props.GetProp<ArrayProperty<StructProperty>>("m_aTrackKeys");

            if (m_aDOFData == null || m_aTrackKeys == null)
            {
                MessageBox.Show("The DOF track is missing either the DOFData or TrackKeys array properties.", "Warning", MessageBoxButton.OK);
                return;
            }

            try
            {
                m_aDOFData.RemoveAt(idx);
                m_aTrackKeys.RemoveAt(idx);
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}", "Error", MessageBoxButton.OK);
                return;
            }

            dofTrack.WriteProperties(props);

            MessageBox.Show($"Keys {idx} successfully deleted", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Inserts a Gesture and time keys to the Gesture track at the specified index.
        /// </summary>
        /// <param name="iew">Current IE window.</param>
        public static void InsertGestureKey(InterpEditorWindow iew)
        {
            if (iew.Pcc == null || iew.Properties_InterpreterWPF == null || iew.Properties_InterpreterWPF.CurrentLoadedExport == null) { return; }

            ExportEntry trackGesture = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (trackGesture.ClassName is not "BioEvtSysTrackGesture")
            {
                MessageBox.Show("Selected export is not an BioEvtSysTrackGesture", "Warning", MessageBoxButton.OK);
                return;
            }

            float time = PromptForFloat("Time to insert the key at:", "Not a valid time.", "Time key");

            PropertyCollection props = trackGesture.GetProperties();
            ArrayProperty<StructProperty> m_aGestures = props.GetProp<ArrayProperty<StructProperty>>("m_aGestures");
            ArrayProperty<StructProperty> m_aTrackKeys = props.GetProp<ArrayProperty<StructProperty>>("m_aTrackKeys");

            if (m_aGestures == null || m_aTrackKeys == null)
            {
                MessageBox.Show("The Gestures track is missing either the Gestures or TrackKeys array properties.", "Warning", MessageBoxButton.OK);
                return;
            }

            int insertIdx = GetIndexByTime(m_aTrackKeys, time, trackGesture.ClassName);

            StructProperty gestureProp = GenerateArrayStructProp(iew.Pcc.Game, "m_aGestures", "BioEvtSysTrackGesture");
            m_aGestures.Insert(insertIdx, gestureProp);

            StructProperty timeProp = GenerateArrayStructProp(iew.Pcc.Game, "m_aTrackKeys", "BioEvtSysTrackGesture");
            timeProp.GetProp<FloatProperty>("fTime").Value = time;
            m_aTrackKeys.Insert(insertIdx, timeProp);

            trackGesture.WriteProperties(props);

            MessageBox.Show($"Keys at {time} successfully added", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Deletes the Gesture and time keys of the Gesture track at specific index.
        /// </summary>
        /// <param name="iew">Current IE window.</param>
        public static void DeleteGestureKey(InterpEditorWindow iew)
        {
            if (iew.Pcc == null || iew.Properties_InterpreterWPF == null || iew.Properties_InterpreterWPF.CurrentLoadedExport == null) { return; }

            ExportEntry gesturesTrack = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (gesturesTrack.ClassName is not "BioEvtSysTrackGesture")
            {
                MessageBox.Show("Selected export is not an BioEvtSysTrackGesture", "Warning", MessageBoxButton.OK);
                return;
            }

            int idx = PromptForInt("Provide the 0-based index of the key to remove.", "Invalid index", -1);
            if (idx == -1) { return; }

            PropertyCollection props = gesturesTrack.GetProperties();
            ArrayProperty<StructProperty> m_aGestures = props.GetProp<ArrayProperty<StructProperty>>("m_aGestures");
            ArrayProperty<StructProperty> m_aTrackKeys = props.GetProp<ArrayProperty<StructProperty>>("m_aTrackKeys");

            if (m_aGestures == null || m_aTrackKeys == null)
            {
                MessageBox.Show("The Gestures track is missing either the Gestures or TrackKeys array properties.", "Warning", MessageBoxButton.OK);
                return;
            }

            try
            {
                m_aGestures.RemoveAt(idx);
                m_aTrackKeys.RemoveAt(idx);
            }
            catch (Exception e)
            {
                MessageBox.Show($"{e.Message}", "Error", MessageBoxButton.OK);
                return;
            }

            gesturesTrack.WriteProperties(props);

            MessageBox.Show($"Keys {idx} successfully deleted", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Generate a StructProperty to add to an array.
        /// </summary>
        /// <param name="game">Game the pcc is of.</param>
        /// <param name="parentName">Name of the parent to add the struct to.</param>
        /// <param name="containingType">Classname of the struct.</param>
        /// <returns>Generated StructProperty</returns>
        private static StructProperty GenerateArrayStructProp(MEGame game, string parentName, string containingType)
        {
            PropertyInfo p = GlobalUnrealObjectInfo.GetPropertyInfo(game, parentName, containingType);
            string typeName = p.Reference;
            PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(game, typeName, true);
            return new StructProperty(typeName, props, isImmutable: GlobalUnrealObjectInfo.IsImmutable(typeName, game));
        }

        /// <summary>
        /// Get the index at which to insert a key so that it's inserted before the next element in the timeline.
        /// </summary>
        /// <param name="arr">Array to find the index in.</param>
        /// <param name="time">Time ot find the index of.</param>
        /// <param name="className">Track's class name.</param>
        /// <returns>The index at which to insert the key.</returns>
        private static int GetIndexByTime(ArrayProperty<StructProperty> arr, float time, string className)
        {
            int idx = 0;

            for (int i = 0; i < arr.Count; i++)
            {
                StructProperty prop = arr[i];
                FloatProperty timeProp = prop.GetProp<FloatProperty>($"{(className == "InterpTrackMove" ? "Time" : "fTime")}");

                if (time > timeProp.Value) { idx = i + 1; }
                else
                {
                    idx = i;
                    break;
                }
            }

            return idx;
        }

        public static void AddPresetGroup(string preset, InterpEditorWindow iew)
        {
            var currExp = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (currExp != null && iew.Pcc != null)
            {
                var game = iew.Pcc.Game;

                if (!(game.IsGame3() || game.IsGame2()))
                {
                    MessageBox.Show("This experiment is not available for ME1/LE1 files.", "Warning", MessageBoxButton.OK);
                    return;
                }

                if (currExp.ClassName != "InterpData")
                {
                    MessageBox.Show("InterpData not selected.", "Warning", MessageBoxButton.OK);
                    return;
                }

                switch (preset)
                {
                    case "Director":
                        MatineeHelper.AddPreset(preset, currExp, game);
                        break;

                    case "Camera":
                        var camActor = PromptForStr("Name of camera actor:", "Not a valid camera actor name.");
                        if (!string.IsNullOrEmpty(camActor))
                        {
                            MatineeHelper.AddPreset(preset, currExp, game, camActor);
                        }
                        break;

                    case "Actor":
                        var actActor = PromptForStr("Name of actor:", "Not a valid actor name.");
                        if (!string.IsNullOrEmpty(actActor))
                        {
                            MatineeHelper.AddPreset(preset, currExp, game, actActor);
                        }
                        break;
                }
            }
            return;
        }

        public static void AddPresetTrack(string preset, InterpEditorWindow iew)
        {
            var currExp = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (currExp != null && iew.Pcc != null)
            {
                var game = iew.Pcc.Game;

                if (!(game.IsGame3() || game.IsGame2()))
                {
                    MessageBox.Show("This experiment is not available for ME1/LE1 files.", "Warning", MessageBoxButton.OK);
                    return;
                }

                if (currExp.ClassName != "InterpGroup")
                {
                    MessageBox.Show("InterpGroup not selected.", "Warning", MessageBoxButton.OK);
                    return;
                }

                switch (preset)
                {
                    case "Gesture":
                    case "Gesture2":
                        var actor = PromptForStr("Name of gesture actor:", "Not a valid gesture actor name.");
                        if (!string.IsNullOrEmpty(actor))
                        {
                            MatineeHelper.AddPreset(preset, currExp, game, actor);
                        }
                        break;
                }
            }
            return;
        }

        /// <summary>
        /// Add a Camera InterpGroup with its actor set along with Move and FOV tracks, and inserting a position, rotation, and time keys to its Move track.
        /// </summary>
        /// <param name="iew">Current IE window.</param>
        public static void AddPresetCameraWithKeys(InterpEditorWindow iew)
        {
            if (iew.Pcc == null || iew.Properties_InterpreterWPF == null || iew.Properties_InterpreterWPF.CurrentLoadedExport == null) { return; }

            if (iew.Pcc.Game.IsGame1())
            {
                MessageBox.Show("This experiment is not available for ME1/LE1 files.", "Warning", MessageBoxButton.OK);
                return;
            }

            if (iew.Properties_InterpreterWPF.CurrentLoadedExport.ClassName is not "InterpData")
            {
                MessageBox.Show("Selected export is not an InterpData", "Warning", MessageBoxButton.OK);
                return;
            }

            string camActor = PromptForStr("Name of camera actor:", "Not a valid camera actor name.");

            ExportEntry group = MatineeHelper.AddPreset("Camera", iew.Properties_InterpreterWPF.CurrentLoadedExport, iew.Pcc.Game, camActor);

            MatineeHelper.TryGetInterpTrack(group, "InterpTrackMove", out ExportEntry trackMove);

            // Gather all the values from the user
            string moveVals = PromptDialog.Prompt(null,
                "Write the x,y,z position values, the x(roll),y(pitch),z(yaw) rotation values, and the time at which to set the movement in the following form:\n" +
                "posX,posY,posZ;rotX,rotY,rotZ;time\n\n" +
                "Use periods for decimals, not commas."
                );
            if (string.IsNullOrEmpty(moveVals))
            {
                MessageBox.Show("Invalid movement values", "Warning", MessageBoxButton.OK);
                return;
            }

            string[] moveValsArr = moveVals.Trim().Split(";");
            if (moveValsArr.Length != 3)
            {
                MessageBox.Show("Did not provide one of the pos, rot, or time values.", "Warning", MessageBoxButton.OK);
                return;
            }

            string[] posArrAsStr = moveValsArr[0].Trim().Split(",");
            if (posArrAsStr.Length != 3)
            {
                MessageBox.Show("Did not provide one of the X, Y, or Z position values.", "Warning", MessageBoxButton.OK);
                return;
            }

            List<float> posArr = new();
            foreach (string val in posArrAsStr)
            {
                if (!float.TryParse(val.Trim(), out float f))
                {
                    MessageBox.Show($"Error parsing the value \"{val.Trim()}\" for the position.", "Warning", MessageBoxButton.OK);
                    return;
                }
                posArr.Add(f);
            }

            string[] rotArrAsStr = moveValsArr[1].Trim().Split(",");
            if (rotArrAsStr.Length != 3)
            {
                MessageBox.Show("Did not provide one of the X, Y, or Z rotation values.", "Warning", MessageBoxButton.OK);
                return;
            }

            List<float> rotArr = new();
            foreach (string val in rotArrAsStr)
            {
                if (!float.TryParse(val.Trim(), out float f))
                {
                    MessageBox.Show($"Error parsing the value \"{val.Trim()}\" for the rotation.", "Warning", MessageBoxButton.OK);
                    return;
                }
                rotArr.Add(f);
            }

            string timeStr = moveValsArr[2].Trim();
            if (!float.TryParse(timeStr, out float time))
            {
                MessageBox.Show("Error parsing the time.", "Warning", MessageBoxButton.OK);
                return;
            }

            InsertTrackMoveKey(trackMove, posArr, rotArr, time);

            MessageBox.Show($"Added actor preset InterpGroup with the provided move keys.", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Provided a full animation name, set the starting pose set, animation, and offset for the selected gesture track.
        /// </summary>
        /// <param name="iew">Current IE window.</param>
        public static void SetStartingPose(InterpEditorWindow iew)
        {
            if (iew.Pcc == null || iew.Properties_InterpreterWPF == null || iew.Properties_InterpreterWPF.CurrentLoadedExport == null) { return; }

            ExportEntry trackGesture = iew.Properties_InterpreterWPF.CurrentLoadedExport;

            if (trackGesture.ClassName is not "BioEvtSysTrackGesture")
            {
                MessageBox.Show("Selected export is not an BioEvtSysTrackGesture", "Warning", MessageBoxButton.OK);
                return;
            }

            string prompt = PromptForStr("StartingPoseSet, StartingPoseAnim, and StartPoseOffset separated by a semi-colon:", "Invalid animation.");

            string[] promptArr = prompt.Trim().Split(";");

            if (promptArr.Length != 3)
            {
                MessageBox.Show("Did not provide the required values.", "Warning", MessageBoxButton.OK);
                return;
            }

            string startingPoseSet = promptArr[0].Trim();
            if (string.IsNullOrEmpty(startingPoseSet))
            {
                MessageBox.Show("Invalid StartingPoseSet.", "Warning", MessageBoxButton.OK);
                return;
            }
            string startingPoseAnim = promptArr[1].Trim();
            if (string.IsNullOrEmpty(startingPoseAnim))
            {
                MessageBox.Show("Invalid StartingPoseAnim.", "Warning", MessageBoxButton.OK);
                return;
            }

            if (!float.TryParse(promptArr[2], out float offset))
            {
                MessageBox.Show("Invalid StartPoseOffset.", "Warning", MessageBoxButton.OK);
                return;
            }

            if (!iew.Pcc.Names.Contains($"{startingPoseSet}_{startingPoseAnim}"))
            {
                MessageBox.Show("The provided animation does not exist in the current package.", "Warning", MessageBoxButton.OK);
                return;
            }

            PropertyCollection props = trackGesture.GetProperties();
            props.AddOrReplaceProp(new NameProperty(startingPoseSet, "nmStartingPoseSet"));
            props.AddOrReplaceProp(new NameProperty(startingPoseAnim, "nmStartingPoseAnim"));
            props.AddOrReplaceProp(new FloatProperty(offset, "m_fStartPoseOffset"));

            trackGesture.WriteProperties(props);

            MessageBox.Show($"Successfully set the starting pose.", "Success", MessageBoxButton.OK);
        }
    }
}
