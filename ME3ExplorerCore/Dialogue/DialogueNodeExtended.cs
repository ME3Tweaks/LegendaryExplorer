using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;

namespace ME3ExplorerCore.Dialogue
{

    [DebuggerDisplay("DNExtended {ReplyType} IsReply: {IsReply}, Line: {Line}")]

    public class DialogueNodeExtended : INotifyPropertyChanged
    {
        public bool IsReply { get; set; }
        public int NodeCount { get; set; } //This is the count for reply and node.
        public StructProperty NodeProp { get; set; }
        public int SpeakerIndex { get; set; }
        public int LineStrRef { get; set; }
        public string Line { get; set; }
        public bool FiresConditional { get; set; }
        public int ConditionalOrBool { get; set; }
        public int Transition { get; set; }
        /// <summary>
        /// Tag of speaker - generated.
        /// </summary>
        public SpeakerExtended SpeakerTag { get; set; }
        /// <summary>
        /// InterpData object reference UIndex
        /// </summary>
        public ExportEntry Interpdata { get; set; }
        /// <summary>
        /// Length of interpdata
        /// </summary>
        public float InterpLength { get; set; }
        /// <summary>
        /// WwiseStream object reference Male UIndex
        /// </summary>
        public ExportEntry WwiseStream_Male { get; set; }
        /// <summary>
        /// WwiseStream object reference Female UIndex
        /// </summary>
        public ExportEntry WwiseStream_Female { get; set; }
        /// <summary>
        /// FaceFX reference Male TBD
        /// </summary>
        public string FaceFX_Male { get; set; }
        /// <summary>
        /// FaceFX reference female TBD
        /// </summary>
        public string FaceFX_Female { get; set; }
        public int Listener { get; set; }
        public int ConditionalParam { get; set; }
        public int TransitionParam { get; set; }
        public int ExportID { get; set; }
        public bool IsSkippable { get; set; }
        public bool IsUnskippable { get; set; }
        public bool IsDefaultAction { get; set; }
        public bool IsMajorDecision { get; set; }
        public bool IsNonTextLine { get; set; }
        public bool IgnoreBodyGesture { get; set; }
        public bool IsAmbient { get; set; }
        public int CameraIntimacy { get; set; }
        public bool HideSubtitle { get; set; }
        public NameReference Script { get; set; }
        public EBCConvGUIStyles GUIStyle { get; set; }
        public EBCReplyTypes ReplyType { get; set; }

        public DialogueNodeExtended(StructProperty NodeProp, bool IsReply, int NodeCount, int SpeakerIndex, int LineStrRef, string Line, bool FiresConditional, int ConditionalOrBool, int Transition, EBCReplyTypes ReplyType)
        {
            this.NodeProp = NodeProp;
            this.IsReply = IsReply;
            this.NodeCount = NodeCount;
            this.SpeakerIndex = SpeakerIndex;
            this.LineStrRef = LineStrRef;
            this.Line = Line;
            this.FiresConditional = FiresConditional;
            this.ConditionalOrBool = ConditionalOrBool;
            this.Transition = Transition;
            this.ReplyType = ReplyType;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="nodeExtended"></param>
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
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }

}
