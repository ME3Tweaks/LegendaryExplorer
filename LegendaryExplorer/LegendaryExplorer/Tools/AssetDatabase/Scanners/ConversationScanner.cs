using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.AssetDatabase.Scanners
{
    internal class ConversationScanner : AssetScanner
    {
        public ConversationScanner() : base()
        {
        }

        public override void ScanExport(ExportScanInfo e, ConcurrentAssetDB db, AssetDBScanOptions options)
        {
            if (e.IsDefault) return;
            if (e.ClassName == "BioConversation" && !db.GeneratedConvo.ContainsKey(e.Export.ObjectName.Instanced))
            {
                bool IsAmbient = true;

                var speakers = GetSpeakers(e.Export, e.Properties);

                var entryprop = e.Properties.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
                foreach (StructProperty Node in entryprop)
                {
                    int speakerindex = Node.GetProp<IntProperty>("nSpeakerIndex");
                    speakerindex = speakerindex + 2;
                    if (speakerindex < 0 || speakerindex >= speakers.Count)
                        continue;
                    int linestrref = 0;
                    var linestrrefprop = Node.GetProp<StringRefProperty>("srText");
                    if (linestrrefprop != null)
                    {
                        linestrref = linestrrefprop.Value;
                    }

                    var ambientLine = Node.GetProp<BoolProperty>("IsAmbient");
                    if (IsAmbient)
                        IsAmbient = ambientLine;

                    var newLine = new ConvoLine(linestrref, speakers[speakerindex], e.Export.ObjectName.Instanced);
                    if (HasTLKLine(newLine, e.Export.FileRef))
                    {
                        db.GeneratedLines.TryAdd(linestrref.ToString(), newLine);
                    }
                }

                var replyprop = e.Properties.GetProp<ArrayProperty<StructProperty>>("m_ReplyList");
                if (replyprop != null)
                {
                    foreach (StructProperty Node in replyprop)
                    {
                        int linestrref = 0;
                        var linestrrefprop = Node.GetProp<StringRefProperty>("srText");
                        if (linestrrefprop != null)
                        {
                            linestrref = linestrrefprop.Value;
                        }

                        var ambientLine = Node.GetProp<BoolProperty>("IsAmbient");
                        if (IsAmbient)
                            IsAmbient = ambientLine;

                        ConvoLine newLine = new(linestrref, "Shepard", e.Export.ObjectName.Instanced);
                        if (HasTLKLine(newLine, e.Export.FileRef))
                        {
                            db.GeneratedLines.TryAdd(linestrref.ToString(), newLine);
                        }
                    }
                }

                var newConv = new Conversation(e.Export.ObjectName.Instanced, IsAmbient, new FileKeyExportPair(e.FileKey, e.Export.UIndex));
                db.GeneratedConvo.TryAdd(e.Export.InstancedFullPath.ToLower(), newConv);
            }
        }

        private List<string> GetSpeakers(ExportEntry export, PropertyCollection props)
        {
            var speakers = new List<string> { "Shepard", "Owner" };
            if (!export.Game.IsGame3())
            {
                var s_speakers = props.GetProp<ArrayProperty<StructProperty>>("m_SpeakerList");
                if (s_speakers != null)
                {
                    speakers.AddRange(s_speakers.Select(t => t.GetProp<NameProperty>("sSpeakerTag").ToString()));
                }
            }
            else
            {
                var a_speakers = props.GetProp<ArrayProperty<NameProperty>>("m_aSpeakerList");
                if (a_speakers != null)
                {
                    foreach (NameProperty n in a_speakers)
                    {
                        speakers.Add(n.ToString());
                    }
                }
            }

            return speakers;
        }

        /// <summary>
        /// If game one, sets the ConvoLine line to resolved TLK string. Returns false if not possible
        /// </summary>
        /// <param name="line"></param>
        /// <param name="fileref"></param>
        /// <returns></returns>
        private static bool HasTLKLine(ConvoLine line, IMEPackage fileref)
        {
            if (fileref.Game == MEGame.ME1)
            {
                line.Line = ME1TalkFiles.FindDataById(line.StrRef, fileref);
                if (line.Line is "No Data" or "\"\"" or "\" \"" or " ")
                    return false;
            }
            else if (fileref.Game == MEGame.LE1)
            {
                line.Line = LE1TalkFiles.FindDataById(line.StrRef, fileref);
                if (line.Line is "No Data" or "\"\"" or "\" \"" or " ")
                    return false;
            }
            return true;
        }
    }
}
