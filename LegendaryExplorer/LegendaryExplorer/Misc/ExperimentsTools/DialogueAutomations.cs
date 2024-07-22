using ICSharpCode.AvalonEdit.Rendering;
using LegendaryExplorer.DialogueEditor.DialogueEditorExperiments;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorer.UserControls.ExportLoaderControls;
using LegendaryExplorer.UserControls.SharedToolControls.Curves;
using LegendaryExplorerCore.Dialogue;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Matinee;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Xml.Linq;

using static LegendaryExplorer.Misc.ExperimentsTools.SharedMethods;

namespace LegendaryExplorer.Misc.ExperimentsTools
{
    public static class DialogueAutomations
    {
        /// <summary>
        /// Replace the line of a conversation node, along with its asociated audio, if passed.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="node">Dialogue node to operate on.</param>
        /// <param name="newTlkID">New TLK to replace.</param>
        /// <param name="audioInfo">Audio info to replace with.</param>
        /// <param name="FXAControl_F">Female FaceFX controls, with a loaded export.</param>
        /// <param name="FXAControl_M">Male FaceFX controls, with a loaded export.</param>
        public static void ReplaceLineAndAudio(IMEPackage pcc, DialogueNodeExtended node, string newTlkID,
            LineAudioInfo audioInfo, FaceFXAnimSetEditorControl FXAControl_F, FaceFXAnimSetEditorControl FXAControl_M)
        {
            FXAControl_F.SelectLineByName(node.FaceFX_Female ?? "");
            FXAControl_M.SelectLineByName(node.FaceFX_Male ?? "");
            string currTlkID = node.LineStrRef.ToString();

            ExportEntry femaleEvent = DialogueEditorExperimentsE.GetWwiseEvent(pcc, FXAControl_F.SelectedLine);
            ExportEntry maleEvent = DialogueEditorExperimentsE.GetWwiseEvent(pcc, FXAControl_M.SelectedLine);
            ExportEntry femaleStream = DialogueEditorExperimentsE.GetWwiseStream(pcc, femaleEvent, currTlkID, "_f_");
            ExportEntry maleStream = DialogueEditorExperimentsE.GetWwiseStream(pcc, maleEvent, currTlkID, "_m_");

            DialogueEditorExperimentsE.UpdateWwiseEvent(pcc, femaleEvent, currTlkID, newTlkID);
            DialogueEditorExperimentsE.UpdateWwiseEvent(pcc, maleEvent, currTlkID, newTlkID);

            DialogueEditorExperimentsE.UpdateWwiseStream(femaleStream, currTlkID, newTlkID);
            DialogueEditorExperimentsE.UpdateWwiseStream(maleStream, currTlkID, newTlkID);

            DialogueEditorExperimentsE.UpdateFaceFX(FXAControl_F, currTlkID, newTlkID);
            DialogueEditorExperimentsE.UpdateFaceFX(FXAControl_M, currTlkID, newTlkID);

            node.NodeProp.Properties.AddOrReplaceProp(new StringRefProperty(int.Parse(newTlkID), "srText"));

            if (FXAControl_F.SelectedLine != null && audioInfo != null) { ReplaceAudioInfo(femaleStream, audioInfo, true); }
            if (FXAControl_M.SelectedLine != null && audioInfo != null) { ReplaceAudioInfo(maleStream, audioInfo, false); }
        }

        /// <summary>
        /// Replaces the audio infomation of the WwiseStream.
        /// </summary>
        /// <param name="wwiseStream">WwiseStream to operate on.</param>
        /// <param name="audioInfo">Information to use to replace.</param>
        /// <param name="isFemale">Whether to use the female or male info.</param>
        public static void ReplaceAudioInfo(ExportEntry wwiseStream, LineAudioInfo audioInfo, bool isFemale)
        {
            if (wwiseStream == null) { return; }

            PropertyCollection props = wwiseStream.GetProperties();
            props.AddOrReplaceProp(new NameProperty(isFemale ? audioInfo.Filename_F : audioInfo.Filename_M, "Filename"));
            WwiseStream binary = wwiseStream.GetBinaryData<WwiseStream>();
            binary.DataSize = isFemale ? audioInfo.SizeOnDisk_F : audioInfo.SizeOnDisk_M;
            wwiseStream.WritePropertiesAndBinary(props, binary);

            byte[] unparsedBinary = wwiseStream.GetBinaryData();
            Span<byte> dataOffsetSpan = unparsedBinary.AsSpan(^4..);
            HexToBytes(isFemale ? audioInfo.OffsetInFile_F : audioInfo.OffsetInFile_M).CopyTo(dataOffsetSpan);
            wwiseStream.WriteBinary(unparsedBinary);
        }

