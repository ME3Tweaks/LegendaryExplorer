using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Kismet;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal;
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
            
            int messageID = 100;
            var emailInfos = new List<ME2EmailMergeFile>();

            var json = @"D:\Mass Effect Modding\Dumb Shit\ME2 Mail Merge\test.json";
            emailInfos.Add(JsonConvert.DeserializeObject<ME2EmailMergeFile>(File.ReadAllText(json)));

            // This only works for LE2.

            // Send message - On level load, all email conditionals are checked and emails are sent via transition if they are true
            ExportEntry SendMessageContainer = pcc.GetUExport(1500); // The sequence object containing SendMessages
            ExportEntry LastSendMessage = pcc.GetUExport(1520); // The last SendMessage in the chain
            ExportEntry TemplateSendMessage = resources.GetUExport(128);

            ExportEntry MarkReadContainer = pcc.GetUExport(1490);
            ExportEntry LastMarkRead = pcc.GetUExport(1491);
            KismetHelper.RemoveOutputLinks(LastMarkRead);
            ExportEntry MarkReadOutLink = pcc.GetUExport(1499);
            ExportEntry TemplateMarkRead = resources.GetUExport(133);
            ExportEntry TemplateMarkReadTransition = resources.GetUExport(172);

            foreach (var emailMod in emailInfos)
            {
                string modName = "DLC_MOD_" + emailMod.ModName;
                foreach (var email in emailMod.Emails)
                {
                    string emailName = modName + "_" + email.EmailName;

                    //
                    // SendMessage
                    //
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
                        TemplateSendMessage,
                        pcc, SendMessageContainer, true, out var outSendEntry);

                    var newSend = outSendEntry as ExportEntry;

                    newSend.ObjectName = new NameReference(emailName);
                    KismetHelper.AddObjectToSequence(newSend, SendMessageContainer);
                    newSend.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                    {
                        new StrProperty(emailName)
                    }, "m_aObjComment"));

                    // Set Trigger Conditional
                    var pmCheckConditional = newSend.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqAct_PMCheckConditional" && e is ExportEntry);
                    if (pmCheckConditional is ExportEntry conditional)
                    {
                        conditional.WriteProperty(new IntProperty(email.TriggerConditional, "m_nIndex"));
                        conditional.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                        {
                            new StrProperty("Time for "+email.EmailName+"?")
                        }, "m_aObjComment"));
                    }
                    
                    // Set Send Transition
                    var pmExecuteTransition = newSend.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqAct_PMExecuteTransition" && e is ExportEntry);
                    if (pmExecuteTransition is ExportEntry transition)
                    {
                        transition.WriteProperty(new IntProperty(email.SendTransition, "m_nIndex"));
                        transition.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                        {
                            new StrProperty("Send "+email.EmailName+" message.")
                        }, "m_aObjComment"));
                    }

                    KismetHelper.CreateOutputLink(LastSendMessage, "Out", newSend);
                    LastSendMessage = newSend;

                    //
                    // MarkRead
                    //
                    EntryImporter.ImportAndRelinkEntries(EntryImporter.PortingOption.CloneTreeAsChild,
                        TemplateMarkRead, pcc, MarkReadContainer, true, out var outMarkReadEntry);
                    var newMarkRead = outMarkReadEntry as ExportEntry;

                    newMarkRead.ObjectName = new NameReference(emailName);
                    KismetHelper.AddObjectToSequence(newMarkRead, MarkReadContainer);
                    newMarkRead.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                    {
                        new StrProperty(emailName)
                    }, "m_aObjComment"));

                    // Set Plot Int
                    var storyManagerInt = newMarkRead.GetChildren()
                        .FirstOrDefault(e => e.ClassName == "BioSeqVar_StoryManagerInt" && e is ExportEntry);
                    if (storyManagerInt is ExportEntry plotInt)
                    {
                        plotInt.WriteProperty(new IntProperty(email.StatusPlotInt, "m_nIndex"));
                        plotInt.WriteProperty(new ArrayProperty<StrProperty>(new List<StrProperty>()
                        {
                            new StrProperty(email.EmailName)
                        }, "m_aObjComment"));
                    }

                    KismetHelper.CreateOutputLink(LastMarkRead, "Out", newMarkRead);
                    LastMarkRead = newMarkRead;

                    //
                    // Display Email
                    //

                    //
                    // Archive Email
                    //
                }
            }
            KismetHelper.CreateOutputLink(LastMarkRead, "Out", MarkReadOutLink);

            pcc.Save(outputPath);
        }
    }
}
