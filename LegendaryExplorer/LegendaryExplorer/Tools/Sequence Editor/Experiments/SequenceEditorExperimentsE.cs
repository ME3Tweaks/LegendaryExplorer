
using LegendaryExplorer.Dialogs;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using static LegendaryExplorerCore.Kismet.SeqTools;

namespace LegendaryExplorer.Tools.Sequence_Editor.Experiments
{
    /// <summary>
    /// Experiments in Sequence Editor (Exkywor's stuff)
    /// </summary>
    public static class SequenceEditorExperimentsE
    {
        /// <summary>
        /// Updates the variable links of the selected Interp. Removes links that no longer have a matching InterpGroup,
        /// adds links for new groups, and keeps groups that remaing the same.
        /// </summary>
        /// <param name="sew">Sequence Editor window.</param>
        /// <param name="inLoop">Whether we're updating in a loop. Used to avoid double-checking what the caller has already checked.</param>
        /// <param name="interp">Interp to update. Used to pass an interp gotten from a loop, rather than being a selected object.</param>
        public static void UpdateVarLinks(SequenceEditorWPF sew, bool inLoop = false, ExportEntry interp = null)
        {
            if (interp == null)
            {
                if (sew.Pcc == null || sew.SelectedItem == null || sew.SelectedItem.Entry == null) { return; }

                if (!sew.SelectedObjects.HasExactly(1))
                {
                    ShowError("Select only one Interp object");
                    return;
                }

                interp = sew.SelectedObjects[0].Export;
                if (interp.ClassName != "SeqAct_Interp")
                {
                    ShowError("Selected object is not a SeqAct_Interp");
                    return;
                }
            }

            // We get a list of StructProperties instead of VarLinkInfo because we want to keep existing ones intact
            List<StructProperty> varLinks = interp.GetProperty<ArrayProperty<StructProperty>>("VariableLinks").ToList();
            if (varLinks == null)
            {
                ShowError("The selected Interp contains no VariableLinks");
                return;
            }


            List<StructProperty> dataLinks = varLinks.Where(link => link.GetProp<StrProperty>("LinkDesc").Value == "Data").ToList();
            if (!dataLinks.Any())
            {
                ShowError("The selected Interp contains no Data variable link");
                return;
            }

            ObjectProperty dataObj = dataLinks.First().GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").FirstOrDefault();
            if (dataObj == null)
            {
                ShowError("No InterpDatas were linked to the Data variable link");
                return;
            }

            ExportEntry interpData = sew.Pcc.GetUExport(dataObj.Value);
            if (interpData.ClassName != "InterpData")
            {
                ShowError("The first object linked to the Data variable link is not an InterpData");
                return;
            }

            // Don't check if there are no InterpGroups, since an update could be to remove all of them
            ArrayProperty<ObjectProperty> interpGroups = interpData.GetProperty<ArrayProperty<ObjectProperty>>("InterpGroups");

            List<string> groupNames = new();
            // We want to keep the Data and Anchor links
            groupNames.Add("Data");
            groupNames.Add("Anchor");

            groupNames.AddRange(interpGroups.Where(id =>
            {
                ExportEntry group = null;
                if (!sew.Pcc.TryGetUExport(id.Value, out group)) { return false; }
                return group.GetProperty<NameProperty>("GroupName") != null;
            }).Select(id =>
            {
                ExportEntry group = sew.Pcc.GetUExport(id.Value);
                return group.GetProperty<NameProperty>("GroupName").Value.Name;
            }).Distinct().ToList());

            List<StructProperty> updatedLinks = new();
            foreach (string name in groupNames)
            {
                // Keep existing varLinks if they are the default Data or Anchor, or if it already exists in the list, and add new links.
                StructProperty varLink = varLinks.Find(link => string.Equals(link.GetProp<StrProperty>("LinkDesc").Value, name, StringComparison.OrdinalIgnoreCase));
                if (varLink != null)
                {
                    updatedLinks.Add(varLink);
                }
                else
                {
                    PropertyCollection props = GlobalUnrealObjectInfo.getDefaultStructValue(sew.Pcc.Game, "SeqVarLink", true);
                    props.AddOrReplaceProp(new StrProperty(name, "LinkDesc"));
                    int index = sew.Pcc.FindImport("Engine.SeqVar_Object").UIndex;
                    props.AddOrReplaceProp(new ObjectProperty(index, "ExpectedType"));
                    props.AddOrReplaceProp(new IntProperty(1, "MinVars"));
                    props.AddOrReplaceProp(new IntProperty(255, "MaxVars"));
                    updatedLinks.Add(new StructProperty("SeqVarLink", props));
                }
            }

            KismetHelper.RemoveVariableLinks(interp); // Clear the variable links
            ArrayProperty<StructProperty> variableLinksProp = new ArrayProperty<StructProperty>("VariableLinks");
            updatedLinks.ForEach(link => variableLinksProp.Add(link));
            interp.WriteProperty(variableLinksProp);

            if (!inLoop)
            {
                MessageBox.Show($"Variable links updated", "Success", MessageBoxButton.OK);
            }
        }

