using System.Linq;
using System.Windows.Forms;
using LegendaryExplorer.Dialogs;
using LegendaryExplorer.DialogueEditor;
using LegendaryExplorer.Tools.FaceFXEditor;
using LegendaryExplorer.Tools.Soundplorer;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LibVLCSharp.Shared;

namespace LegendaryExplorer.Tools.InterpEditor.InterpExperiments
{
    public static class InterpEditorExperimentsH
    {
        public static void OpenFovoLineDialogueEditor(InterpEditorWindow iew)
        {
            var selectedLine = GetConversationFromSelectedTrack(iew);
            if (!selectedLine.HasValue) return;
            var (strRef, conv) = selectedLine.Value;
            var dlg = new DialogueEditorWindow();
            dlg.Show();
            dlg.LoadFile(conv.Export.FileRef.FilePath, conv.Export.UIndex);
            if(strRef > 0) dlg.TrySelectStrRef(strRef);
        }

        public static void OpenFovoLineAudio(bool isMale, InterpEditorWindow iew)
        {
            var node = GetSelectedFOVOLine(iew);
            var stream = isMale ? node?.WwiseStream_Male : node?.WwiseStream_Female;
            if (stream is null) return;
            new SoundplorerWPF(stream).Show();
        }

        public static void OpenFovoLineFXA(bool isMale, InterpEditorWindow iew)
        {
            var node = GetSelectedFOVOLine(iew);
            if (node is null) return;
            var faceFx = isMale ? node.FaceFX_Male : node.FaceFX_Female;
            var faceFxUIndex = isMale ? node.SpeakerTag.FaceFX_Male?.UIndex : node.SpeakerTag.FaceFX_Female?.UIndex;
            if (!faceFxUIndex.HasValue)
            {
                MessageBox.Show($@"Node {node.LineStrRef} appears to have no FaceFX. Aborting.");
                return;
            }

            var pcc = node.WwiseStream_Female?.FileRef ?? node.WwiseStream_Male?.FileRef;
            if (pcc is null) return;

            if (pcc.IsUExport(faceFxUIndex.Value) && faceFx is not null)
            {
                var fxe = new FaceFXEditorWindow();
                fxe.LoadFile(pcc.FilePath);
                fxe.Show();
                fxe.SelectAnimset(faceFxUIndex.Value, faceFx);
            }
            else if (pcc.IsUExport(faceFxUIndex.Value))
            {
                new FaceFXEditorWindow(pcc.GetUExport(faceFxUIndex.Value)).Show();
            }
            else
            {
                var facefxEditor = new FaceFXEditorWindow();
                facefxEditor.LoadFile(pcc.FilePath);
                facefxEditor.Show();
            }
        }

        private static DialogueNodeExtended GetSelectedFOVOLine(InterpEditorWindow iew)
        {
            var selected = GetConversationFromSelectedTrack(iew);
            if (selected is null) return null;
            var (strRef, conv) = selected.Value;
            //conv.LoadConversation(TLKManagerWPF.GlobalFindStrRefbyID, true);
            conv.ParseSpeakers();
            conv.ParseEntryList(TLKManagerWPF.GlobalFindStrRefbyID);
            conv.ParseReplyList(TLKManagerWPF.GlobalFindStrRefbyID);
            conv.ParseWwiseBank();
            conv.DetailedParse();

            return conv.EntryList.ToList().Concat(conv.ReplyList).FirstOrDefault(n => n.LineStrRef == strRef);
        }

        private static (int strRef, ConversationExtended conv)? GetConversationFromSelectedTrack(InterpEditorWindow iew)
        {
            if (iew.TimelineControl.MatineeTree.SelectedItem is InterpTrack track && track.Export.ClassName == "SFXInterpTrackPlayFaceOnlyVO")
            {
                var keys = track.Export.GetProperty<ArrayProperty<StructProperty>>("m_aFOVOKeys");
                if (keys is null || keys.Count == 0) return null;
                int keyIndex = 0;
                if (keys.Count > 1)
                {
                    string result = PromptDialog.Prompt(iew, "Please enter FOVO key index", "Legendary Explorer", "0", true);
                    if (string.IsNullOrEmpty(result) || !int.TryParse(result, out var idx) || idx >= keys.Count)
                    {
                        return null;
                    }
                    keyIndex = idx;
                }
                var conversationUindex = keys[keyIndex].GetProp<ObjectProperty>("pConversation").Value;
                var lineStrRef = keys[keyIndex].GetProp<IntProperty>("nLineStrRef")?.Value;
                IEntry conversationEntry = track.Export.FileRef.GetEntry(conversationUindex);
                if (conversationEntry is ImportEntry convImport)
                {
                    conversationEntry = EntryImporter.ResolveImport(convImport, new PackageCache());
                }
                if (conversationEntry is null || conversationEntry is not ExportEntry convExport) return null;

                return (lineStrRef ?? -1, new ConversationExtended(convExport));
            }
            return null;
        }
    }
}