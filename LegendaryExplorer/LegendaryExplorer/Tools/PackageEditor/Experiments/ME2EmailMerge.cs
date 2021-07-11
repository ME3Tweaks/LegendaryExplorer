using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Vml.Office;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using Newtonsoft.Json;

namespace LegendaryExplorer.Tools.PackageEditor.Experiments
{
    public class ME2EmailMerge
    {
        internal class ME2EmailMergeFile
        {
            [JsonProperty("game")]
            public MEGame Game { get; set; }

            [JsonProperty("modName")]
            public string ModName { get; set; }

            [JsonProperty("emails")]
            public List<ME2EmailSingle> Emails { get; set; }
        }

        internal class ME2EmailSingle
        {
            [JsonProperty("emailName")]
            public string EmailName { get; set; }

            [JsonProperty("triggerConditional")]
            public int TriggerConditional { get; set; }

            [JsonProperty("sendTransition")]
            public int SendTransition { get; set; }

            [JsonProperty("statusPlotInt")]
            public int StatusPlotInt { get; set; }

            [JsonProperty("titleStrRef")]
            public int TitleStrRef { get; set; }

            [JsonProperty("descStrRef")]
            public int DescStrRef { get; set; }
        }

        public static string ResourcesFile =
            @"D:\Mass Effect Modding\Dumb Shit\ME2 Mail Merge\103Message_Templates.pcc";

        public static string BaseFile =
            @"D:\Mass Effect Modding\Dumb Shit\ME2 Mail Merge\BioD_Nor_103Messages.pcc";

        public static void BuildMessagesSequence(string outputPath)
        {
            using IMEPackage resources = MEPackageHandler.OpenMEPackage(ResourcesFile);
            using IMEPackage pcc = MEPackageHandler.OpenMEPackage(BaseFile);
            
            int messageID = 95;
            var emailInfos = new List<ME2EmailMergeFile>();

            var json = @"D:\Mass Effect Modding\Dumb Shit\ME2 Mail Merge\test.json";
            emailInfos.Add(JsonConvert.DeserializeObject<ME2EmailMergeFile>(File.ReadAllText(json)));

            // This only works for LE2 at the moment. ME2 is going to be painful unless we require all DLCs

            // Send message - On level load, all email conditionals are checked and emails are sent via transition if they are true
            ExportEntry SendMessageContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Send_Messages");
            ExportEntry LastSendMessage = pcc.FindExport(pcc.Game == MEGame.LE2 ? @"TheWorld.PersistentLevel.Main_Sequence.Send_Messages.METR_Messages" : @"TheWorld.PersistentLevel.Main_Sequence.Send_Messages.DLC_UNC_Pack");
            ExportEntry TemplateSendMessage = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Send_MessageTemplate");


            // Mark Read - email ints are set to read when the terminal is opened on unread
            ExportEntry MarkReadContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read");
            ExportEntry LastMarkRead = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read.DLC_CER");
            KismetHelper.RemoveOutputLinks(LastMarkRead);

            ExportEntry MarkReadOutLink =
                pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read.DLC_UNC_Pack_02");
            ExportEntry TemplateMarkRead = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_ReadTemplate");
            ExportEntry TemplateMarkReadTransition = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read_Transition");


            // Display Messages - 
            ExportEntry DisplayMessageContainer =
                pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_Messages");
            ExportEntry DisplayMessageOutLink =
                pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_Messages.SeqCond_CompareBool_0");
            IEntry lastDisplayMessageEntry = KismetHelper.GetSequenceObjects(DisplayMessageContainer).First((e) =>
            {
                if (e is ExportEntry seq)
                {
                    var outLinks = KismetHelper.GetOutboundLinksOfNode(seq);
                    if (outLinks.Count > 0 && outLinks[0].Count > 0)
                    {
                        return outLinks[0][0].LinkedOp == DisplayMessageOutLink;
                    }
                }
                return false;
            });
            ExportEntry LastDisplayMessage = lastDisplayMessageEntry as ExportEntry;
            KismetHelper.RemoveOutputLinks(LastDisplayMessage);
            var DisplayMessageVariableLinks = LastDisplayMessage.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            ExportEntry TemplateDisplayMessage =
                resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_MessageTemplate");

            // Archive Messages - 
            ExportEntry ArchiveContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Archive_Message");
            ExportEntry ArchiveSwitch = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Archive_Message.SeqAct_Switch_0");
            ExportEntry ArchiveOutLink =
                pcc.FindExport(
                    @"TheWorld.PersistentLevel.Main_Sequence.Archive_Message.BioSeqAct_PMCheckConditional_1");
            ExportEntry ExampleSetInt = KismetHelper.GetOutboundLinksOfNode(ArchiveSwitch)[0][0].LinkedOp as ExportEntry;
            ExportEntry ExamplePlotInt =
                ExampleSetInt.GetProperty<ArrayProperty<StructProperty>>("VariableLinks").Values[0]
                    .GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables")[0].ResolveToEntry(pcc) as ExportEntry;
            
            foreach (var emailMod in emailInfos)
            {
                string modName = "DLC_MOD_" + emailMod.ModName;

                foreach (var email in emailMod.Emails)
                {
                    string emailName = modName + "_" + email.EmailName;

                    //
                    // SendMessage
                    //

                    // Create seq object
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
                        TemplateSendMessage,
                        pcc, SendMessageContainer, true, out var outSendEntry);

                    var newSend = outSendEntry as ExportEntry;

                    // Set name, comment, add to sequence
                    newSend.ObjectName = new NameReference(emailName);
                    KismetHelper.AddObjectToSequence(newSend, SendMessageContainer);
                    newSend.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                    {
                        new StrProperty(emailName)
                    }, "m_aObjComment"));

