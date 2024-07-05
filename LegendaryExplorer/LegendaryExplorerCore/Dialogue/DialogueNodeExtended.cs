using System.ComponentModel;
using System.Diagnostics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Dialogue
{
    /// <summary>
    /// Represents a node in a BioConversation with it's StructProperty values and other useful information parsed
    /// </summary>
    [DebuggerDisplay("DNExtended {ReplyType} IsReply: {IsReply}, Line: {Line}")]
    public class DialogueNodeExtended : INotifyPropertyChanged
    {
        /// <summary>If true, this represents a player node (reply), if false, a non-player node (entry)</summary>
        public bool IsReply { get; set; }
        /// <summary>This node's index in the reply or entry array in the conversation export</summary>
        public int NodeCount { get; set; }
        /// <summary>The original StructProperty of this dialogue node</summary>
        public StructProperty NodeProp { get; set; }
        /// <summary>The speaker index for this node. If this is a reply node, this should be -2 (player). The nSpeakerIndex property</summary>
        public int SpeakerIndex { get; set; }
        /// <summary>The TLK string reference of this node's line. The srText property</summary>
        public int LineStrRef { get; set; }
        /// <summary>The parsed dialogue line from a TLK file, based on <see cref="LineStrRef"/></summary>
        public string Line { get; set; }
        /// <summary>If true, this node checks for a conditional. If false, it checks for a plot bool. The bFireConditional property value</summary>
        public bool FiresConditional { get; set; }
        /// <summary>The conditional or bool ID checked by this node. Conditional or bool is determined by <see cref="FiresConditional"/>. The nConditionalFunc property</summary>
        /// <remarks>If this plot is false, this node will be skipped.</remarks>
        public int ConditionalOrBool { get; set; }
        /// <summary>The parameter that should be supplied to the <see cref="ConditionalOrBool"/>. The nConditionalParam property</summary>
        /// <remarks>If this node checks a conditional, this is the conditional parameter. If this node checks a bool,
        /// a param value of 1 will check if the bool is true, while a value of 0 will check if the bool is false.</remarks>
        public int ConditionalParam { get; set; }
        /// <summary>The parsed plot path of the <see cref="ConditionalOrBool"/> value from a plot database</summary>
        public string ConditionalPlotPath { get; set; }
        /// <summary>The ID of a transition that will be fired when this node is activated. The nStateTransition property</summary>
        public int Transition { get; set; }
        /// <summary>The parameter that should be supplied to the <see cref="Transition"/>. The nStateTransitionParam property</summary>
        public int TransitionParam { get; set; }
        /// <summary>The parsed plot path of the <see cref="Transition"/> value from a plot database</summary>
        public string TransitionPlotPath { get; set; }
        /// <summary>Extended information on this node's speaker</summary>
        public SpeakerExtended SpeakerTag { get; set; }
        /// <summary>A reference to the InterpData used by this node</summary>
        public ExportEntry Interpdata { get; set; }
        /// <summary>The length of this node's InterpData</summary>
        public float InterpLength { get; set; }
        /// <summary>A reference to this node's male WwiseStream object</summary>
        public ExportEntry WwiseStream_Male { get; set; }
        /// <summary>A reference to this node's female WwiseStream object</summary>
        public ExportEntry WwiseStream_Female { get; set; }
        /// <summary>The line name of this node's male FaceFX line</summary>
        public string FaceFX_Male { get; set; }
        /// <summary>The line name of this node's female FaceFX line</summary>
        public string FaceFX_Female { get; set; }
        /// <summary>The nListenerIndex property of this node.</summary>
        public int Listener { get; set; }
        /// <summary>The nExportID property of this node. A unique number tying this dialogue node to a ConvNode sequence event.</summary>
        public int ExportID { get; set; }
        /// <summary>The bSkippable property of this node. Only used on entries, if <see cref="IsReply"/> is false.</summary>
        public bool IsSkippable { get; set; }
        /// <summary>The bUnskippable property of this node. Only used on replies, if <see cref="IsReply"/> is true</summary>
        public bool IsUnskippable { get; set; }
        /// <summary>The bIsDefaultAction property of this node. Only exists on Reply nodes in Game 3</summary>
        public bool IsDefaultAction { get; set; }
        /// <summary>The bIsMajorDecision property of this node. Only exists on Reply nodes in Game 3</summary>
        public bool IsMajorDecision { get; set; }
        /// <summary>The bIsNonTextLine property of this node</summary>
        public bool IsNonTextLine { get; set; }
        /// <summary>The bIgnoreBodyGestures property of this node</summary>
        public bool IgnoreBodyGesture { get; set; }
        /// <summary>The bAmbient property of this node. True if this dialogue line should be ambient</summary>
        public bool IsAmbient { get; set; }
        /// <summary>The nCameraIntimacy property of this node</summary>
        /// <remarks>This value is used to partially determine SwitchCamera angles when no camera is specified in an InterpData</remarks>
        public int CameraIntimacy { get; set; }
        /// <summary>The bAlwaysHideSubtitle property of this node. Only exists in Game 3</summary>
        public bool HideSubtitle { get; set; }
        /// <summary>The NameReference of a scripted event that should be fired on this node</summary>
        public NameReference Script { get; set; }
        /// <summary>The GUI style of this node</summary>
        public EConvGUIStyles GUIStyle { get; set; }
        /// <summary>The reply type of this node. Only used if <see cref="IsReply"/> is true.</summary>
        public EReplyTypes ReplyType { get; set; }

        /// <summary>
        /// Basic constructor to create a new DialogueNodeExtended
        /// </summary>
        /// <param name="nodeProp">Node's StructProperty</param>
        /// <param name="isReply">True if this is a reply, false if entry</param>
        /// <param name="nodeCount">The array index of this node</param>
        /// <param name="speakerIndex">Speaker index of this node</param>
        /// <param name="lineStrRef">String ref of this node's line</param>
        /// <param name="line">String representation of this node's line</param>
        /// <param name="firesConditional">The bFireConditional value of this node</param>
        /// <param name="conditionalOrBool">The nConditionalFunc value of this node</param>
        /// <param name="transition">The nStateTransition value of this node</param>
        /// <param name="replyType">The ReplyType value of this node</param>
        /// <param name="conditionalParam">The ConditionalParam value of this node</param>
        public DialogueNodeExtended(StructProperty nodeProp, bool isReply, int nodeCount, int speakerIndex, int lineStrRef, string line, bool firesConditional, int conditionalOrBool, int transition, EReplyTypes replyType, int conditionalParam = 0)
        {
            NodeProp = nodeProp;
            IsReply = isReply;
            NodeCount = nodeCount;
            SpeakerIndex = speakerIndex;
            LineStrRef = lineStrRef;
            Line = line;
            FiresConditional = firesConditional;
            ConditionalOrBool = conditionalOrBool;
            ConditionalParam = conditionalParam;
            Transition = transition;
            ReplyType = replyType;
        }

        /// <summary>
        /// Constructor to create a copy of another DialogueNodeExtended
        /// </summary>
        /// <param name="nodeExtended">Node to create a copy of</param>
        public DialogueNodeExtended(DialogueNodeExtended nodeExtended)
        {
            NodeProp = nodeExtended.NodeProp;
            IsReply = nodeExtended.IsReply;
            NodeCount = nodeExtended.NodeCount;
            SpeakerIndex = nodeExtended.SpeakerIndex;
            LineStrRef = nodeExtended.LineStrRef;
            Line = nodeExtended.Line;
            FiresConditional = nodeExtended.FiresConditional;
            ConditionalOrBool = nodeExtended.ConditionalOrBool;
            Transition = nodeExtended.Transition;
            SpeakerTag = nodeExtended.SpeakerTag;
            Interpdata = nodeExtended.Interpdata;
            WwiseStream_Male = nodeExtended.WwiseStream_Male;
            WwiseStream_Female = nodeExtended.WwiseStream_Female;
            FaceFX_Male = nodeExtended.FaceFX_Male;
            FaceFX_Female = nodeExtended.FaceFX_Female;
            Listener = nodeExtended.Listener;
            ConditionalParam = nodeExtended.ConditionalParam;
            TransitionParam = nodeExtended.TransitionParam;
            ExportID = nodeExtended.ExportID;
            IsSkippable = nodeExtended.IsSkippable;
            IsUnskippable = nodeExtended.IsUnskippable;
            IsDefaultAction = nodeExtended.IsDefaultAction;
            IsMajorDecision = nodeExtended.IsMajorDecision;
            IsNonTextLine = nodeExtended.IsNonTextLine;
            IgnoreBodyGesture = nodeExtended.IgnoreBodyGesture;
            IsAmbient = nodeExtended.IsAmbient;
            CameraIntimacy = nodeExtended.CameraIntimacy;
            HideSubtitle = nodeExtended.HideSubtitle;
            Script = nodeExtended.Script;
            GUIStyle = nodeExtended.GUIStyle;
            ReplyType = nodeExtended.ReplyType;
            ConditionalPlotPath = nodeExtended.ConditionalPlotPath;
            TransitionPlotPath = nodeExtended.TransitionPlotPath;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