        /// <summary>
        /// Replace the line of a conversation node, along with its asociated audio, and the FaceFX.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="node">Dialogue node to operate on.</param>
        /// <param name="tlkID">New TLK to replace.</param>
        /// <param name="lineAudioInfo">Line Audio Info to use.</param>
        /// <param name="FXA_F_Control">Female FaceFX controls, with a loaded export.</param>
        /// <param name="FXA_M_Control">Male FaceFX controls, with a loaded export.</param>
        public static void ReplaceLineAndAudioAndFXA(IMEPackage pcc, DialogueNodeExtended node, string tlkID, LineAudioInfo lineAudioInfo, FaceFXAnimSetEditorControl FXA_F_Control, FaceFXAnimSetEditorControl FXA_M_Control)
        {
            ReplaceLineAndAudio(pcc, node, tlkID, lineAudioInfo, FXA_F_Control, FXA_M_Control);
            // Update FaceFX and timings
            FXA_F_Control.SelectLineByName(node.Line);
            FXA_M_Control.SelectLineByName(node.Line);

            if (lineAudioInfo.XMLUri_F.EndsWith(".xml")) { ReplaceAnimationFromXml(FXA_F_Control, lineAudioInfo.XMLUri_F); }
            else { ReplaceAnimationFromJson(FXA_F_Control, lineAudioInfo.XMLUri_F); }

            if (lineAudioInfo.XMLUri_M.EndsWith(".xml")) { ReplaceAnimationFromXml(FXA_M_Control, lineAudioInfo.XMLUri_M); }
            else { ReplaceAnimationFromJson(FXA_M_Control, lineAudioInfo.XMLUri_M); }
        }


        /// <summary>
        /// Gets the dialogue node that matches the given export ID.
        /// </summary>
        /// <param name="conversation">Loaded conversation to find the node in.</param>
        /// <param name="exportID">Export ID of the node to find.</param>
        /// <returns>Dialogue node.</returns>
        public static DialogueNodeExtended GetNode(ConversationExtended conversation, int exportID)
        {
            ObservableCollection<DialogueNodeExtended> nodes = conversation.EntryList;
            nodes.AddRange(conversation.ReplyList);
            DialogueNodeExtended node = nodes.FirstOrDefault(node => node.ExportID == exportID);
            return node;
        }

        /// <summary>
        /// Gets the dialogue nodes that match the given export IDs.
        /// </summary>
        /// <param name="conversation">Loaded conversation to find the node in.</param>
        /// <param name="exportID">Export IDs of the nodes to find.</param>
        /// <returns>Dialogue nodes.</returns>
        public static IEnumerable<DialogueNodeExtended> GetNodes(ConversationExtended conversation, int[] exportIDs)
            => exportIDs.Select(id => GetNode(conversation, id));

        /// <summary>
        /// Get a node from a conversation by its index.
        /// </summary>
        /// <param name="conversation">Conversation to get the node from.</param>
        /// <param name="index">Node's index.</param>
        /// <param name="isReply">Whether the node is a reply or an entry node.</param>
        /// <returns>Dialogue node.</returns>
        public static DialogueNodeExtended GetNodeByIndex(ConversationExtended conversation, int index, bool isReply)
            => isReply ? conversation.ReplyList[index] : conversation.EntryList[index];

        /// <summary>
        /// Get nodes from a conversation by their index.
        /// MUST be of the same type, either Reply or Entry.
        /// </summary>
        /// <param name="conversation">Conversation to get the nodes from.</param>
        /// <param name="areReply">Whether the nodes are a reply or an entry node.</param>
        /// <param name="indexex">Nodes' index.</param>
        /// <returns>Dialogue nodes.</returns>
        public static IEnumerable<DialogueNodeExtended> GetNodesByIndex(ConversationExtended conversation, bool areReply, int[] indexes)
            => indexes.Select(index => areReply ? conversation.ReplyList[index] : conversation.EntryList[index]);

        /// <summary>
        /// Writes a DialogueNodeExtended to a conversation.
        /// </summary>
        /// <param name="node">Node to write.</param>
        /// <param name="conversation">Conversation to write the node to.</param>
        public static void WriteNode(DialogueNodeExtended node, ExportEntry conversation)
        {
            if (node.IsReply)
            {
                ArrayProperty<StructProperty> m_ReplyList = conversation.GetProperty<ArrayProperty<StructProperty>>("m_ReplyList");
                m_ReplyList.ReplaceFirstOrAdd(el => el.GetProp<IntProperty>("nExportID").Value == node.ExportID, node.NodeProp);
                conversation.WriteProperty(m_ReplyList);
            }
            else
            {
                ArrayProperty<StructProperty> m_EntryList = conversation.GetProperty<ArrayProperty<StructProperty>>("m_EntryList");
                m_EntryList.ReplaceFirstOrAdd(el => el.GetProp<IntProperty>("nExportID").Value == node.ExportID, node.NodeProp);
                conversation.WriteProperty(m_EntryList);
            }
        }

        /// <summary>
        /// Writes multiple DialogueNodeExtended to a conversation.
        /// </summary>
        /// <param name="conversation">Conversation to write the node to.</param>
        /// <param name="nodes">Nodes to write to the conversation.</param>
        public static void WriteNodes(ExportEntry conversation, params DialogueNodeExtended[] nodes)
        {
            foreach (DialogueNodeExtended node in nodes)
            {
                WriteNode(node, conversation);
            }
        }

        /// <summary>
        /// Creates a new ConversationExtended and loads it.
        /// </summary>
        /// <param name="conv">Conversation to generate the extended from.</param>
        /// <returns>Loaded ConversationExtended.</returns>
        public static ConversationExtended GetLoadedConversation(ExportEntry conv)
        {
            ConversationExtended loadedConv = new(conv);
            loadedConv.LoadConversation(TLKManagerWPF.GlobalFindStrRefbyID, true);
            return loadedConv;
        }