        /// <summary>
        /// Updates the variable links of all selected interps, or all interps in the selected sequence, that contain an interpData.
        /// </summary>
        /// <param name="sew">Sequence Editor window.</param>
        /// <param name="selected">True to update only selected interps. Falls to update the whole sequence.</param>
        public static void UpdateSequenceVarLinks(SequenceEditorWPF sew, bool selected = false)
        {
            if (sew.Pcc == null || sew.SelectedSequence == null) { return; }

            ExportEntry selectedSequence = null;
            if (selected)
            {
                if (sew.SelectedObjects.Count == 0) { return; }
            }
            else
            {
                selectedSequence = sew.Pcc.GetUExport(sew.SelectedSequence.UIndex);
                if (selectedSequence.ClassName != "Sequence")
                {
                    ShowError("Selected sequence is not a valid sequence.");
                    return;
                }
            }

            IEnumerable<ExportEntry> interps = selected ? sew.SelectedObjects.Select(el => el.Export)
                                                        : GetAllSequenceElements(selectedSequence).Select(el => (ExportEntry)el);

            interps = interps.Where(export =>
            {
                // Keep only Interps that contain valid InterpDatas
                if (export.ClassName == "SeqAct_Interp")
                {
                    List<StructProperty> varLinks = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks").ToList();
                    if (varLinks == null) { return false; }
                    List<StructProperty> dataLinks = varLinks.Where(link => link.GetProp<StrProperty>("LinkDesc").Value == "Data").ToList();
                    if (!dataLinks.Any()) { return false; }
                    ObjectProperty dataObj = dataLinks.First().GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").FirstOrDefault();
                    if (dataObj == null) { return false; }
                    ExportEntry interpData = sew.Pcc.GetUExport(dataObj.Value);
                    if (interpData.ClassName != "InterpData") { return false; }
                    return true;
                }
                else
                {
                    return false;
                }
            }).ToList();

            if (!interps.Any())
            {
                ShowError($"Selected {(selected ? "interps contained no" : "sequence contains no interps with")} linked interpDatas.");
                return;
            }

            foreach (ExportEntry interp in interps)
            {
                UpdateVarLinks(sew, true, interp);
            }

            MessageBox.Show($"All {(selected ? "selected " : "")}interps' variable links were updated", "Success", MessageBoxButton.OK);
        }

        /// <summary>
        /// Adds an Interp and an InterpData template to control a dialogue wheel director or camera.
        /// </summary>
        /// <param name="sew">Current SE instance.</param>
        /// <param name="wantDir">True to add director template, false for camera template.</param>
        public static void AddDialogueWheelTemplate(SequenceEditorWPF sew, bool wantDir = false)
        {
            if (sew.Pcc == null || sew.SelectedSequence == null) { return; }

            string camActor = promptForActor($"Name of camera actor to {(wantDir ? "control" : "add")}:", "Not a valid camera actor name.");

            PackageCache cache = new();

            // Add the interp with the required properties
            ExportEntry interp = SequenceObjectCreator.CreateSequenceObject(sew.Pcc, "SeqAct_Interp", cache);
            PropertyCollection interpProps = interp.GetProperties();

            ArrayProperty<StructProperty> variableLinks = interp.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            // Add camera varLink
            PropertyCollection camProps = GlobalUnrealObjectInfo.getDefaultStructValue(sew.Pcc.Game, "SeqVarLink", true);
            camProps.AddOrReplaceProp(new StrProperty(camActor, "LinkDesc"));
            int objIdx = sew.Pcc.FindImport("Engine.SeqVar_Object").UIndex;
            camProps.AddOrReplaceProp(new ObjectProperty(objIdx, "ExpectedType"));
            variableLinks.Add(new StructProperty("SeqVarLink", camProps));
            interpProps.AddOrReplaceProp(variableLinks);

            // Add other properties
            ArrayProperty<StrProperty> comment = new("m_aObjComment");
            comment.Add(new StrProperty($"{(wantDir ? "Wheel Director" : "Wheel Camera")}"));
            interpProps.AddOrReplaceProp(comment);
            interpProps.AddOrReplaceProp(new BoolProperty(true, "bRewindOnPlay"));
            interpProps.AddOrReplaceProp(new BoolProperty(true, "bClientSideOnly"));
            interpProps.AddOrReplaceProp(new BoolProperty(true, "bLooping"));

            interp.WriteProperties(interpProps);
            interp.RemoveProperty("OutputLinks");

            ExportEntry interpData = SequenceObjectCreator.CreateSequenceObject(sew.Pcc, "InterpData", cache);
            PropertyCollection interpDataProps = new PropertyCollection();
            interpDataProps.AddOrReplaceProp(new FloatProperty(20, "InterpLength"));
            interpData.WriteProperties(interpDataProps);

            // Add the objects to the sequence
            KismetHelper.AddObjectToSequence(interp, sew.SelectedSequence);
            KismetHelper.AddObjectToSequence(interpData, sew.SelectedSequence);
            KismetHelper.CreateVariableLink(interp, "Data", interpData);

            if (wantDir)
            {
                MatineeHelper.AddPreset("Director", interpData, sew.Pcc.Game);
                ExportEntry camGroup = MatineeHelper.AddNewGroupToInterpData(interpData, camActor);
                camGroup.WriteProperty(new NameProperty(camActor, "m_nmSFXFindActor"));
            }
            else
            {
                MatineeHelper.AddPreset("Camera", interpData, sew.Pcc.Game, camActor);
            }

            MessageBox.Show($"Added dialogue wheel {(wantDir ? "director" : "camera")} template", "Success", MessageBoxButton.OK);
        }

        private static void ShowError(string errMsg)
        {
            MessageBox.Show(errMsg, "Warning", MessageBoxButton.OK);
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
    }
}