                    // Set Trigger Conditional
                    var pmCheckConditionalSM = newSend.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqAct_PMCheckConditional" && e is ExportEntry);
                    if (pmCheckConditionalSM is ExportEntry conditional)
                    {
                        conditional.WriteProperty(new IntProperty(email.TriggerConditional, "m_nIndex"));
                        conditional.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                        {
                            new StrProperty("Time for "+email.EmailName+"?")
                        }, "m_aObjComment"));
                    }
                    
                    // Set Send Transition
                    var pmExecuteTransitionSM = newSend.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqAct_PMExecuteTransition" && e is ExportEntry);
                    if (pmExecuteTransitionSM is ExportEntry transition)
                    {
                        transition.WriteProperty(new IntProperty(email.SendTransition, "m_nIndex"));
                        transition.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                        {
                            new StrProperty("Send "+email.EmailName+" message.")
                        }, "m_aObjComment"));
                    }

                    // Hook up output links
                    KismetHelper.CreateOutputLink(LastSendMessage, "Out", newSend);
                    LastSendMessage = newSend;

                    //
                    // MarkRead
                    //

                    // Create seq object
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
                        TemplateMarkRead, pcc, MarkReadContainer, true, out var outMarkReadEntry);
                    var newMarkRead = outMarkReadEntry as ExportEntry;

                    // Set name, comment, add to sequence
                    newMarkRead.ObjectName = new NameReference(emailName);
                    KismetHelper.AddObjectToSequence(newMarkRead, MarkReadContainer);
                    newMarkRead.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                    {
                        new StrProperty(emailName)
                    }, "m_aObjComment"));

                    // Set Plot Int
                    var storyManagerIntMR = newMarkRead.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqVar_StoryManagerInt" && e is ExportEntry);
                    if (storyManagerIntMR is ExportEntry plotIntMR)
                    {
                        plotIntMR.WriteProperty(new IntProperty(email.StatusPlotInt, "m_nIndex"));
                        plotIntMR.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                        {
                            new StrProperty(email.EmailName)
                        }, "m_aObjComment"));
                    }

                    // Hook up output links
                    KismetHelper.CreateOutputLink(LastMarkRead, "Out", newMarkRead);
                    LastMarkRead = newMarkRead;

                    //
                    // Display Email
                    //

                    // Create seq object
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
                        TemplateDisplayMessage, pcc, DisplayMessageContainer, true, out var outDisplayMessage);
                    var newDisplayMessage = outDisplayMessage as ExportEntry;

                    // Set name, comment, variable links, add to sequence
                    newDisplayMessage.ObjectName = new NameReference(emailName);
                    KismetHelper.AddObjectToSequence(newDisplayMessage, DisplayMessageContainer);
                    newDisplayMessage.WriteProperty(DisplayMessageVariableLinks);
                    newDisplayMessage.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                    {
                        new StrProperty(emailName)
                    }, "m_aObjComment"));

                    var displayChildren = newDisplayMessage.GetChildren();

                    // Set Plot Int
                    var storyManagerIntDE = displayChildren.FirstOrDefault(e =>
                        e.ClassName == "BioSeqVar_StoryManagerInt" && e is ExportEntry);
                    if (storyManagerIntDE is ExportEntry plotIntDE)
                    {
                        plotIntDE.WriteProperty(new IntProperty(email.StatusPlotInt, "m_nIndex"));
                    }

                    // Set Email ID
                    var emailIdDE = displayChildren.FirstOrDefault(e =>
                        e.ClassName == "SeqVar_Int" && e is ExportEntry);
                    if (emailIdDE is ExportEntry EmailIDDE)
                    {
                        EmailIDDE.WriteProperty(new IntProperty(messageID, "IntValue"));
                        messageID++;
                    }

                    // Set Title StrRef
                    var titleStrRef = displayChildren.FirstOrDefault(e =>
                        e.ClassName == "BioSeqVar_StrRef" && e is ExportEntry ee && ee.GetProperty<NameProperty>("VarName").Value == "Title StrRef");
                    if (titleStrRef is ExportEntry Title)
                    {
                        Title.WriteProperty(new StringRefProperty(email.TitleStrRef, "m_srValue"));
                    }

                    // Set Description StrRef
                    var descStrRef = displayChildren.FirstOrDefault(e =>
                        e.ClassName == "BioSeqVar_StrRef" && e is ExportEntry ee && ee.GetProperty<NameProperty>("VarName").Value == "Desc StrRef");
                    if (descStrRef is ExportEntry Desc)
                    {
                        Desc.WriteProperty(new StringRefProperty(email.DescStrRef, "m_srValue"));
                    }

                    // Hook up output links
                    KismetHelper.CreateOutputLink(LastDisplayMessage, "Out", newDisplayMessage);
                    LastDisplayMessage = newDisplayMessage;

                    //
                    // Archive Email
                    //
                    var NewSetInt = EntryCloner.CloneEntry(ExampleSetInt);
                    KismetHelper.AddObjectToSequence(NewSetInt, ArchiveContainer);
                    KismetHelper.CreateOutputLink(ArchiveSwitch, "Link "+messageID, NewSetInt);

                    var NewPlotInt = EntryCloner.CloneEntry(ExamplePlotInt);
                    KismetHelper.AddObjectToSequence(NewPlotInt, ArchiveContainer);
                    NewPlotInt.WriteProperty(new IntProperty(email.StatusPlotInt, "m_nIndex"));
                    NewPlotInt.WriteProperty(new StrProperty(emailName, "m_sRefName"));

                    NewSetInt.GetProperty<ArrayProperty<StructProperty>>("VariableLinks").Values[0]
                        .GetProp<ArrayProperty<ObjectProperty>>("LinkedVariables")[0] = new ObjectProperty(NewPlotInt);

                    KismetHelper.CreateOutputLink(NewSetInt, "Out", ArchiveOutLink);
                }
            }
            KismetHelper.CreateOutputLink(LastMarkRead, "Out", MarkReadOutLink);
            KismetHelper.CreateOutputLink(LastDisplayMessage, "Out", DisplayMessageOutLink);

            pcc.Save(outputPath);
        }
    }
}