        /// <summary>
        /// Gets an FXA Control and loads the given export.
        /// </summary>
        /// <param name="pcc">Pcc to operate on.</param>
        /// <param name="UExport">Export to load.</param>
        /// <returns>Loaded FaceFXAnimSetEditorControl.</returns>
        public static FaceFXAnimSetEditorControl GetLoadedFXAControl(IMEPackage pcc, int UExport)
        {
            FaceFXAnimSetEditorControl FXA_Control = new();
            FXA_Control.LoadExport(pcc.GetUExport(UExport));
            return FXA_Control;
        }

        /// <summary>
        /// Clears the _m animations for the FXA control.
        /// </summary>
        /// <param name="control">FaceFX animation set editor control.</param>
        public static void ClearLipSyncKeys(FaceFXAnimSetEditorControl control)
        {
            foreach (var anim in control.Animations)
            {
                if (anim.Name.StartsWith("m_"))
                {
                    anim.Points = new LinkedList<CurvePoint>();
                }
            }
            SaveChanges(control);
        }

        /// <summary>
        /// Saves the changes to the FXA control.
        /// </summary>
        /// <param name="control">FaceFX animation set editor control.</param>
        public static void SaveChanges(FaceFXAnimSetEditorControl control)
        {
            if (control.SelectedLine != null)
            {
                var curvePoints = new List<CurvePoint>();
                var numKeys = new List<int>();
                var animationNames = new List<int>();
                foreach (Animation anim in control.Animations)
                {
                    animationNames.Add(control.FaceFX.Names.FindOrAdd(anim.Name));
                    curvePoints.AddRange(anim.Points);
                    numKeys.Add(anim.Points.Count);
                }
                control.SelectedLine.AnimationNames = animationNames;
                control.SelectedLine.Points = curvePoints.Select(x => new FaceFXControlPoint
                {
                    time = x.InVal,
                    weight = x.OutVal,
                    inTangent = x.ArriveTangent,
                    leaveTangent = x.LeaveTangent
                }).ToList();
                control.SelectedLine.NumKeys = numKeys;
                control.SelectedLineEntry.UpdateLength();
            }
            control.CurrentLoadedExport?.WriteBinary(control.FaceFX.Binary);
        }

