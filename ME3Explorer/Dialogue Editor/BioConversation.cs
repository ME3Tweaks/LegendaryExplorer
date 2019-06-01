using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.Unreal;

namespace ME3Explorer.Dialogue_Editor
{
    public partial class BioConversationExtended : NotifyPropertyChangedWindowBase
    {
        #region Convo
        //Contains nested conversation structure.
        // - IntepData
        //Extended Nested Collections:
        // - Speakers have FaceFX Objects
        // - DialogueNodeExtended has InterpData, WwiseStream_M, Wwisestream_F, FaceFX_ID_M, FaceFX_ID_F.


        public class ConversationExtended : NotifyPropertyChangedBase
        {
            public IExportEntry export { get; set; }

            public string ConvName { get; set; }

            public bool bParsed { get; set; }
            public bool bFirstParsed { get; set; }

            public ObservableCollectionExtended<SpeakerExtended> Speakers { get; set; }
            public ObservableCollectionExtended<DialogueNodeExtended> EntryList { get; set; }
            public ObservableCollectionExtended<DialogueNodeExtended> ReplyList { get; set; }
            /// <summary>
            /// WwiseBank Reference UIndex
            /// </summary>
            public int WwiseBank { get; set; }
            /// <summary>
            /// Sequence Reference UIndex
            /// </summary>
            public int Sequence { get; set; }

            public ConversationExtended(IExportEntry export, string ConvName, ObservableCollectionExtended<SpeakerExtended> Speakers, ObservableCollectionExtended<DialogueNodeExtended> EntryList, ObservableCollectionExtended<DialogueNodeExtended> ReplyList)
            {
                this.export = export;
                this.ConvName = ConvName;
                this.Speakers = Speakers;
                this.EntryList = EntryList;
                this.ReplyList = ReplyList;
            }

            public ConversationExtended(IExportEntry export, string ConvName, bool bParsed, bool bFirstParsed, ObservableCollectionExtended<SpeakerExtended> Speakers, ObservableCollectionExtended<DialogueNodeExtended> EntryList, ObservableCollectionExtended<DialogueNodeExtended> ReplyList, int WwiseBank, int Sequence)
            {
                this.export = export;
                this.ConvName = ConvName;
                this.bParsed = bParsed;
                this.bFirstParsed = bFirstParsed;
                this.Speakers = Speakers;
                this.EntryList = EntryList;
                this.ReplyList = ReplyList;
                this.WwiseBank = WwiseBank;
                this.Sequence = Sequence;
            }

