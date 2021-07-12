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

        public static void BuildMessagesSequence(string outputPath)
        {
            // File to base modifications on
            string BaseFile =
                @"D:\Mass Effect Modding\Dumb Shit\ME2 Mail Merge\BioD_Nor_103Messages.pcc";
            using IMEPackage pcc = MEPackageHandler.OpenMEPackage(BaseFile);

            // Path to Message templates file - different files for ME2/LE2
            string ResourcesFilePath = pcc.Game == MEGame.LE2
                ? @"D:\Mass Effect Modding\Dumb Shit\ME2 Mail Merge\103Message_Templates.pcc"
                : "";
            using IMEPackage resources = MEPackageHandler.OpenMEPackage(ResourcesFilePath);
            
            var emailInfos = new List<ME2EmailMergeFile>();

            var json = @"D:\Mass Effect Modding\Dumb Shit\ME2 Mail Merge\test.json";
            emailInfos.Add(JsonConvert.DeserializeObject<ME2EmailMergeFile>(File.ReadAllText(json)));

            // Sanity checks
            if (!emailInfos.Any() || !emailInfos.SelectMany(e => e.Emails).Any())
            {
                return;
            }

            if(emailInfos.Any(e => e.Game != pcc.Game))
            {
                throw new Exception("ME2 email merge manifest targets incorrect game");
            }

            // Send message - All email conditionals are checked and emails transitions are triggered
            ExportEntry SendMessageContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Send_Messages");
            ExportEntry LastSendMessage = KismetHelper.GetSequenceObjects(SendMessageContainer).OfType<ExportEntry>()
                .FirstOrDefault(e =>
                {
                    var outbound = KismetHelper.GetOutboundLinksOfNode(e);
                    return outbound.Count == 1 && outbound[0].Count == 0;
                });
            ExportEntry TemplateSendMessage = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Send_MessageTemplate");


            // Mark Read - email ints are set to read
            // This is the only section that does not gracefully handle different DLC installations - DLC_CER is required atm
            ExportEntry MarkReadContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read");
            ExportEntry LastMarkRead = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read.DLC_CER");
            ExportEntry MarkReadOutLink = KismetHelper.GetOutboundLinksOfNode(LastMarkRead)[0][0].LinkedOp as ExportEntry;
            KismetHelper.RemoveOutputLinks(LastMarkRead);

            ExportEntry TemplateMarkRead = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_ReadTemplate");
            ExportEntry TemplateMarkReadTransition = resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Mark_Read_Transition");


            // Display Messages - Str refs are passed through to GUI
            ExportEntry DisplayMessageContainer =
                pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_Messages");
            ExportEntry DisplayMessageOutLink =
                pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_Messages.SeqCond_CompareBool_0");
            
            ExportEntry LastDisplayMessage = SeqTools.FindOutboundConnectionsToNode(DisplayMessageOutLink, KismetHelper.GetSequenceObjects(DisplayMessageContainer).OfType<ExportEntry>())[0];
            KismetHelper.RemoveOutputLinks(LastDisplayMessage);
            var DisplayMessageVariableLinks = LastDisplayMessage.GetProperty<ArrayProperty<StructProperty>>("VariableLinks");
            ExportEntry TemplateDisplayMessage =
                resources.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Display_MessageTemplate");

            // Archive Messages - Message ints are set to 3
            ExportEntry ArchiveContainer = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Archive_Message");
            ExportEntry ArchiveSwitch = pcc.FindExport(@"TheWorld.PersistentLevel.Main_Sequence.Archive_Message.SeqAct_Switch_0");
            ExportEntry ArchiveOutLink =
                pcc.FindExport(
                    @"TheWorld.PersistentLevel.Main_Sequence.Archive_Message.BioSeqAct_PMCheckConditional_1");
            ExportEntry ExampleSetInt = KismetHelper.GetOutboundLinksOfNode(ArchiveSwitch)[0][0].LinkedOp as ExportEntry;
            ExportEntry ExamplePlotInt = SeqTools.GetVariableLinksOfNode(ExampleSetInt)[0].LinkedNodes[0] as ExportEntry;

            int messageID = KismetHelper.GetOutboundLinksOfNode(ArchiveSwitch).Count + 1;
            int currentSwCount = ArchiveSwitch.GetProperty<IntProperty>("LinkCount").Value;
            
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
                    KismetHelper.SetComment(newSend, emailName);

                    // Set Trigger Conditional
                    var pmCheckConditionalSM = newSend.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqAct_PMCheckConditional" && e is ExportEntry);
                    if (pmCheckConditionalSM is ExportEntry conditional)
                    {
                        conditional.WriteProperty(new IntProperty(email.TriggerConditional, "m_nIndex"));
                        KismetHelper.SetComment(conditional, "Time for "+email.EmailName+"?");
                    }
                    
                    // Set Send Transition
                    var pmExecuteTransitionSM = newSend.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqAct_PMExecuteTransition" && e is ExportEntry);
                    if (pmExecuteTransitionSM is ExportEntry transition)
                    {
                        transition.WriteProperty(new IntProperty(email.SendTransition, "m_nIndex"));
                        KismetHelper.SetComment(transition, "Send "+email.EmailName+" message.");
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
                    KismetHelper.SetComment(newMarkRead, emailName);

                    // Set Plot Int
                    var storyManagerIntMR = newMarkRead.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqVar_StoryManagerInt" && e is ExportEntry);
                    if (storyManagerIntMR is ExportEntry plotIntMR)
                    {
                        plotIntMR.WriteProperty(new IntProperty(email.StatusPlotInt, "m_nIndex"));
                        KismetHelper.SetComment(plotIntMR, email.EmailName);
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
                    KismetHelper.SetComment(newDisplayMessage, emailName);

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
                    KismetHelper.CreateOutputLink(NewSetInt, "Out", ArchiveOutLink);

                    KismetHelper.CreateNewOutputLink(ArchiveSwitch, "Link "+(messageID - 1), NewSetInt);

                    var NewPlotInt = EntryCloner.CloneEntry(ExamplePlotInt);
                    KismetHelper.AddObjectToSequence(NewPlotInt, ArchiveContainer);
                    NewPlotInt.WriteProperty(new IntProperty(email.StatusPlotInt, "m_nIndex"));
                    NewPlotInt.WriteProperty(new StrProperty(emailName, "m_sRefName"));

                    var linkedVars = SeqTools.GetVariableLinksOfNode(NewSetInt);
                    linkedVars[0].LinkedNodes = new List<IEntry>() {NewPlotInt};
                    SeqTools.WriteVariableLinksToNode(NewSetInt, linkedVars);

                    messageID++;
                    currentSwCount++;
                }
            }
            KismetHelper.CreateOutputLink(LastMarkRead, "Out", MarkReadOutLink);
            KismetHelper.CreateOutputLink(LastDisplayMessage, "Out", DisplayMessageOutLink);
            ArchiveSwitch.WriteProperty(new IntProperty(currentSwCount, "LinkCount"));

            pcc.Save(outputPath);
        }
    }
}
