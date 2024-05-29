
using DocumentFormat.OpenXml.Wordprocessing;
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

namespace LegendaryExplorer.Tools.Sequence_Editor.Experiments
{
    /// <summary>
    /// Experiments in Sequence Editor (Kinkojiro's stuff)
    /// </summary>
    public static class SequenceEditorExperimentsK
    {
        /// <summary>
        /// Updates the variable links of the selected Interp. Add links to an Anchor object.
        /// </summary>
        /// <param name="sew">Sequence Editor window.</param>
        /// <param name="inLoop">Whether we're updating in a loop. Used to avoid double-checking what the caller has already checked.</param>
        /// <param name="interp">Interp to update. Used to pass an interp gotten from a loop, rather than being a selected object.</param>
        public static void UpdateAnchorVarLink(SequenceEditorWPF sew, ExportEntry anchorSeqVar, bool inLoop = false, ExportEntry interp = null)
        {
            if (interp == null)
            {
                if (sew.Pcc == null || sew.SelectedItem == null || sew.SelectedItem.Entry == null) { return; }

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

            List<StructProperty> anchorLinks = varLinks.Where(link => link.GetProp<StrProperty>("LinkDesc").Value == "Anchor").ToList();
            if (!anchorLinks.Any())
            {
                ShowError("Skipping as the selected Interp contains no Anchor variable link");
                return;
            }

            ObjectProperty anchorObj = anchorLinks.First().GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").FirstOrDefault();
            if (anchorObj != null)
            {
                ShowError("Skipping as anchor exists");
                return;
            }

            if (anchorSeqVar.ClassName != "SeqVar_Object" && anchorSeqVar.ClassName != "BioSeqVar_ObjectFindByTag")
            {
                ShowError("The anchor needs to be a SeqVar_Object or BioSeqVar_ObjectFindByTag");
                return;
            }

            StructProperty varLink = varLinks.Find(link => string.Equals(link.GetProp<StrProperty>("LinkDesc").Value, "Anchor", StringComparison.OrdinalIgnoreCase));
            KismetHelper.CreateVariableLink(interp, "Anchor", anchorSeqVar);

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
        public static void UpdateAllInterpAnchorsVarLinks(SequenceEditorWPF sew)
        {
            if (sew.Pcc == null || sew.SelectedSequence == null) { return; }

            if (sew.SelectedObjects.Count == 0)
            {
                ShowError("No Anchor selected.");
                return;
            }

            ExportEntry anchorObject = sew.Pcc.GetUExport(sew.SelectedObjects[0].UIndex);
            if(anchorObject == null || (anchorObject.ClassName != "SeqVar_Object" && anchorObject.ClassName != "BioSeqVar_ObjectFindByTag")) 
            {
                ShowError("Selected anchor is not valid.");
                return;
            }

            ExportEntry selectedSequence = null;
            selectedSequence = sew.Pcc.GetUExport(sew.SelectedSequence.UIndex);
            if (selectedSequence.ClassName != "Sequence")
            {
                ShowError("Selected sequence is not a valid sequence.");
                return;
            }

            IEnumerable<ExportEntry> interps = KismetHelper.GetAllSequenceElements(selectedSequence).Select(el => (ExportEntry)el);

            interps = interps.Where(export =>
            {
                // Keep only Interps that contain valid InterpDatas
                if (export.ClassName == "SeqAct_Interp")
                {
                    List<StructProperty> varLinks = export.GetProperty<ArrayProperty<StructProperty>>("VariableLinks").ToList();
                    if (varLinks == null) { return false; }
                    List<StructProperty> dataLinks = varLinks.Where(link => link.GetProp<StrProperty>("LinkDesc").Value == "Anchor").ToList();
                    if (!dataLinks.Any()) { return false; }
                    ObjectProperty dataObj = dataLinks.First().GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables").FirstOrDefault();
                    if (dataObj != null) { return false; }
                    return true;
                }
                else
                {
                    return false;
                }
            }).ToList();

            if (!interps.Any())
            {
                ShowError($"No Interps without anchors found.");
                return;
            }

            foreach (ExportEntry interp in interps)
            {
                UpdateAnchorVarLink(sew, anchorObject, true, interp);
            }

            MessageBox.Show($"Interps' anchor links were updated", "Success", MessageBoxButton.OK);
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
