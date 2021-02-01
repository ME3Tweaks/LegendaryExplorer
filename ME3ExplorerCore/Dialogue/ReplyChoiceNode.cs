using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ME3ExplorerCore.Dialogue
{

    public class ReplyChoiceNode : INotifyPropertyChanged
    {
        public int Order { get; set; }
        public int Index { get; set; }
        public string Paraphrase { get; set; }
        /// <summary>
        /// Reply choice strref
        /// </summary>
        public int ReplyStrRef { get; set; }
        /// <summary>
        /// reply choice category
        /// </summary>
        public EBCReplyCategory RCategory { get; set; }
        public string ReplyLine { get; set; }
        public string NodeIDLink { get; set; }
        public string Ordinal { get; set; }
        public int TgtCondition { get; set; }
        public string TgtFireCnd { get; set; }
        public string TgtLine { get; set; }
        public string TgtSpeaker { get; set; }

        public ReplyChoiceNode(int Index, string Paraphrase, int ReplyStrRef, EBCReplyCategory RCategory, string ReplyLine)
        {
            this.Index = Index;
            this.Paraphrase = Paraphrase;
            this.ReplyStrRef = ReplyStrRef;
            this.RCategory = RCategory;
            this.ReplyLine = ReplyLine;
        }

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
