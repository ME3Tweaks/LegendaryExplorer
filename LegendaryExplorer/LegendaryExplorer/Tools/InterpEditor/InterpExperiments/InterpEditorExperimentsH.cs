using System.Linq;
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
        public static void OpenFovoLineAudio(bool isMale, InterpEditorWindow iew)
        {
            var Pcc = iew.Pcc;
            var node = GetSelectedFOVOLine(iew);
            if (node is null) return;
            var stream = isMale ? node.WwiseStream_Male : node.WwiseStream_Female;

            if (stream is not null)
            {
                new SoundplorerWPF(stream).Show();
            }
            else
            {
                var soundplorerWPF = new SoundplorerWPF();
                soundplorerWPF.LoadFile(Pcc.FilePath);
                soundplorerWPF.Show();
            }
        }

        public static void OpenFovoLineFXA(bool isMale, InterpEditorWindow iew)
        {
            var Pcc = iew.Pcc;
            var node = GetSelectedFOVOLine(iew);
            if (node is null) return;
            var faceFx = isMale ? node.FaceFX_Male : node.FaceFX_Female;
            var faceFxUindex = isMale ? node.SpeakerTag.FaceFX_Male.UIndex : node.SpeakerTag.FaceFX_Female.UIndex;

            if (Pcc.IsUExport(faceFxUindex) && faceFx is not null)
            {
                new FaceFXEditorWindow(Pcc.GetUExport(faceFxUindex), faceFx).Show();
            }
            else if (Pcc.IsUExport(faceFxUindex))
            {
                new FaceFXEditorWindow(Pcc.GetUExport(faceFxUindex)).Show();
            }
            else
            {
                var facefxEditor = new FaceFXEditorWindow();
                facefxEditor.LoadFile(Pcc.FilePath);
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
            if (iew.timelineControl.MatineeTree.SelectedItem is InterpTrack track && track.Export.ClassName == "SFXInterpTrackPlayFaceOnlyVO")
            {
                var keys = track.Export.GetProperty<ArrayProperty<StructProperty>>("m_aFOVOKeys");
                if (keys is null || keys.Count == 0) return null;
                var conversationUindex = keys[0].GetProp<ObjectProperty>("pConversation").Value;
                var lineStrRef = keys[0].GetProp<IntProperty>("nLineStrRef")?.Value;
                IEntry conversationEntry = track.Export.FileRef.GetEntry(conversationUindex);
                if (conversationEntry is ImportEntry convImport)
                {
                    conversationEntry = EntryImporter.ResolveImport(convImport);
                }
                if (conversationEntry is null || conversationEntry is not ExportEntry convExport) return null;

                return (lineStrRef ?? -1, new ConversationExtended(convExport));
            }
            return null;
        }
    }
}