using System.ComponentModel;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Dialogue
{
    /// <summary>
    /// Represents a reply in a BioConversation with useful information parsed
    /// </summary>
    /// <remarks>
    /// Built from a BioDialogReplyListDetails StructProperty from an entry node, and the player reply node it links to
    /// </remarks>
    public class ReplyChoiceNode : INotifyPropertyChanged
    {
        /// <summary>The order in the reply list that this choice is found</summary>
        public int Order { get; set; }
        /// <summary>The reply node ID of the dialogue node that this choice links to</summary>
        public int Index { get; set; }
        /// <summary>The value of the sParaphrase string property</summary>
        /// <remarks>At the moment, this will always be an empty string</remarks>
        public string Paraphrase { get; set; }
        /// <summary>The TLK string reference of this nodes paraphrased reply. The srParaphrase property</summary>
        public int ReplyStrRef { get; set; }
        /// <summary>The category of this reply. Partially determines position on the dialogue wheel</summary>
        public EReplyCategory RCategory { get; set; }
        /// <summary>The parsed paraphrase reply string, based on <see cref="ReplyStrRef"/></summary>
        public string ReplyLine { get; set; }
        /// <summary>A string version of <see cref="Index"/>, with "E" added for displaying in the UI (eg. 'E12')</summary>
        public string NodeIDLink { get; set; }
        /// <summary>A string version of <see cref="Order"/> with an ordinal numbering suffix (eg. '1st')</summary>
        public string Ordinal { get; set; }
        /// <summary>The <see cref="DialogueNodeExtended.ConditionalOrBool"/> value of the reply node that this choice links to</summary>
        public int TgtCondition { get; set; }
        /// <summary>The <see cref="DialogueNodeExtended.FiresConditional"/> value of the reply node that this choice links to</summary>
        public string TgtFireCnd { get; set; }
        /// <summary>The parsed string <see cref="DialogueNodeExtended.Line"/> value of the reply node that this choice links to</summary>
        public string TgtLine { get; set; }
        /// <summary>The <see cref="SpeakerExtended.SpeakerName"/> of the speaker of the reply node that this choice links to</summary>
        public string TgtSpeaker { get; set; }

        /// <summary>
        /// Basic constructor for <see cref="ReplyChoiceNode"/>
        /// </summary>
        /// <param name="index">Index of reply node that this choice links to</param>
        /// <param name="paraphrase">The sParaphrase string property value</param>
        /// <param name="replyStrRef">The srParaphrase value of this choice</param>
        /// <param name="rCategory">The category of this choice</param>
        /// <param name="replyLine">String representation of the paraphrase line</param>
        public ReplyChoiceNode(int index, string paraphrase, int replyStrRef, EReplyCategory rCategory, string replyLine)
        {
            Index = index;
            Paraphrase = paraphrase;
            ReplyStrRef = replyStrRef;
            RCategory = rCategory;
            ReplyLine = replyLine;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="other">ReplyChoiceNode to copy values from</param>
        public ReplyChoiceNode(ReplyChoiceNode other)
        {
            Order = other.Order;
            Index = other.Index;
            Paraphrase = other.Paraphrase;
            ReplyStrRef = other.ReplyStrRef;
            RCategory = other.RCategory;
            ReplyLine = other.ReplyLine;
            NodeIDLink = other.NodeIDLink;
            Ordinal = other.Ordinal;
            TgtCondition = other.TgtCondition;
            TgtFireCnd = other.TgtFireCnd;
            TgtLine = other.TgtLine;
            TgtSpeaker = other.TgtSpeaker;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
