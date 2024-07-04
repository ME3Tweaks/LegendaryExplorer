using System.ComponentModel;
using System.Diagnostics;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorerCore.Dialogue
{
    /// <summary>
    /// Represents a BioDialogueSpeaker in a BioConversation with useful information parsed
    /// </summary>
    [DebuggerDisplay("SpeakerExtended {SpeakerID} {SpeakerName}")]
    public class SpeakerExtended : INotifyPropertyChanged
    {
        /// <summary>The index of this speaker in the BioConversation's m_SpeakerList</summary>
        public int SpeakerID { get; set; }
        /// <summary>A string representation of this speaker's name. This is usually the actor's tag</summary>
        public string SpeakerName
        {
            get => SpeakerNameRef.Instanced;
            set => SpeakerNameRef = NameReference.FromInstancedString(value);
        }

        /// <summary>Reference to this speaker's male FaceFXAnimSet</summary>
        public IEntry FaceFX_Male { get; set; }
        /// <summary>Reference to this speaker's female FaceFXAnimSet</summary>
        public IEntry FaceFX_Female { get; set; }
        /// <summary>The TLK string ID used by this speaker in subtitles</summary>
        public int StrRefID { get; set; }
        /// <summary>The parsed string value of the speaker's <see cref="StrRefID"/>. EG: "Shepard"</summary>
        public string FriendlyName { get; set; }
        /// <summary>The name reference for this speaker, typically the sSpeakerTag property in the BioDialogueSpeaker struct</summary>
        public NameReference SpeakerNameRef { get; set; }

        /// <summary>
        /// Creates a SpeakerExtended with just speaker name and index
        /// </summary>
        /// <param name="speakerID">Index into speaker array of this speaker</param>
        /// <param name="speakerName">Name or tag of this speaker, as a NameReference</param>
        public SpeakerExtended(int speakerID, NameReference speakerName)
        {
            SpeakerID = speakerID;
            SpeakerNameRef = speakerName;
        }

        /// <summary>
        /// Creates a SpeakerExtended from a full set of parsed information
        /// </summary>
        /// <param name="speakerID">Index into speaker array of this speaker</param>
        /// <param name="speakerName">Name or tag of this speaker</param>
        /// <param name="faceFxMale">Male FaceFXAnimSet for this speaker</param>
        /// <param name="faceFxFemale">Female FaceFXAnimSet for this speaker</param>
        /// <param name="strRefID">TLK string ID of speaker's in-game name</param>
        /// <param name="friendlyName">String of speaker's in-game name</param>
        public SpeakerExtended(int speakerID, NameReference speakerName, IEntry faceFxMale, IEntry faceFxFemale, int strRefID, string friendlyName)
        {
            SpeakerID = speakerID;
            SpeakerNameRef = speakerName;
            FaceFX_Male = faceFxMale;
            FaceFX_Female = faceFxFemale;
            StrRefID = strRefID;
            FriendlyName = friendlyName;
        }

#pragma warning disable
        public event PropertyChangedEventHandler PropertyChanged;
#pragma warning restore
    }
}