        /// <summary>
        /// Replaces a FaceFX animation with the values from an xml file.
        /// </summary>
        /// <param name="control">Loaded FaceFX controls.</param>
        /// <param name="xmlUri">Path to the XML file.</param>
        /// <param name="chosenAnimName">In the case of multiple animations in the file, the name of the one to use.</param>
        public static void ReplaceAnimationFromXml(FaceFXAnimSetEditorControl control, string xmlUri, string chosenAnimName = null)
        {
            #region xml import
            XElement xmlDoc = XElement.Load(xmlUri);
            List<XElement> animations = xmlDoc.Descendants("animation_groups").Descendants("animation_group").Descendants("animation").ToList();
            XElement animationElement;
            if (animations.Count == 0)
            {
                return;
            }
            if (animations.Count > 1)
            {
                List<string> animNames = animations.Select((x, i) => x.Attribute("name")?.Value ?? i.ToString()).ToList();
                if (chosenAnimName == null) { return; }
                animationElement = animations.Find(x => x.Attribute("name")?.Value == chosenAnimName);
            }
            else
            {
                animationElement = animations[0];
            }
            IEnumerable<XElement> curveNodes = animationElement.Descendants("curves").Descendants();
            FaceFXAnimSetEditorControl.LineSection lineSec = new() { animSecs = new Dictionary<string, List<FaceFXControlPoint>>() };
            float firstTime = float.MaxValue;
            float lastTime = float.MinValue;
            foreach (XElement curveNode in curveNodes)
            {
                string curveName = curveNode.Attribute("name")?.Value;
                if (curveName is null)
                {
                    continue;
                }
                if (curveNode.Value is string value)
                {
                    float[] keys = value.Trim().Split(' ').Select(s =>
                    {
                        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
                        {
                            return result;
                        }
                        return 0f;
                    }).ToArray();
                    List<FaceFXControlPoint> points = new();
                    for (int i = 0; i + 3 < keys.Length; i += 4)
                    {
                        firstTime = MathF.Min(firstTime, keys[i]);
                        lastTime = MathF.Max(firstTime, keys[i]);
                        points.Add(new FaceFXControlPoint
                        {
                            time = keys[i],
                            weight = keys[i + 1],
                            inTangent = keys[i + 2],
                            leaveTangent = keys[i + 3]
                        });
                    }
                    lineSec.animSecs.Add(curveName, points);
                }
            }
            lineSec.span = MathF.Max(0, lastTime - firstTime);

            #endregion

            var newPoints = new List<FaceFXControlPoint>();
            for (int i = 0, j = 0; i < control.SelectedLine.AnimationNames.Count; i++)
            {
                int newNumPoints = 0;
                string animName = control.FaceFX.Names[control.SelectedLine.AnimationNames[i]];
                if (lineSec.animSecs.TryGetValue(animName, out List<FaceFXControlPoint> points))
                {
                    newPoints.AddRange(points);
                    newNumPoints += points.Count;
                    lineSec.animSecs.Remove(animName);
                }
                else
                {
                    for (int k = 0; k < control.SelectedLine.NumKeys[i]; k++)
                    {
                        newPoints.Add(control.SelectedLine.Points[j + k]);
                        newNumPoints++;
                    }
                }
                j += control.SelectedLine.NumKeys[i];
                control.SelectedLine.NumKeys[i] = newNumPoints;
            }
            //add new animations
            if (lineSec.animSecs.Count > 0)
            {
                foreach ((string name, List<FaceFXControlPoint> points) in lineSec.animSecs)
                {
                    control.SelectedLine.AnimationNames.Add(control.FaceFX.Names.FindOrAdd(name));
                    control.SelectedLine.NumKeys.Add(points.Count);
                    newPoints.AddRange(points);
                }
            }
            control.SelectedLineEntry.Points = newPoints;
            control.CurrentLoadedExport?.WriteBinary(control.FaceFX.Binary);
        }
        /// <summary>
        /// Replaces a FaceFX animation with the values from a json file.
        /// </summary>
        /// <param name="control">Loaded FaceFX controls.</param>
        /// <param name="jsonFile">Path to the json file.</param>
        /// <param name="chosenAnimName">In the case of multiple animations in the file, the name of the one to use.</param>
        public static void ReplaceAnimationFromJson(FaceFXAnimSetEditorControl control, string jsonFile)
        {
            float start = -1;
            var lineSec = JsonConvert.DeserializeObject<FaceFXAnimSetEditorControl.LineSection>(File.ReadAllText(jsonFile));

            ClearLipSyncKeys(control);

            // Insert animation from json
            var newPoints = new List<FaceFXControlPoint>();
            for (int i = 0, j = 0; i < control.SelectedLine.AnimationNames.Count; i++)
            {
                int k = 0;
                int newNumPoints = 0;
                FaceFXControlPoint tmp;
                for (; k < control.SelectedLine.NumKeys[i]; k++)
                {
                    tmp = control.SelectedLine.Points[j + k];
                    if (tmp.time >= start)
                    {
                        break;
                    }
                    newPoints.Add(tmp);
                    newNumPoints++;
                }
                string animName = control.FaceFX.Names[control.SelectedLine.AnimationNames[i]];
                if (lineSec.animSecs.TryGetValue(animName, out List<FaceFXControlPoint> points))
                {
                    newPoints.AddRange(points.Select(p => { p.time += start; return p; }));
                    newNumPoints += points.Count;
                    lineSec.animSecs.Remove(animName);
                }
                for (; k < control.SelectedLine.NumKeys[i]; k++)
                {
                    tmp = control.SelectedLine.Points[j + k];
                    tmp.time += lineSec.span;
                    newPoints.Add(tmp);
                    newNumPoints++;
                }
                j += control.SelectedLine.NumKeys[i];
                control.SelectedLine.NumKeys[i] = newNumPoints;
            }
            //if the line we are importing from had more animations than this one, we need to add some animations
            if (lineSec.animSecs.Count > 0)
            {
                foreach ((string name, List<FaceFXControlPoint> points) in lineSec.animSecs)
                {
                    control.SelectedLine.AnimationNames.Add(control.FaceFX.Names.FindOrAdd(name));
                    control.SelectedLine.NumKeys.Add(points.Count);
                    newPoints.AddRange(points.Select(p => { p.time += start; return p; }));
                }
            }

            control.SelectedLineEntry.Points = newPoints;
            control.CurrentLoadedExport?.WriteBinary(control.FaceFX.Binary);
        }

        /// <summary>
        /// Update the InterpLength of the InterpData associated with the given node.
        /// </summary>
        /// <param name="node">Node to get the InterpData from.</param>
        /// <param name="length">New length.</param>
        public static void UpdateNodeLength(DialogueNodeExtended node, float length)
        {
            ExportEntry interpData = node.Interpdata;
            interpData.WriteProperty(new FloatProperty(length, "InterpLength"));
        }

