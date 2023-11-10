using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Dialogue
{
    /// <summary>
    /// Contains the nested conversation structure of a BioConversation export, with extended parsing of most elements
    /// </summary>
    /// <remarks>
    /// Contains nested conversation structure
    /// - InterpData
    /// Extended Nested Collections:
    ///  - Speakers have FaceFX Objects
    ///  - DialogueNodeExtended has InterpData, WwiseStream_M, WwiseStream_F, FaceFX_ID_M, FaceFX_ID_F
    /// </remarks>
    [DebuggerDisplay("ConversationExtended {ExportUID} {ConvName}")]
    public class ConversationExtended : INotifyPropertyChanged
    {
        /// <summary>The UIndex of this BioConversation</summary>
        public int ExportUID { get; init; }
        /// <summary>The properties of this BioConversation</summary>
        public PropertyCollection BioConvo { get; }
        /// <summary>The export for the BioConversation</summary>
        public ExportEntry Export { get; }
        /// <summary>The object name of the BioConversation</summary>
        public string ConvName { get; init; }
        /// <summary>If true, this conversation has completed a detailed parse</summary>
        public bool IsParsed { get; set; }
        /// <summary>If true, this conversation has had an initial (non-detailed) parse completed</summary>
        /// <remarks>At the moment, this property is not set when running LoadConversation(), only by the DialogueEditor class in LEX</remarks>
        public bool IsFirstParsed { get; set; }

        /// <summary>
        /// A dictionary of starting entry nodes. The m_StartingList property
        /// </summary>
        /// <remarks>
        /// Key: Index into m_StartingList array.
        /// Value: Index into <see cref="EntryList"/> collection
        /// </remarks>
        public SortedDictionary<int, int> StartingList { get; private set; } = new();
        /// <summary>The speakers defined in this conversation, including player and owner. Parsed from the m_SpeakerList or m_aSpeakerList property</summary>
        public ObservableCollectionExtended<SpeakerExtended> Speakers { get; set; }
        /// <summary>The entry (non-player) dialogue nodes in this conversation, in correct order</summary>
        public ObservableCollectionExtended<DialogueNodeExtended> EntryList { get; private set; }
        /// <summary>The reply (player) dialogue nodes in this conversation, in correct order</summary>
        public ObservableCollectionExtended<DialogueNodeExtended> ReplyList { get; private set; }
        /// <summary>The stage directions defined in this conversation</summary>
        public ObservableCollectionExtended<StageDirection> StageDirections { get; } = new();
        /// <summary>The scripted events defined for use by this BioConversation. The m_aScriptList property</summary>
        public ObservableCollectionExtended<NameReference> ScriptList { get; } = new();

        /// <summary>Reference to the WwiseBank export for this BioConversation</summary>
        public ExportEntry WwiseBank { get; set; }
        /// <summary>Reference to the matinee sequence for this BioConversation</summary>
        public IEntry Sequence { get; set; }
        /// <summary>Reference to the NonSpeaker FaceFXAnimset for this BioConversation</summary>
        public IEntry NonSpkrFFX { get; set; }

        /// <summary>
        /// Creates a ConversationExtended from a BioConversation export
        /// </summary>
        /// <param name="export">Export to parse properties from</param>
        public ConversationExtended(ExportEntry export)
        {
            Export = export;
            ExportUID = export.UIndex;
            ConvName = export.ObjectName;
            BioConvo = export.GetProperties();
            Speakers = new ObservableCollectionExtended<SpeakerExtended>();
            EntryList = new ObservableCollectionExtended<DialogueNodeExtended>();
            ReplyList = new ObservableCollectionExtended<DialogueNodeExtended>();
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">Conversation to copy properties from</param>
        public ConversationExtended(ConversationExtended other)
        {
            ExportUID = other.ExportUID;
            ConvName = other.ConvName;
            BioConvo = other.BioConvo;
            Export = other.Export;
            IsParsed = other.IsParsed;
            IsFirstParsed = other.IsFirstParsed;
            StartingList = other.StartingList;
            Speakers = other.Speakers;
            EntryList = other.EntryList;
            ReplyList = other.ReplyList;
            WwiseBank = other.WwiseBank;
            Sequence = other.Sequence;
            NonSpkrFFX = other.NonSpkrFFX;
            ScriptList.AddRange(other.ScriptList);
            StageDirections.AddRange(other.StageDirections);
        }

        /// <summary>
        /// Parses all conversation data from the export into this class instance
        /// </summary>
        /// <param name="tlkLookup">Lambda function used to perform TLK lookups</param>
        /// <param name="detailedParse">If true, a detailed parse will be performed. This parses additional properties, such as InterpData, for each node.</param>
        public void LoadConversation(Func<int, IMEPackage, string> tlkLookup = null, bool detailedParse = false)
        {
            ParseStartingList();
            ParseSpeakers();
            ParseEntryList(tlkLookup);
            ParseReplyList(tlkLookup);
            ParseScripts();
            ParseNSFFX();
            ParseSequence();
            ParseWwiseBank();
            ParseStageDirections(tlkLookup);

            if (detailedParse)
            {
                DetailedParse();
            }
        }

        /// <summary>
        /// Performs a detailed parse of the BioConversation export and references. This primarily sets properties like
        /// FaceFX and InterpData on the individual <see cref="DialogueNodeExtended"/>s in the conversation.
        /// </summary>
        public void DetailedParse()
        {

            foreach (var spkr in Speakers)
            {
                spkr.FaceFX_Male = GetFaceFX(spkr.SpeakerID, true);
                spkr.FaceFX_Female = GetFaceFX(spkr.SpeakerID, false);
            }
            generateSpeakerTags();
            parseLinesInterpData();
            parseLinesFaceFX();
            parseLinesAudioStreams();
            parseLinesScripts();

            IsParsed = true;
        }

        /// <summary>
        /// Sets the <see cref="DialogueNodeExtended.SpeakerTag"/> property for each dialogue node in the conversation
        /// </summary>
        /// <remarks>Entry list, reply list, and speaker list should already be populated before calling</remarks>
        private void generateSpeakerTags()
        {
            foreach (var e in EntryList)
            {
                int spkridx = e.SpeakerIndex;
                var spkrtag = Speakers.FirstOrDefault(s => s.SpeakerID == spkridx);
                if (spkrtag != null)
                    e.SpeakerTag = spkrtag;
            }

            foreach (var r in ReplyList)
            {
                int spkridx = r.SpeakerIndex;
                var spkrtag = Speakers.FirstOrDefault(s => s.SpeakerID == spkridx);
                if (spkrtag != null)
                    r.SpeakerTag = spkrtag;
            }
        }

        /// <summary>
        /// Finds and sets the <see cref="DialogueNodeExtended.Interpdata"/> for each dialogue node in the conversation
        /// </summary>
        /// <remarks>Entry list and reply list should already be populated before calling</remarks>
        private void parseLinesInterpData()
        {
            if (Sequence == null || Sequence.UIndex < 1)
                return;
            //Get sequence from convo
            //Get list of BioConvoStarts
            //Match to export id => SeqAct_Interp => Interpdata
            if (Sequence is ExportEntry sequence)
            {
                var seqobjs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");

                var convStarts = new Dictionary<int, ExportEntry>();
                foreach (var prop in seqobjs)
                {
                    var seqobj = Sequence.FileRef.GetUExport(prop.Value);
                    if (seqobj.ClassName == "BioSeqEvt_ConvNode")
                    {
                        int key = seqobj.GetProperty<IntProperty>("m_nNodeID"); //ME3
                        if (!convStarts.ContainsKey(key))
                        {
                            convStarts.Add(key, seqobj);
                        }
                    }
                }

                foreach (var entry in EntryList)
                {
                    try
                    {
                        entry.Interpdata = ParseSingleNodeInterpData(entry, convStarts);
                    }
                    catch (Exception e)
                    {
#if DEBUG
                        throw new Exception($"EntryList parse interpdata failed: {entry.NodeCount}", e);
#endif
                    }
                }

                foreach (var reply in ReplyList)
                {
                    try
                    {
                        reply.Interpdata = ParseSingleNodeInterpData(reply, convStarts);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"ReplyList parse interpdata failed: {reply.NodeCount}", e);
                    }
                }
            }
        }

        /// <summary>
        /// Sets the FaceFX line names for each dialogue node in the conversation
        /// </summary>
        /// <remarks>Entry list and reply list should already be populated before calling</remarks>
        private void parseLinesFaceFX()
        {
            foreach (var entry in EntryList)
            {
                if (entry.Line != "No data" && !string.IsNullOrWhiteSpace(entry.Line))
                {
                    entry.FaceFX_Female = $"FXA_{entry.LineStrRef}_F";
                    entry.FaceFX_Male = $"FXA_{entry.LineStrRef}_M";
                }
                else
                {
                    entry.FaceFX_Female = "None";
                    entry.FaceFX_Male = "None";
                }
            }

            foreach (var reply in ReplyList)
            {
                if (reply.Line != "No data" && !string.IsNullOrWhiteSpace(reply.Line))
                {
                    reply.FaceFX_Female = $"FXA_{reply.LineStrRef}_F";
                    reply.FaceFX_Male = $"FXA_{reply.LineStrRef}_M";
                }
                else
                {
                    reply.FaceFX_Female = "None";
                    reply.FaceFX_Male = "None";
                }
            }
        }

        /// <summary>
        /// Finds the InterpData export for a given DialogueNode
        /// </summary>
        /// <param name="node">Node to find InterpData for</param>
        /// <param name="convStarts">Dictionary of node <see cref="DialogueNodeExtended.ExportID"/> to SeqEvt_ConvNode exports. If null, this will be calculated.</param>
        /// <returns>InterpData export for node, null if not found</returns>
        public ExportEntry ParseSingleNodeInterpData(DialogueNodeExtended node, Dictionary<int, ExportEntry> convStarts = null)
        {
            if (Sequence == null || node == null || Sequence.UIndex < 1)
                return null;

            if (convStarts == null && Sequence is ExportEntry sequence)
            {
                var seqobjs = sequence.GetProperty<ArrayProperty<ObjectProperty>>("SequenceObjects");
                convStarts = new Dictionary<int, ExportEntry>();
                foreach (var prop in seqobjs)
                {
                    var seqobj = Sequence.FileRef.GetUExport(prop.Value);
                    if (seqobj.ClassName == "BioSeqEvt_ConvNode")
                    {
                        int key = seqobj.GetProperty<IntProperty>("m_nNodeID"); //ME3
                        if (!convStarts.ContainsKey(key))
                        {
                            convStarts.Add(key, seqobj);
                        }
                    }
                }
            }

            //Match to export id => SeqAct_Interp => Interpdata
            node.ExportID = node.NodeProp.GetProp<IntProperty>("nExportID");
            if (node.ExportID != 0 && convStarts != null)
            {
                var convstart = convStarts.FirstOrDefault(s => s.Key == node.ExportID).Value;
                if (convstart != null)
                {
                    // Find the interp data
                    var searchingExports = new List<ExportEntry> {convstart};
                    var seqActInterp = recursiveFindSeqActInterp(searchingExports, new List<ExportEntry>(), 10);

                    var varLinksProp = seqActInterp.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
                    if (varLinksProp != null)
                    {
                        foreach (var prop in varLinksProp)
                        {
                            var desc = prop.GetProp<StrProperty>("LinkDesc").Value; //ME3/ME2/ME1
                            if (desc == "Data") //ME3/ME1
                            {
                                var linkedVars = prop.GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables");
                                if (linkedVars != null && linkedVars.Count > 0)
                                {
                                    var datalink = linkedVars[0].Value;
                                    return Sequence.FileRef.GetUExport(datalink);

                                }
                                break;
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Recursively searches a kismet sequence for an export of class SeqAct_Interp
        /// </summary>
        /// <param name="nodesToSearch">List of nodes to search</param>
        /// <param name="nodesSearched">List of nodes already visited</param>
        /// <param name="searchDepthRemaining">How many layers deep should be searched</param>
        /// <returns>Export of class SeqAct_Interp, or null if not found</returns>
        private ExportEntry recursiveFindSeqActInterp(List<ExportEntry> nodesToSearch, List<ExportEntry> nodesSearched, int searchDepthRemaining)
        {
            if (searchDepthRemaining <= 0)
                return null; // NOT FOUND, NO FURTHER SEARCH

            List<ExportEntry> nextNodesToSearch = new();
            foreach (var searchingExport in nodesToSearch)
            {
                if (nodesSearched.Contains(searchingExport))
                    continue; // Do not enumerate existing items we've found, if there's some sort of loop
                if (searchingExport.ClassName == "SeqAct_Interp")
                {
                    return searchingExport;
                }
                else
                {
                    nodesSearched.Add(searchingExport);
                }

                var outLinks = KismetHelper.GetOutputLinksOfNode(searchingExport);
                foreach(var outbound in outLinks)
                {
                    nextNodesToSearch.AddRange(outbound.Where(x => x.LinkedOp is ExportEntry).Select(x=>x.LinkedOp as ExportEntry));
                }
            }

            return recursiveFindSeqActInterp(nextNodesToSearch, nodesSearched, --searchDepthRemaining);
        }

        /// <summary>
        /// Finds the stream exports and sets the WwiseStream properties for each dialogue node in the conversation
        /// </summary>
        private void parseLinesAudioStreams()
        {
            try
            {
                if (Export.FileRef.Game is not (MEGame.LE1 or MEGame.ME1))
                {
                    Dictionary<string, ExportEntry> streams = Export.FileRef.Exports.Where(x => x.ClassName == "WwiseStream").ToDictionary(x => $"{x.ObjectName.Name.ToLower()}_{x.UIndex}");

                    foreach (var node in EntryList)
                    {
                        string srchFem = $"{node.LineStrRef}_f";
                        string srchM = $"{node.LineStrRef}_m";
                        node.WwiseStream_Female = streams.FirstOrDefault(s => s.Key.Contains(srchFem)).Value;
                        node.WwiseStream_Male = streams.FirstOrDefault(s => s.Key.Contains(srchM)).Value;
                    }

                    foreach (var node in ReplyList)
                    {
                        string srchFem = $"{node.LineStrRef}_f";
                        string srchM = $"{node.LineStrRef}_m";
                        node.WwiseStream_Female = streams.FirstOrDefault(s => s.Key.Contains(srchFem)).Value;
                        node.WwiseStream_Male = streams.FirstOrDefault(s => s.Key.Contains(srchM)).Value;
                    }
                }
            }
            catch (Exception e)
            {
#if DEBUG
                throw new Exception("Failure to parse wwisestreams for lines", e);
#endif
            }
        }

        /// <summary>
        /// Parses each dialogue node's <see cref="DialogueNodeExtended.Script"/> property from the node's struct.
        /// Conversation entries, replies, and scripted events should be populated first
        /// </summary>
        /// <remarks>
        /// Will return early if <see cref="IsFirstParsed"/> is false
        /// </remarks>
        /// <exception cref="Exception">Parse failure on script list</exception>
        private void parseLinesScripts()
        {
            if (IsFirstParsed)
            {
                try
                {
                    foreach (var entry in EntryList)
                    {
                        var scriptidx = entry.NodeProp.GetProp<IntProperty>("nScriptIndex");
                        entry.Script = ScriptList[scriptidx + 1];
                    }
                    foreach (var reply in ReplyList)
                    {
                        var scriptidx = reply.NodeProp.GetProp<IntProperty>("nScriptIndex");
                        reply.Script = ScriptList[scriptidx + 1];
                    }
                }
                catch (Exception e)
                {
#if DEBUG
                    throw new Exception("Parse failure on script list", e);
#endif
                }
            }
        }

        /// <summary>
        /// Parses the <see cref="EntryList"/> collection from the export's m_EntryList property
        /// </summary>
        /// <param name="tlkLookup">Lambda function to use for TLK lookup</param>
        public void ParseEntryList(Func<int, IMEPackage, string> tlkLookup = null)
        {
            EntryList = new ObservableCollectionExtended<DialogueNodeExtended>();
            var entryprop = BioConvo.GetProp<ArrayProperty<StructProperty>>("m_EntryList");
            int cnt = 0;

            foreach (StructProperty node in entryprop)
            {
                EntryList.Add(ParseSingleLine(node, cnt, false, tlkLookup));
                cnt++;
            }
        }

        /// <summary>
        /// Parses the <see cref="ReplyList"/> collection from the export's m_ReplyList property
        /// </summary>
        /// <param name="tlkLookup">Lambda function to use for TLK lookup</param>
        public void ParseReplyList(Func<int, IMEPackage, string> tlkLookup = null)
        {
            ReplyList = new ObservableCollectionExtended<DialogueNodeExtended>();
            var replyprop = BioConvo.GetProp<ArrayProperty<StructProperty>>("m_ReplyList"); //ME3
            if (replyprop != null)
            {
                int cnt = 0;
                foreach (StructProperty node in replyprop)
                {
                    ReplyList.Add(ParseSingleLine(node, cnt, true, tlkLookup));
                    cnt++;
                }
            }
        }

        /// <summary>
        /// Creates a single <see cref="DialogueNodeExtended"/> from a node's StructProperty
        /// </summary>
        /// <param name="node">StructProperty of dialogue node, must be BioDialogReplyNode or BioDialogEntryNode</param>
        /// <param name="count">The array index of this node</param>
        /// <param name="isReply">True if node is reply (player) node, false if entry (non-player) node</param>
        /// <param name="tlkLookupFunc">Lambda function used to perform TLK lookups</param>
        /// <returns>New dialogue node created from the struct values</returns>
        /// <exception cref="Exception">Parse failed, likely on property lookup</exception>
        public DialogueNodeExtended ParseSingleLine(StructProperty node, int count, bool isReply, Func<int, IMEPackage, string> tlkLookupFunc = null)
        {
            int linestrref = 0;
            int spkridx = -2;
            int cond = -1;
            string line = "Unknown Reference";
            int stevent = -1;
            bool bcond = false;
            EReplyTypes eReply = EReplyTypes.REPLY_STANDARD;
            try
            {
                linestrref = node.GetProp<StringRefProperty>("srText")?.Value ?? 0;
                line = tlkLookupFunc?.Invoke(linestrref, Export.FileRef);
                cond = node.GetProp<IntProperty>("nConditionalFunc")?.Value ?? -1;
                stevent = node.GetProp<IntProperty>("nStateTransition")?.Value ?? -1;
                bcond = node.GetProp<BoolProperty>("bFireConditional");
                if (isReply)
                {
                    Enum.TryParse(node.GetProp<EnumProperty>("ReplyType").Value.Name, out eReply);
                }
                else
                {
                    spkridx = node.GetProp<IntProperty>("nSpeakerIndex");
                }

                return new DialogueNodeExtended(node, isReply, count, spkridx, linestrref, line, bcond, cond, stevent, eReply);
            }
            catch (Exception e)
            {
                if (LegendaryExplorerCoreLib.IsDebug)
                {
                    throw new Exception($"List Parse failed: N{count} Reply?:{isReply}, {linestrref}, {line}, {cond}, {stevent}, {bcond}, {eReply}", e);  //Note some convos don't have replies.
                }
                return new DialogueNodeExtended(node, isReply, count, spkridx, linestrref, line, bcond, cond, stevent, eReply);
            }
        }

        /// <summary>
        /// Parses the <see cref="StartingList"/> dictionary from the m_StartingList ArrayProperty in the export
        /// </summary>
        public void ParseStartingList()
        {
            StartingList = new SortedDictionary<int, int>();
            var prop = Export.GetProperty<ArrayProperty<IntProperty>>("m_StartingList"); //ME1/ME2/ME3
            if (prop != null)
            {
                int pos = 0;
                foreach (var sl in prop)
                {
                    StartingList.Add(pos, sl.Value);
                    pos++;
                }
            }
        }

        /// <summary>
        /// Populates the <see cref="Speakers"/> collection from the speaker list ArrayProperty in the export
        /// </summary>
        public void ParseSpeakers()
        {
            Speakers = new ObservableCollectionExtended<SpeakerExtended>
            {
                new SpeakerExtended(-2, "player", null, null, 125303, "\"Shepard\""),
                new SpeakerExtended(-1, "owner", null, null, 0, "No data")
            };
            try
            {
                if (!Export.FileRef.Game.IsGame3())
                {
                    var s_speakers = BioConvo.GetProp<ArrayProperty<StructProperty>>("m_SpeakerList");
                    if (s_speakers != null)
                    {
                        for (int id = 0; id < s_speakers.Count; id++)
                        {
                            var spkr = new SpeakerExtended(id, s_speakers[id].GetProp<NameProperty>("sSpeakerTag").Value);
                            Speakers.Add(spkr);
                        }
                    }
                }
                else
                {
                    var a_speakers = BioConvo.GetProp<ArrayProperty<NameProperty>>("m_aSpeakerList");
                    if (a_speakers != null)
                    {
                        int id = 0;
                        foreach (NameProperty n in a_speakers)
                        {
                            var spkr = new SpeakerExtended(id, n.Value);
                            Speakers.Add(spkr);
                            id++;
                        }
                    }
                }
            }
            catch (Exception e)
            {
#if DEBUG
                throw new Exception("Starting List Parse failed", e);
#endif
            }
        }

        /// <summary>
        /// Populates the <see cref="ScriptList"/> property from the scripted events defined in the export
        /// </summary>
        public void ParseScripts() 
        {
            ScriptList.Add("None");
            if (Export.FileRef.Game.IsGame3())
            {
                var a_scripts = BioConvo.GetProp<ArrayProperty<NameProperty>>("m_aScriptList");
                if (a_scripts != null)
                {
                    foreach (var scriptprop in a_scripts)
                    {
                        var scriptname = scriptprop.Value;
                        ScriptList.Add(scriptname);
                    }
                }
            }
            else
            {
                var a_sscripts = BioConvo.GetProp<ArrayProperty<StructProperty>>("m_ScriptList");
                if (a_sscripts != null)
                {
                    foreach (var scriptprop in a_sscripts)
                    {
                        var s = scriptprop.GetProp<NameProperty>("sScriptTag");
                        ScriptList.Add(s.Value);
                    }
                }
            }
        }

        /// <summary>
        /// Sets this object's <see cref="NonSpkrFFX"/> property based on the contents of the export
        /// </summary>
        public void ParseNSFFX()
        {
            string propname = "m_pNonSpeakerFaceFXSet";
            if (Export.FileRef.Game.IsGame1())
            {
                propname = "m_pConvFaceFXSet";
            }

            var seq = BioConvo.GetProp<ObjectProperty>(propname);
            if (seq != null)
            {
                NonSpkrFFX = Export.FileRef.GetEntry(seq.Value);
            }
            else
            {
                NonSpkrFFX = null;
            }
        }
        /// <summary>
        /// Sets this object's <see cref="WwiseBank"/> property based on the contents of the export
        /// </summary>
        public void ParseWwiseBank()
        {
            WwiseBank = null;
            if (!Export.FileRef.Game.IsGame1())
            {
                try
                {
                    ArrayProperty<ObjectProperty> wwevents;
                    IEntry ffxo = GetFaceFX(-1, true); //find owner animset

                    if (ffxo == null) //if no facefx then maybe soundobject conversation
                    {
                        wwevents = Export.GetProperty<ArrayProperty<ObjectProperty>>("m_aMaleSoundObjects");

                    }
                    else
                    {
                        ExportEntry ffxoExport = (ExportEntry)ffxo;

                        wwevents = ffxoExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues"); //pull an owner wwiseevent array
                        if (wwevents == null || wwevents.Count == 0 || wwevents[0].Value == 0)
                        {
                            IEntry ffxp = GetFaceFX(-2, true); //find player as alternative
                            if (!Export.FileRef.IsUExport(ffxp.UIndex))
                                return;
                            ExportEntry ffxpExport = (ExportEntry)ffxp;
                            wwevents = ffxpExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues");
                        }
                        if (wwevents == null || wwevents.Count == 0 || wwevents[0].Value == 0)
                        {
                            IEntry ffxS = GetFaceFX(0, true); //find speaker 1 as alternative
                            if (ffxS == null || !Export.FileRef.IsUExport(ffxS.UIndex))
                                return;
                            ExportEntry ffxSExport = (ExportEntry)ffxS;
                            wwevents = ffxSExport.GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues");
                        }
                    }

                    if (wwevents == null || wwevents.Count == 0 || wwevents[0].Value == 0)
                    {
                        WwiseBank = null;
                        return;
                    }

                    if (Export.FileRef.Game.IsGame3())
                    {
                        StructProperty r = Export.FileRef.GetUExport(wwevents[0].Value).GetProperty<StructProperty>("Relationships"); //lookup bank
                        var bank = r.GetProp<ObjectProperty>("Bank");
                        WwiseBank = Export.FileRef.GetUExport(bank.Value);
                    }
                    else if (Export.FileRef.Game == MEGame.ME2) //Game is ME2.  Wwisebank ref in Binary.
                    {
                        var wwiseEvent = Export.FileRef.GetUExport(wwevents[0].Value).GetBinaryData<WwiseEvent>();
                        foreach (var link in wwiseEvent.Links)
                        {
                            if (link.WwiseBanks.FirstOrDefault() is int bankIdx and > 0)
                            {
                                WwiseBank = Export.FileRef.GetUExport(bankIdx);
                                break;
                            }
                        }
                    }
                    else if (Export.FileRef.Game is MEGame.LE2)
                    {
                        var r = Export.FileRef.GetUExport(wwevents[0].Value).GetProperty<ArrayProperty<StructProperty>>("References"); //lookup PlatformRelationships
                        var wr = r[0].GetProp<StructProperty>("Relationships");
                        var bank = wr.GetProp<ObjectProperty>("Bank");
                        WwiseBank = Export.FileRef.GetUExport(bank.Value);
                    }
                }
                catch (Exception e)
                {
#if DEBUG
                    throw new Exception($"WwiseBank Parse Failed. {ConvName}", e);
#endif
                }
            }
        }

        /// <summary>
        /// Sets this object's <see cref="Sequence"/> property based on the contents of the export
        /// </summary>
        public void ParseSequence()
        {
            string propname = "MatineeSequence";
            if (Export.FileRef.Game.IsGame1())
            {
                propname = "m_pEvtSystemSeq";
            }

            var seq = BioConvo.GetProp<ObjectProperty>(propname);
            if (seq != null)
            {
                Sequence = Export.FileRef.GetEntry(seq.Value);
                // TODO: Use a packagecache or something for this?
                if (Sequence is ImportEntry sequenceImport)
                {
                    Sequence = EntryImporter.ResolveImport(sequenceImport);
                }
            }
            else
            {
                Sequence = null;
            }
        }

        /// <summary>
        /// Populates the <see cref="StageDirections"/> property from the m_aStageDirections property in the export.
        /// This property only exists in Game 3.
        /// </summary>
        /// <param name="tlkLookup">Lambda function used to perform TLK lookups</param>
        /// <exception cref="Exception">Unable to parse stage direction</exception>
        public void ParseStageDirections(Func<int, IMEPackage, string> tlkLookup = null)
        {
            if (Export.FileRef.Game.IsGame3())
            {
                var dprop = BioConvo.GetProp<ArrayProperty<StructProperty>>("m_aStageDirections"); //ME3 Only not in ME1/2
                if (dprop != null)
                {
                    foreach (var direction in dprop)
                    {
                        int strref = 0;
                        string line = "No data";
                        string action = "None";
                        try
                        {
                            var strrefprop = direction.GetProp<StringRefProperty>("srStrRef");
                            if (strrefprop != null)
                            {
                                strref = strrefprop.Value;
                                line = tlkLookup?.Invoke(strref, Export.FileRef);
                            }
                            var actionprop = direction.GetProp<StrProperty>("sText");
                            if (actionprop != null)
                            {
                                action = actionprop.Value;
                            }
                            StageDirections.Add(new StageDirection(strref, line, action));
                        }
                        catch (Exception e)
                        {
#if DEBUG
                            throw new Exception($"stage directions parse failed {ConvName}", e);
#endif
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Gets the FaceFXAnimset entry from the export for a given speaker ID
        /// </summary>
        /// <param name="speakerID">ID of speaker to get animset for. -1 = Owner, -2 = Player</param>
        /// <param name="isMale">If true, get the male animset, otherwise get the female animset</param>
        public IEntry GetFaceFX(int speakerID, bool isMale = false)
        {
            string ffxPropName = "m_aFemaleFaceSets"; //ME2/M£3
            if (isMale)
            {
                ffxPropName = "m_aMaleFaceSets";
            }
            var ffxList = BioConvo.GetProp<ArrayProperty<ObjectProperty>>(ffxPropName);
            if (ffxList != null && ffxList.Count > speakerID + 2)
            {
                return Export.FileRef.GetEntry(ffxList[speakerID + 2].Value);
            }
            else
            {
                if (!Export.Game.IsGame3() || !Export.ObjectNameString.EndsWith("_dlg", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                // Some conversations in Game3 don't have the m_aFaceSets properties. This is a workaround.
                var fxaName =
                    $"FXA_{Export.ObjectNameString[..^4]}_{Speakers[speakerID + 2].SpeakerName}_{(isMale ? 'M' : 'F')}";
                foreach (var entry in Export.FileRef.Exports)
                {
                    if (string.Equals(entry.ObjectName, fxaName, StringComparison.OrdinalIgnoreCase))
                    {
                        return entry;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Serializes the <see cref="StartingList"/>, <see cref="EntryList"/>, <see cref="ReplyList"/>, and <see cref="Speakers"/> list
        /// to a StructProperty, optionally written to the conversation export
        /// </summary>
        /// <param name="commitToExport">If true, serialized StructProperty will be written to the export</param>
        public void SerializeNodes(bool commitToExport = true)
        {
            AutoGenerateSpeakerArrays();
            var newstartlist = new ArrayProperty<IntProperty>("m_StartingList");
            foreach ((var _, int value) in StartingList)
            {
                newstartlist.Add(value);
            }

            var newentryList = new ArrayProperty<StructProperty>("m_EntryList");
            foreach (var entry in EntryList.OrderBy(entry => entry.NodeCount))
            {
                newentryList.Add(entry.NodeProp);
            }
            var newreplyList = new ArrayProperty<StructProperty>("m_ReplyList");
            foreach (var reply in ReplyList.OrderBy(reply => reply.NodeCount))
            {
                newreplyList.Add(reply.NodeProp);
            }


            if (Export.Game.IsGame3())
            {
                var newSpeakerList = new ArrayProperty<NameProperty>( "m_aSpeakerList");
                foreach (var speaker in Speakers.OrderBy(x => x.SpeakerID))
                {
                    if (speaker.SpeakerID < 0)
                        continue; // They don't belong here
                    newSpeakerList.Add(new NameProperty(speaker.SpeakerNameRef));
                }
                BioConvo.AddOrReplaceProp(newSpeakerList);
            }
            else
            {
                var newSpeakerList = new ArrayProperty<StructProperty>("m_SpeakerList");
                foreach (var speaker in Speakers.OrderBy(x => x.SpeakerID))
                {
                    if (speaker.SpeakerID < 0)
                        continue; // They don't belong here
                    PropertyCollection ssProps = new PropertyCollection();
                    ssProps.Add(new NameProperty(speaker.SpeakerNameRef, "sSpeakerTag"));
                    var speakerStruct = new StructProperty("BioDialogSpeaker", ssProps);
                    newSpeakerList.Add(speakerStruct);
                }

                if (newSpeakerList.Count > 0)
                {
                    BioConvo.AddOrReplaceProp(newSpeakerList);
                }
                else
                {
                    BioConvo.RemoveNamedProperty(newSpeakerList.Name); // This ensures this property is removed so it reserializes the same as vanilla
                }
            }


            if (newstartlist.Count > 0)
            {
                BioConvo.AddOrReplaceProp(newstartlist);
            }

            if (newentryList.Count > 0)
            {
                BioConvo.AddOrReplaceProp(newentryList);
            }

            if (newreplyList.Count > 0)
            {
                BioConvo.AddOrReplaceProp(newreplyList);
            }

            if (commitToExport)
                Export.WriteProperties(BioConvo);
        }

        /// <summary>
        /// Traverses the conversation graph, setting the aSpeakerList ArrayProperty for each starting node
        /// </summary>
        /// <remarks>
        /// Properties are set in the node's <see cref="DialogueNodeExtended.NodeProp"/>
        /// </remarks>
        /// <returns>True if conversation has looping paths, false otherwise</returns>
        public bool AutoGenerateSpeakerArrays()
        {
            bool hasLoopingPaths = false;

            // Set blank speakers/listeners
            var blankaSpkr = new ArrayProperty<IntProperty>("aSpeakerList");
            foreach (var dnode in EntryList)
            {
                dnode.NodeProp.Properties.AddOrReplaceProp(blankaSpkr);
            }

            // Traverse conversation graph
            foreach (int entryIndex in StartingList.Values)
            {
                var aSpkrs = new SortedSet<int>();
                var startNode = EntryList[entryIndex];
                var visitedNodes = new HashSet<DialogueNodeExtended>();
                var newNodes = new Queue<DialogueNodeExtended>();
                aSpkrs.Add(startNode.SpeakerIndex);
                var startprop = startNode.NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                foreach (var e in startprop)
                {
                    var lprop = e.GetProp<IntProperty>("nIndex");
                    newNodes.Enqueue(ReplyList[lprop.Value]);

                }
                visitedNodes.Add(startNode);
                while (newNodes.Any())
                {
                    var thisnode = newNodes.Dequeue();
                    if (!visitedNodes.Contains(thisnode))
                    {
                        if (thisnode.IsReply)
                        {
                            var thisprop = thisnode.NodeProp.GetProp<ArrayProperty<IntProperty>>("EntryList");
                            if (thisprop != null)
                            {
                                foreach (var r in thisprop)
                                {
                                    newNodes.Enqueue(EntryList[r.Value]);
                                }
                            }
                        }
                        else
                        {
                            aSpkrs.Add(thisnode.SpeakerIndex);
                            var thisprop = thisnode.NodeProp.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                            foreach (var e in thisprop)
                            {
                                var eprop = e.GetProp<IntProperty>("nIndex");
                                newNodes.Enqueue(ReplyList[eprop.Value]);

                            }
                        }
                        visitedNodes.Add(thisnode);
                    }
                    else { hasLoopingPaths = true; }
                }
                var newaSpkr = new ArrayProperty<IntProperty>("aSpeakerList");
                foreach (var a in aSpkrs)
                {
                    newaSpkr.Add(a);
                }
                startNode.NodeProp.Properties.AddOrReplaceProp(newaSpkr);
            }
            return hasLoopingPaths;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}