            public void ParseSpeakers()
            {
                this.Speakers = new ObservableCollectionExtended<SpeakerExtended>();
                if (export.FileRef.Game != MEGame.ME3)
                {
                    var s_speakers = export.GetProperty<ArrayProperty<StructProperty>>("m_SpeakerList");
                    if (s_speakers != null)
                    {
                        for (int id = 0; id < s_speakers.Count; id++)
                        {
                            var spkr = new SpeakerExtended(id, s_speakers[id].GetProp<NameProperty>("sSpeakerTag").ToString());
                            Speakers.Add(spkr);
                        }
                    }
                }
                else
                {
                    var a_speakers = export.GetProperty<ArrayProperty<NameProperty>>("m_aSpeakerList");
                    if (a_speakers != null)
                    {
                        int id = 0;
                        foreach (NameProperty n in a_speakers)
                        {
                            var spkr = new SpeakerExtended(id, n.ToString());
                            Speakers.Add(spkr);
                            id++;
                        }
                    }
                }
            }
            /// <summary>
            /// Returns the Uindex of FaceFXAnimSet
            /// </summary>
            /// <param name="speakerID">SpeakerID -1 = Owner, -2 = Player</param>
            /// <param name="isMale">will pull female by default</param>
            public int ParseFaceFX(int speakerID, bool isMale = false)
            {
                string ffxPropName = "m_aFemaleFaceSets"; //ME2/M£3
                if (isMale)
                {
                    ffxPropName = "m_aMaleFaceSets";
                }
                var ffxList = export.GetProperty<ArrayProperty<ObjectProperty>>(ffxPropName);
                if (ffxList != null)
                {
                    return ffxList[speakerID + 2].Value;
                }

                return 0;
            }
            /// <summary>
            /// Returns the Uindex of appropriate sequence
            /// </summary>
            public int ParseSequence()
            {
                string propname = "MatineeSequence";
                if (export.FileRef.Game == MEGame.ME1)
                {
                    propname = "m_pEvtSystemSeq";
                }

                var seq = export.GetProperty<ObjectProperty>(propname);
                if (seq != null)
                {
                    return seq.Value;
                }

                return 0;
            }
            /// <summary>
            /// Returns the Uindex of WwiseBank
            /// </summary>
            public int ParseWwiseBank()
            {
                var Pcc = export.FileRef;
                if (Pcc.Game != MEGame.ME1)
                {
                    var ffxo = ParseFaceFX(-1, true); //find owner animset
                    if (ffxo > 0)
                    {
                        var wwevents = Pcc.getUExport(ffxo).GetProperty<ArrayProperty<ObjectProperty>>("ReferencedSoundCues"); //pull a wwiseevent array
                        if (wwevents != null)
                        {
                            if (Pcc.Game == MEGame.ME3)
                            {
                                StructProperty r = Pcc.getUExport(wwevents[0].Value).GetProperty<StructProperty>("Relationships"); //lookup bank
                                if (r != null)
                                {
                                    var bank = r.GetProp<ObjectProperty>("Bank");
                                    return bank.Value;
                                }
                            }
                            else //Game is ME2.  Wwisebank ref in Binary.
                            {
                                var data = Pcc.getUExport(wwevents[0].Value).getBinaryData();
                                int binarypos = 4;
                                int count = BitConverter.ToInt32(data, binarypos);
                                if (count > 0)
                                {
                                    binarypos += 4;
                                    int bnkcount = BitConverter.ToInt32(data, binarypos);
                                    if (bnkcount > 0)
                                    {
                                        binarypos += 4;
                                        int bank = BitConverter.ToInt32(data, binarypos);
                                        return bank;
                                    }
                                }
                            }
                        }
                    }
                }

                return 0;
            }

            public List<int> GetStartingList()
            {
                List<int> startList = new List<int>();
                var prop = export.GetProperty<ArrayProperty<ObjectProperty>>("m_StartingList"); //ME1/ME2/ME3
                if(prop != null)
                {
                    foreach(var sl in prop)
                    {
                        startList.Add(sl.Value);
                    }
                    
                }
                return startList;
            }
        }


        #endregion Convo


        public class SpeakerExtended : NotifyPropertyChangedBase
        {

            public int SpeakerID { get; set; }

            public string SpeakerName { get; set; }

            /// <summary>
            /// Male UIndex object reference
            /// </summary>
            public int FaceFX_Male { get; set; }
            /// <summary>
            /// Female UIndex object reference
            /// </summary>
            public int FaceFX_Female { get; set; }

            public SpeakerExtended(int SpeakerID, string SpeakerName)
            {
                this.SpeakerID = SpeakerID;
                this.SpeakerName = SpeakerName;
            }

            public SpeakerExtended(int SpeakerID, string SpeakerName, int FaceFX_Male, int FaceFX_Female)
            {
                this.SpeakerID = SpeakerID;
                this.SpeakerName = SpeakerName;
                this.FaceFX_Male = FaceFX_Male;
                this.FaceFX_Female = FaceFX_Female;
            }
        }


        public class DialogueNodeExtended : NotifyPropertyChangedBase
        {
            public int EntryID { get; set; }

            /// <summary>
            /// InterpData object reference UIndex
            /// </summary>
            public int Interpdata { get; set; }
            /// <summary>
            /// WwiseStream object reference Male UIndex
            /// </summary>
            public int WwiseStream_Male { get; set; }
            /// <summary>
            /// WwiseStream object reference Female UIndex
            /// </summary>
            public int WwiseStream_Female { get; set; }

            public DialogueNodeExtended(int EntryID, int Interpdata, int WwiseStream_Male, int WwiseStream_Female)
            {
                this.EntryID = EntryID;
                this.Interpdata = Interpdata;
                this.WwiseStream_Male = WwiseStream_Male;
                this.WwiseStream_Female = WwiseStream_Female;
            }

        }
    }
}