        /// <summary>
        /// Change the entry/reply an reply/entry points to.
        /// Does not write it to the conversation export.
        /// </summary>
        /// <param name="game">Game type.</param>
        /// <param name="node">Node to relink.</param>
        /// <param name="currIdx">Index of the reply/entry the node points to.</param>
        /// <param name="targetIdx">Index of the reply/entry to target.</param>
        /// <param name="sParphrase">String paraphrase, for entries.</param>
        /// <param name="srParaphrase">Ref of string paraphrase, for entries.</param>
        /// <param name="replyCategory">Type of reply, for entries.</param>
        public static void ChangeNodeLink(MEGame game, DialogueNodeExtended node, int currIdx, int targetIdx,
            string sParaphrase = "", int srParaphrase = 0, EReplyCategory replyCategory = EReplyCategory.REPLY_CATEGORY_DEFAULT)
        {
            StructProperty props = node.NodeProp;
            if (node.IsReply)
            {
                ArrayProperty<IntProperty> entryList = props.GetProp<ArrayProperty<IntProperty>>("EntryList");
                foreach (IntProperty entry in entryList)
                {
                    if (entry.Value == currIdx)
                    {
                        entry.Value = targetIdx;
                        break;
                    }
                }
            }
            else
            {
                ArrayProperty<StructProperty> replyListNew = props.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                foreach (StructProperty reply in replyListNew)
                {
                    if (reply.GetProp<IntProperty>("nIndex").Value == currIdx)
                    {
                        PropertyCollection replyProps = reply.Properties;
                        replyProps.AddOrReplaceProp(new StrProperty(sParaphrase, "sParaphrase"));
                        replyProps.AddOrReplaceProp(new IntProperty(targetIdx, "nIndex"));
                        replyProps.AddOrReplaceProp(new StringRefProperty(srParaphrase, "srParaphrase"));
                        replyProps.AddOrReplaceProp(new EnumProperty(replyCategory.ToString(), "EReplyCategory", game, "Category"));
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Add a new link to an entry/reply to the node.
        /// Does not write it to the conversation export.
        /// </summary>
        /// <param name="game">Game type.</param>
        /// <param name="node">Node to add a new entry/reply to..</param>
        /// <param name="newIdx">Index of the reply/entry to point to.</param>
        /// <param name="sParphrase">String paraphrase, for entries.</param>
        /// <param name="srParaphrase">Ref of string paraphrase, for entries.</param>
        /// <param name="replyCategory">Type of reply, for entries.</param>
        public static void AddNodeLink(MEGame game, DialogueNodeExtended node, int newIdx,
            string sParphrase = "", int srParaphrase = 0, EReplyCategory replyCategory = EReplyCategory.REPLY_CATEGORY_DEFAULT)
        {
            StructProperty props = node.NodeProp;
            if (node.IsReply)
            {
                ArrayProperty<IntProperty> entryList = props.GetProp<ArrayProperty<IntProperty>>("EntryList");
                entryList.Add(new IntProperty(newIdx));
            }
            else
            {
                ArrayProperty<StructProperty> replyListNew = props.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                replyListNew.Add(new StructProperty("BioDialogEntryNode", new PropertyCollection()
                {
                    new StrProperty(sParphrase, "sParaphrase"),
                    new IntProperty(newIdx, "nIndex"),
                    new StringRefProperty(srParaphrase, "srParaphrase"),
                    new EnumProperty(replyCategory.ToString(), game, "Category")
                }));
            }
        }

        /// <summary>
        /// Remove the target entry/reply from the node.
        /// Does not write it to the conversation export.
        /// </summary>
        /// <param name="node">Node to operate on..</param>
        /// <param name="targetIdx">Index of the reply/entry to remove.</param>
        public static void RemoveNodeLink(DialogueNodeExtended node, int targetIdx)
        {
            StructProperty props = node.NodeProp;
            if (node.IsReply)
            {
                ArrayProperty<IntProperty> entryList = props.GetProp<ArrayProperty<IntProperty>>("EntryList");
                entryList.TryRemove(entry => entry.Value == targetIdx, out _);
            }
            else
            {
                ArrayProperty<StructProperty> replyListNew = props.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                replyListNew.TryRemove(reply => reply.GetProp<IntProperty>("nIndex").Value == targetIdx, out _);
            }
        }

        /// <summary>
        /// Writes the given Conditional or Boolean and the Parameter to the node.
        /// Does not write it to the conversation export.
        /// </summary>
        /// <param name="node">Node to operate on..</param>
        /// <param name="firesCond">True if the plot is a conditional, false if a boolean.</param>
        /// <param name="condOrBool">Conditional or Boolean to set.</param>
        /// <param name="condParam">Parameter to set.</param>
        public static void WriteNodePlotCheck(DialogueNodeExtended node, bool firesCond, int condOrBool, int condParam)
        {
            StructProperty props = node.NodeProp;

            BoolProperty bFireConditional = props.GetProp<BoolProperty>("bFireConditional");
            bFireConditional.Value = firesCond;
            IntProperty nConditionalFunc = props.GetProp<IntProperty>("nConditionalFunc");
            nConditionalFunc.Value = condOrBool;
            IntProperty nConditionalParam = props.GetProp<IntProperty>("nConditionalParam");
            nConditionalParam.Value = condParam;
        }

        /// <summary>
        /// Swaps the plot checks of two nodes.
        /// Does not write it to the conversation export.
        /// </summary>
        /// <param name="node1">Node 1 to swap.</param>
        /// <param name="node2">Node 2 to swap.</param>
        public static void SwapNodesPlotChecks(DialogueNodeExtended node1, DialogueNodeExtended node2)
        {
            bool node1FiresConditional = node1.FiresConditional;
            int node1CondOrBool = node1.ConditionalOrBool;
            int node1CondParam = node1.ConditionalParam;
            bool node2FiresConditional = node2.FiresConditional;
            int node2CondOrBool = node2.ConditionalOrBool;
            int node2CondParam = node2.ConditionalParam;

            WriteNodePlotCheck(node1, node2FiresConditional, node2CondOrBool, node2CondParam);
            WriteNodePlotCheck(node2, node1FiresConditional, node1CondOrBool, node1CondParam);
        }

        /// <summary>
        /// Swaps the plot checks of two nodes.
        /// DOES WRITE them to the conversation export.
        /// </summary>
        /// <param name="node1">Node 1 to swap.</param>
        /// <param name="node2">Node 2 to swap.</param>
        public static void SwapAndWriteNodesPlotChecks(ExportEntry exp, DialogueNodeExtended node1, DialogueNodeExtended node2)
        {
            SwapNodesPlotChecks(node1, node2);
            WriteNodes(exp, node1, node2);
        }

        /// <summary>
        /// Swaps the order of the first two entries or replies of the given node
        /// Does not write it to the conversation export.
        /// </summary>
        /// <param name="conversation">Conversation to find nodes in.</param>
        /// <param name="node">Node to swap nodes of.</param>
        /// <returns>The swapped nodes.</returns>
        public static (DialogueNodeExtended, DialogueNodeExtended) SwapFirstTwoNodes(ConversationExtended conversation, DialogueNodeExtended node)
        {
            DialogueNodeExtended node1;
            DialogueNodeExtended node2;

            StructProperty props = node.NodeProp;
            if (node.IsReply)
            {
                ArrayProperty<IntProperty> entryList = props.GetProp<ArrayProperty<IntProperty>>("EntryList");
                (entryList[1], entryList[0]) = (entryList[0], entryList[1]);
                node1 = GetNodeByIndex(conversation, entryList[0], false);
                node2 = GetNodeByIndex(conversation, entryList[1], false);
            }
            else
            {
                ArrayProperty<StructProperty> replyList = props.GetProp<ArrayProperty<StructProperty>>("ReplyListNew");
                (replyList[1], replyList[0]) = (replyList[0], replyList[1]);
                node1 = GetNodeByIndex(conversation, replyList[0].GetProp<IntProperty>("nIndex").Value, true);
                node2 = GetNodeByIndex(conversation, replyList[1].GetProp<IntProperty>("nIndex").Value, true);
            }

            return (node1, node2);
        }

        /// <summary>
        /// Swaps the first two nodes that the node links to, and swaps their plot values.
        /// DOES WRITE the node and the first two nodes to the exp.
        /// </summary>
        /// <param name="exp">Conversation export to write to.</param>
        /// <param name="conv">Loaded conversation to operate from.</param>
        /// <param name="node">Source node to get children from.</param>
        public static void SwapAndWriteFirstTwoNodesAndPlots(ExportEntry exp, ConversationExtended conv, DialogueNodeExtended node)
        {
            (DialogueNodeExtended node1, DialogueNodeExtended node2) = SwapFirstTwoNodes(conv, node);
            SwapNodesPlotChecks(node1, node2);
            WriteNodes(exp, node, node1, node2);
        }

        /// <summary>
        /// Swaps the first two nodes that the node links to.
        /// DOES WRITE the node and the first two nodes to the exp.
        /// </summary>
        /// <param name="exp">Conversation export to write to.</param>
        /// <param name="conv">Loaded conversation to operate from.</param>
        /// <param name="node">Source node to get children from.</param>
        public static void SwapAndWriteFirstTwoNodes(ExportEntry exp, ConversationExtended conv, DialogueNodeExtended node)
        {
            SwapFirstTwoNodes(conv, node);
            WriteNode(node, exp);
        }

        /// <summary>
        /// Swaps the first two nodes that the nodes link to, and swaps their plot values.
        /// DOES WRITE the nodes and the first two nodes to the exp.
        /// </summary>
        /// <param name="exp">Conversation export to write to.</param>
        /// <param name="conv">Loaded conversation to operate from.</param>
        /// <param name="node">Source node to get children from.</param>
        public static void BatchSwapAndWriteFirstTwoNodesAndPlots(ExportEntry exp, ConversationExtended conv, params DialogueNodeExtended[] nodes)
        {
            BatchSwapAndWriteFirstTwoNodesAndPlots(exp, conv, nodes);
        }
        public static void BatchSwapAndWriteFirstTwoNodesAndPlots(ExportEntry exp, ConversationExtended conv, IEnumerable<DialogueNodeExtended> nodes)
        {
            foreach (DialogueNodeExtended node in nodes)
            {
                SwapAndWriteFirstTwoNodesAndPlots(exp, conv, node);
            }
        }

        /// <summary>
        /// Swaps the first two nodes that the nodes link to.
        /// DOES WRITE the nodes and the first two nodes to the exp.
        /// </summary>
        /// <param name="exp">Conversation export to write to.</param>
        /// <param name="conv">Loaded conversation to operate from.</param>
        /// <param name="node">Source node to get children from.</param>
        public static void BatchSwapAndWriteFirstTwoNodes(ExportEntry exp, ConversationExtended conv, params DialogueNodeExtended[] nodes)
        {
            BatchSwapAndWriteFirstTwoNodes(exp, conv, nodes);
        }
        public static void BatchSwapAndWriteFirstTwoNodes(ExportEntry exp, ConversationExtended conv, IEnumerable<DialogueNodeExtended> nodes)
        {
            foreach (DialogueNodeExtended node in nodes)
            {
                SwapAndWriteFirstTwoNodes(exp, conv, node);
            }
        }

        /// <summary>
        /// Try get the first Interp referencing the InterpData.
        /// </summary>
        /// <param name="interpData">InterpData to search on.</param>
        /// <param name="interp">Referencing interp.</param>
        /// <returns>Whether the Interp was found or not.</returns>
        public static bool TryGetInterp(ExportEntry interpData, out ExportEntry interp)
        {
            interp = null;

            Dictionary<IEntry, List<string>> refs = interpData.GetEntriesThatReferenceThisOne();

            if (refs.Count == 0) { return false; }

            IEntry entry = null;
            foreach (IEntry e in refs.Keys)
            {
                if (e.ClassName == "SeqAct_Interp")
                {
                    entry = e;
                    break;
                }
            }

            interp = (ExportEntry)entry;
            return true;
        }

        /// <summary>
        /// Filter the nodes, by the filter condition, to get only those that have valid audio information.
        /// </summary>
        /// <param name="nodes">Nodes to filter.</param>
        /// <param name="filter">Filter to apply to the audio nodes.</param>
        /// <param name="usedIDs">ExportIDs that exist in all the nodes. Useful for linking IDs or generating new ones.</param>
        /// <returns>Filtered nodes.</returns>
        public static List<DialogueNodeExtended> FilterAudioNodes(ObservableCollectionExtended<DialogueNodeExtended> nodes,
            Func<DialogueNodeExtended, bool> filter,
            HashSet<int> usedIDs = null)
        {
            if (nodes == null) { return null; }

            List<DialogueNodeExtended> filteredNodes = new();
            foreach (DialogueNodeExtended node in nodes)
            {
                if (node.ExportID > 0 && usedIDs != null) { usedIDs.Add(node.ExportID); }

                if (IsAudioNode(node) && filter(node)) { filteredNodes.Add(node); }
            }

            return filteredNodes;
        }

        /// <summary>
        /// Check if the given node is a valid audio node.
        /// CONDITION: Has FaceFX. FaceFX matches LineRef. LineRef is not -1. Reply type is REPLY_STANDARD.
        /// </summary>
        /// <param name="node">Node to check.</param>
        /// <returns>Whether it's an audio node or not.</returns>
        public static bool IsAudioNode(DialogueNodeExtended node)
        {
            return IsAudioNode(node, out string errMsg);
        }

        /// <summary>
        /// Check if the given node is a valid audio node.
        /// CONDITION: Has FaceFX. FaceFX matches LineRef. LineRef is not -1. Reply type is REPLY_STANDARD.
        /// </summary>
        /// <param name="node">Node to check.</param>
        /// <param name="errMsg">Error message.</param>
        /// <returns>Whether it's an audio node or not.</returns>
        public static bool IsAudioNode(DialogueNodeExtended node, out string errMsg)
        {
            errMsg = "";
            // Check that there's at least one FaceFX and store its strRef
            string faceFX = node.FaceFX_Female ?? (node.FaceFX_Male ?? "");

            // Validate that the node is meant to have data (not autocontinues or dialogend)
            // and that has proper audio data (strRef matches the FaceFX)
            if (string.IsNullOrEmpty(faceFX) || !faceFX.Contains($"{node.LineStrRef}") || node.LineStrRef == -1)
            {
                errMsg = $"Node {(node.IsReply ? "R" : "E")}{node.NodeCount} does not contain valid audio data. Check it contains a LineStrRef, and that its FaceFX exists and points to it.";
                return false;
            }
            if (node.ReplyType != EReplyTypes.REPLY_STANDARD)
            {
                errMsg = $"Node {(node.IsReply ? "R" : "E")}{node.NodeCount}'s type is not REPLY_STANDARD.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get the StrRefID of the VOElements track and the track itself of an InterpData, if they exist.
        /// </summary>
        /// <param name="interpData">InterpData to find the value on.</param>
        /// <param name="VOTrack">VOElementes track, if it exists.</param>
        /// <returns>StrRefID, if it exists.</returns>
        public static int GetVOStrRefID(ExportEntry interpData, out ExportEntry VOTrack)
        {
            VOTrack = null;
            if (!MatineeHelper.TryGetInterpGroup(interpData, "Conversation", out ExportEntry interpGroup))
            {
                return 0;
            }

            if (!MatineeHelper.TryGetInterpTrack(interpGroup, "BioEvtSysTrackVOElements", out ExportEntry interpTrack))
            {
                return 0;
            }

            IntProperty m_nStrRefID = interpTrack.GetProperty<IntProperty>("m_nStrRefID");
            if (m_nStrRefID == null)
            {
                return 0;
            }

            VOTrack = interpTrack;
            return m_nStrRefID.Value;
        }

        /// <summary>
        /// Update the StrRefID of the VOElements track of the InterpData. Creates any missing element.
        /// </summary>
        /// <param name="interpData">InterpData to update the value on.</param>
        /// <param name="strRefID">StringRefID to set.</param>
        public static void UpdateInterpDataStrRefID(ExportEntry interpData, int strRefID, ExportEntry VOElements = null)
        {
            if (VOElements == null)
            {
                if (!MatineeHelper.TryGetInterpGroup(interpData, "Conversation", out ExportEntry interpGroup))
                {
                    interpGroup = MatineeHelper.AddNewGroupToInterpData(interpData, "Conversation");
                }

                if (!MatineeHelper.TryGetInterpTrack(interpGroup, "BioEvtSysTrackVOElements", out VOElements))
                {
                    VOElements = MatineeHelper.AddNewTrackToGroup(interpGroup, "BioEvtSysTrackVOElements");
                }
            }

            PropertyCollection props = VOElements.GetProperties();
            props.AddOrReplaceProp(new IntProperty(strRefID, "m_nStrRefID"));
            AddDefaultTrackKey(VOElements, false, 0, props);
            VOElements.WriteProperties(props);
        }

        /// <summary>
        /// Add a default key to the TrackKeys prop of the interpTrack, if it doesn't contain one, creating the property if necessary,
        /// and writing the props if requested.
        /// </summary>
        /// <param name="interpTrack">Track to add the key to.</param>
        /// <param name="writeProps">Whether to write the props or not. Useful to have control whether the caller or the callee writes and avoid redundant writes.</param>
        /// <param name="time">Time to insert the key at.</param>
        /// <param name="props">Props containing the trackKeys, or to which write them.</param>
        /// <returns>Updated property collection.</returns>
        public static PropertyCollection AddDefaultTrackKey(ExportEntry interpTrack, bool writeProps, int time = 0, PropertyCollection props = null)
        {
            if (props == null) { props = interpTrack.GetProperties(); }
            ArrayProperty<StructProperty> m_aTrackKeys = props.GetProp<ArrayProperty<StructProperty>>("m_aTrackKeys")
                ?? new ArrayProperty<StructProperty>("m_aTrackKeys");

            if (!m_aTrackKeys.Any())
            {
                // Add the key to the track
                m_aTrackKeys.Add(new StructProperty("BioTrackKey",
                    new PropertyCollection()
                    {
                        new NameProperty("None", "KeyName"),
                        new FloatProperty(time, "fTime")
                    },
                    "BioTrackKey"));

                props.AddOrReplaceProp(m_aTrackKeys);
            }
            if (writeProps) { interpTrack.WriteProperties(props); }

            return props;
        }

        /// <summary>
        /// Generate an ObjComment array containing a single comment based on the given line,
        /// concatenating the line at 29 characters and adding an ellipsis at the end
        /// </summary>
        /// <param name="line">Line to use to generate the comment.</param>
        /// <returns>Generated ObjComment array.</returns>
        public static ArrayProperty<StrProperty> GenerateObjComment(string line)
        {
            line = line.Trim('\"');
            return new("m_aObjComment")
            {
                new StrProperty(line == "No Data" ? "" : line.Length <= 32 ? line : $"{line.AsSpan(0, 29)}...")
            };
        }
    }

    /// <summary>
    /// Represents audio information of a TLK line.
    /// </summary>
    public class LineAudioInfo
    {
        public string Filename_F
        {
            get; set;
        }
        public string Filename_M
        {
            get; set;
        }
        public int SizeOnDisk_F
        {
            get; set;
        }
        public int SizeOnDisk_M
        {
            get; set;
        }
        public string OffsetInFile_F
        {
            get; set;
        }
        public string OffsetInFile_M
        {
            get; set;
        }
        public string XMLUri_F
        {
            get; set;
        }
        public string XMLUri_M
        {
            get; set;
        }
        public string AnimationName_F
        {
            get; set;
        }
        public string AnimationName_M
        {
            get; set;
        }

        /// <summary>
        /// Represents audio information of a TLK line.
        /// </summary>
        /// <param name="filename_f">The afc filename the audio is on for the female line.</param>
        /// <param name="filename_m">The afc filename the audio is on for the male line.</param>
        /// <param name="sizeOnDisk_f">The binary size on disk for the female line.</param>
        /// <param name="sizeOnDisk_m">The binary size on disk for the male line.</param>
        /// <param name="offsetInFile_f">The binary offset in file as hex for the female line.</param>
        /// <param name="offsetInFile_m">The binary offset in file as hex for the male line.</param>
        /// <param name="xmlUri_f">Path to the XML file containing the face animations for the female line, optional.</param>
        /// <param name="xmlUri_m">Path to the XML file containing the face animations for the male line, optional.</param>
        /// <param name="animationName_f">Animation to select from the female XML file, optional.</param>
        /// <param name="animationName_m">Animation to select from the male XML file, optional.</param>
        public LineAudioInfo(string filename_f, string filename_m, int sizeOnDisk_f, int sizeOnDisk_m, string offsetInFile_f, string offsetInFile_m,
            string xmlUri_f = "", string xmlUri_m = "", string animationName_f = "", string animationName_m = "")
        {
            Filename_F = filename_f;
            Filename_M = filename_m;
            SizeOnDisk_F = sizeOnDisk_f;
            SizeOnDisk_M = sizeOnDisk_m;
            OffsetInFile_F = offsetInFile_f;
            OffsetInFile_M = offsetInFile_m;
            XMLUri_F = xmlUri_f;
            XMLUri_M = xmlUri_m;
            AnimationName_F = animationName_f;
            AnimationName_M = animationName_m;
        }
    }
}
