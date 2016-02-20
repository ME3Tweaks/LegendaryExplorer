using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.UnrealHelper
{
    public class Languages
    {
        public struct Language
        {
            public string name;
            public List<string> Entries;
        }
        
        public List<Language> GlobalLang;
        public int CurrentLanguage = 0;
                    //0 = Default
                    //1 = German

        public Languages(string path,int DefaultLang)
        {
            GlobalLang = new List<Language>();
            CurrentLanguage = DefaultLang;
            if (File.Exists(path))
                LoadLanguages(path);
            else 
                CreateDefaultLang();
        }

        public void LoadLanguages(string path)
        {
            TreeViewSerializer ts = new TreeViewSerializer();
            TreeView tv = new TreeView();            
            ts.LoadXmlFileInTreeView(tv, path);
            TreeNode t = tv.Nodes[0];
            TreeNode t1 = t.Nodes[0];
            GlobalLang = new List<Language>();
            int count = t1.Nodes.Count;
            CurrentLanguage = Convert.ToInt32(t1.Nodes[0].Text);
            for (int i = 1; i < count; i++)
            {
                Language l = new Language();
                TreeNode t2 = t1.Nodes[i];
                l.name = t2.Text;
                l.Entries = new List<string>();
                int count2 = t2.Nodes.Count;
                for (int j = 0; j < count2; j++)
                    l.Entries.Add(XMLToStr(t2.Nodes[j].Text));
                GlobalLang.Add(l);
            }
        }

        public void CreateDefaultLang()
        {
            Language l = new Language();
            Form1 f1 = new Form1();
            Form2 f2 = new Form2();
            DLCExplorer dlc = new DLCExplorer();
            TOCeditor toc = new TOCeditor();
            Conditionals con = new Conditionals();
            Language_Selector lng = new Language_Selector();
            AFCExtract afc = new AFCExtract();
            BIKExtract bik = new BIKExtract();
            PropertyManager prop = new PropertyManager();
            XBoxConverter xbox = new XBoxConverter();
            Language_Editor lne = new Language_Editor();
            string[] t = new string[0];
            MainWindow tlk = new MainWindow();
            l.name = "Default";
            l.Entries = new List<string>();
#region Form1
            l.Entries.Add(f1.userToolsToolStripMenuItem.Text);                  //0
            l.Entries.Add(f1.dLCEditorToolStripMenuItem.Text);
            l.Entries.Add(f1.tLKEditorToolStripMenuItem.Text);

            l.Entries.Add(f1.tOCbinEditorToolStripMenuItem.Text);           //5
            l.Entries.Add(f1.decompressorToolStripMenuItem.Text);
            l.Entries.Add(f1.conditionalsToolStripMenuItem.Text);
            l.Entries.Add(f1.lanugageSelectorToolStripMenuItem.Text);
            //l.Entries.Add("Extractor");  // KFreon: Not sure what all this is for exactly, so just filling in bits

            l.Entries.Add(f1.aFCToWAVToolStripMenuItem.Text);                    //10
            l.Entries.Add(f1.moviestfcToBIKToolStripMenuItem.Text);
            l.Entries.Add(f1.propertyManagerToolStripMenuItem.Text);
            //l.Entries.Add(f1.xBoxConverterToolStripMenuItem.Text);
            l.Entries.Add(f1.optionsToolStripMenuItem.Text);

            l.Entries.Add(f1.selectToolLanguageToolStripMenuItem.Text);     //15
#endregion
#region Form2
            l.Entries.Add(f2.Text);
            l.Entries.Add(f2.fileOpenToolStripMenuItem.Text);
#endregion
#region DLC Explorer
            l.Entries.Add(dlc.Text);                                        //60
            l.Entries.Add(dlc.groupBoxSfar.Text);
            l.Entries.Add(dlc.labelNumOfFiles.Text);
            l.Entries.Add(dlc.labelTotalUncSize.Text);
            l.Entries.Add(dlc.labelTotalUncBytes.Text);

            l.Entries.Add(dlc.labelTotalComprSize.Text);                    //65
            l.Entries.Add(dlc.labelTotalComprBytes.Text);
            l.Entries.Add(dlc.labelComprRatio.Text);
            l.Entries.Add(dlc.labelFirstEntryOffset.Text);
            l.Entries.Add(dlc.labelFirstBlockOffset.Text);

            l.Entries.Add(dlc.labelFirstDataOffset.Text);                   //70
            l.Entries.Add(dlc.groupBoxFile.Text);
            l.Entries.Add(dlc.labelFullName.Text);
            l.Entries.Add(dlc.labelHash.Text);
            l.Entries.Add(dlc.labelFileSize.Text);

            l.Entries.Add(dlc.labelUncSizeBytes.Text);                      //75
            l.Entries.Add(dlc.labelComprSize.Text);
            l.Entries.Add(dlc.labelComprSizeBytes.Text);
            l.Entries.Add(dlc.labelEntry.Text);
            l.Entries.Add(dlc.labelBlockIndex.Text);

            l.Entries.Add(dlc.labelDataOffset.Text);                        //80
            l.Entries.Add(dlc.toolStripOpenFile.Text);
            l.Entries.Add(dlc.toolStripSaveFile.Text);
            l.Entries.Add(dlc.toolStripInfo.Text);
            l.Entries.Add(dlc.toolStripAbout.Text);
#endregion
#region TOC Editor
            l.Entries.Add(toc.Text);                                        //85
            l.Entries.Add(toc.fileToolStripMenuItem.Text);
            l.Entries.Add(toc.openToolStripMenuItem.Text);
            l.Entries.Add(toc.saveToolStripMenuItem.Text);
            l.Entries.Add(toc.searchToolStripMenuItem.Text);

            l.Entries.Add(toc.searchAgainToolStripMenuItem.Text);           //90
            l.Entries.Add(toc.editFilesizeToolStripMenuItem.Text);
#endregion
#region Conditionals
            l.Entries.Add(con.Text);
            l.Entries.Add(con.fileToolStripMenuItem.Text);
            l.Entries.Add(con.openToolStripMenuItem.Text);

            l.Entries.Add(con.saveToolStripMenuItem.Text);                  //95
            l.Entries.Add(con.mapToSVGToolStripMenuItem.Text);
            l.Entries.Add(con.editToolStripMenuItem.Text);
#endregion
#region Language Select
            l.Entries.Add(lng.Text);
            l.Entries.Add(lng.radioButton1.Text);

            l.Entries.Add(lng.radioButton2.Text);                           //100
            l.Entries.Add(lng.radioButton3.Text);
            l.Entries.Add(lng.radioButton4.Text);
            l.Entries.Add(lng.radioButton5.Text);
            l.Entries.Add(lng.radioButton6.Text);

            l.Entries.Add(lng.button1.Text);                                //105
            l.Entries.Add(lng.button2.Text);
#endregion
#region AFC
            l.Entries.Add(afc.Text);
            l.Entries.Add(afc.fileToolStripMenuItem.Text);
            l.Entries.Add(afc.openToolStripMenuItem.Text);

            l.Entries.Add(afc.extractToolStripMenuItem.Text);               //110
            l.Entries.Add(afc.selectedToolStripMenuItem.Text);
            l.Entries.Add(afc.allToolStripMenuItem.Text);
#endregion
#region BIK
            l.Entries.Add(bik.Text);
            l.Entries.Add(bik.fileToolStripMenuItem.Text);

            l.Entries.Add(bik.openToolStripMenuItem.Text);                  //115
            l.Entries.Add(bik.extractToolStripMenuItem.Text);
            l.Entries.Add(bik.selectedToolStripMenuItem.Text);
            l.Entries.Add(bik.allToolStripMenuItem.Text);
#endregion
#region PropertyMan
            l.Entries.Add(prop.Text);

            l.Entries.Add(prop.fileToolStripMenuItem.Text);                 //120
            l.Entries.Add(prop.newToolStripMenuItem.Text);
            l.Entries.Add(prop.loadDefinitonsToolStripMenuItem.Text);
            l.Entries.Add(prop.saveDefinitionsToolStripMenuItem.Text);
            l.Entries.Add(prop.loadDefaultToolStripMenuItem.Text);

            l.Entries.Add(prop.saveAsDefaultToolStripMenuItem.Text);        //125
            l.Entries.Add(prop.importFromFileToolStripMenuItem.Text);
            l.Entries.Add(prop.actionToolStripMenuItem.Text);
            l.Entries.Add(prop.addClassToolStripMenuItem.Text);
            l.Entries.Add(prop.addPropertyToolStripMenuItem.Text);

            l.Entries.Add(prop.deleteClassToolStripMenuItem.Text);          //130
            l.Entries.Add(prop.deletePropertyToolStripMenuItem.Text);
#endregion
#region xboxconverter
            l.Entries.Add(xbox.Text);
            l.Entries.Add(xbox.fileToolStripMenuItem.Text);
            l.Entries.Add(xbox.xXXPCCToolStripMenuItem.Text);

            l.Entries.Add(xbox.pCCXXXToolStripMenuItem.Text);               //135
#endregion
#region Language Editor
            l.Entries.Add(lne.Text);
            l.Entries.Add(lne.fileToolStripMenuItem.Text);
            l.Entries.Add(lne.saveToolStripMenuItem.Text);
            l.Entries.Add(lne.editToolStripMenuItem.Text);

            l.Entries.Add(lne.copyLanguagToolStripMenuItem.Text);           //140
            l.Entries.Add(lne.deleteLanguageToolStripMenuItem.Text);
            l.Entries.Add(lne.renameLanguageToolStripMenuItem.Text);
            l.Entries.Add(lne.editEntryToolStripMenuItem.Text);
            l.Entries.Add(lne.setLanguageAsDefaultToolStripMenuItem.Text);
#endregion
            GlobalLang.Add(l);
        }

        public void SetLang(Form1 f1)
        {
            f1.userToolsToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[0];
            f1.dLCEditorToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[3];
            f1.tLKEditorToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[4];
            f1.tOCbinEditorToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[5];
            f1.decompressorToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[6];
            f1.conditionalsToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[7];
            f1.lanugageSelectorToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[8];
            //f1.aFCExtractorToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[9];
            f1.aFCToWAVToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[10];
            f1.moviestfcToBIKToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[11];
            f1.propertyManagerToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[12];
            //f1.xBoxConverterToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[13];
            f1.optionsToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[14];
            f1.selectToolLanguageToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[15];
        }
        public void SetLang(Form2 f2)
        {
            f2.Text = GlobalLang[CurrentLanguage].Entries[16];
            f2.fileOpenToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[17];
        }

        public void SetLang(DLCExplorer dlc)
        {
            dlc.Text = GlobalLang[CurrentLanguage].Entries[60];                                        //60
            dlc.groupBoxSfar.Text = GlobalLang[CurrentLanguage].Entries[61];
            dlc.labelNumOfFiles.Text = GlobalLang[CurrentLanguage].Entries[62];
            dlc.labelTotalUncSize.Text = GlobalLang[CurrentLanguage].Entries[63];
            dlc.labelTotalUncBytes.Text = GlobalLang[CurrentLanguage].Entries[64];

            dlc.labelTotalComprSize.Text = GlobalLang[CurrentLanguage].Entries[65];                    //65
            dlc.labelTotalComprBytes.Text = GlobalLang[CurrentLanguage].Entries[66];
            dlc.labelComprRatio.Text = GlobalLang[CurrentLanguage].Entries[67];
            dlc.labelFirstEntryOffset.Text = GlobalLang[CurrentLanguage].Entries[68];
            dlc.labelFirstBlockOffset.Text = GlobalLang[CurrentLanguage].Entries[69];

            dlc.labelFirstDataOffset.Text = GlobalLang[CurrentLanguage].Entries[70];                   //70
            dlc.groupBoxFile.Text = GlobalLang[CurrentLanguage].Entries[71];
            dlc.labelFullName.Text = GlobalLang[CurrentLanguage].Entries[72];
            dlc.labelHash.Text = GlobalLang[CurrentLanguage].Entries[73];
            dlc.labelFileSize.Text = GlobalLang[CurrentLanguage].Entries[74];

            dlc.labelUncSizeBytes.Text = GlobalLang[CurrentLanguage].Entries[75];                      //75
            dlc.labelComprSize.Text = GlobalLang[CurrentLanguage].Entries[76];
            dlc.labelComprSizeBytes.Text = GlobalLang[CurrentLanguage].Entries[77];
            dlc.labelEntry.Text = GlobalLang[CurrentLanguage].Entries[78];
            dlc.labelBlockIndex.Text = GlobalLang[CurrentLanguage].Entries[79];

            dlc.labelDataOffset.Text = GlobalLang[CurrentLanguage].Entries[80];                        //80
            dlc.toolStripOpenFile.Text = GlobalLang[CurrentLanguage].Entries[81];
            dlc.toolStripSaveFile.Text = GlobalLang[CurrentLanguage].Entries[82];
            dlc.toolStripInfo.Text = GlobalLang[CurrentLanguage].Entries[83];
            dlc.toolStripAbout.Text = GlobalLang[CurrentLanguage].Entries[84];
        }
        public void SetLang(TOCeditor toc)
        {
            toc.Text = GlobalLang[CurrentLanguage].Entries[85];                                        //85
            toc.fileToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[86];
            toc.openToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[87];
            toc.saveToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[88];
            toc.searchToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[89];
            toc.searchAgainToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[90];           //90
            toc.editFilesizeToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[91];
        }
        public void SetLang(Conditionals con)
        {
            con.Text = GlobalLang[CurrentLanguage].Entries[92];
            con.fileToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[93];
            con.openToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[94];
            con.saveToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[95];                 //95
            con.mapToSVGToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[96];
            con.editToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[97];
        }
        public void SetLang(Language_Selector lng)
        {
            lng.Text = GlobalLang[CurrentLanguage].Entries[98];
            lng.radioButton1.Text = GlobalLang[CurrentLanguage].Entries[99];
            lng.radioButton2.Text = GlobalLang[CurrentLanguage].Entries[100];                           //100
            lng.radioButton3.Text = GlobalLang[CurrentLanguage].Entries[101];
            lng.radioButton4.Text = GlobalLang[CurrentLanguage].Entries[102];
            lng.radioButton5.Text = GlobalLang[CurrentLanguage].Entries[103];
            lng.radioButton6.Text = GlobalLang[CurrentLanguage].Entries[104];
            lng.button1.Text = GlobalLang[CurrentLanguage].Entries[105];                                //105
            lng.button2.Text = GlobalLang[CurrentLanguage].Entries[106];
        }
        public void SetLang(AFCExtract afc)
        {
            afc.Text = GlobalLang[CurrentLanguage].Entries[107];
            afc.fileToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[108];
            afc.openToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[109];
            afc.extractToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[110];               //110
            afc.selectedToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[111];
            afc.allToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[112];
        }
        public void SetLang(BIKExtract bik)
        {
            bik.Text = GlobalLang[CurrentLanguage].Entries[113];
            bik.fileToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[114];
            bik.openToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[115];
            bik.extractToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[116];
            bik.selectedToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[117];
            bik.allToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[118];
        }
        public void SetLang(PropertyManager prop)
        {
            prop.Text = GlobalLang[CurrentLanguage].Entries[119];
            prop.fileToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[120];                 //120
            prop.newToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[121];
            prop.loadDefinitonsToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[122];
            prop.saveDefinitionsToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[123];
            prop.loadDefaultToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[124];
            prop.saveAsDefaultToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[125];        //125
            prop.importFromFileToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[126];
            prop.actionToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[127];
            prop.addClassToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[128];
            prop.addPropertyToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[129];
            prop.deleteClassToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[130];          //130
            prop.deletePropertyToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[131];
        }
        public void SetLang(XBoxConverter xbox)
        {
            xbox.Text = GlobalLang[CurrentLanguage].Entries[132];
            xbox.fileToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[133];
            xbox.xXXPCCToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[134];
            xbox.pCCXXXToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[135];               //135
        }
        public void SetLang(Language_Editor lne)
        {
            lne.Text = GlobalLang[CurrentLanguage].Entries[136];
            lne.fileToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[137];
            lne.saveToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[138];
            lne.editToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[139];
            lne.copyLanguagToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[140];           //140
            lne.deleteLanguageToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[141];
            lne.renameLanguageToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[142];
            lne.editEntryToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[143];
            lne.setLanguageAsDefaultToolStripMenuItem.Text = GlobalLang[CurrentLanguage].Entries[144];
        }

        public string StrToXML(string s)
        {
            string t = "";
            for(int i=0;i<s.Length;i++)
                switch (s[i])
                {
                    case 'ä':
                        t += "&1";
                        break;
                    case 'ö':
                        t += "&2";
                        break;
                    case 'ü':
                        t += "&3";
                        break;
                    case 'Ä':
                        t += "&4";
                        break;
                    case 'Ö':
                        t += "&5";
                        break;
                    case 'Ü':
                        t += "&6";
                        break;
                    case 'ß':
                        t += "&7";
                        break;
                    case '&':
                        t += "&8";
                        break;
                    case '<':
                        t += "&9";
                        break;
                    case '>':
                        t += "&A";
                        break;
                    default:
                        t += s[i];
                        break;
                }
            return t;
        }

        public string XMLToStr(string s)
        {
            string t = "";
            for (int i = 0; i < s.Length; i++)
                if (s[i] == '&')
                {
                    string t2 = s[i].ToString();
                    t2 += s[i + 1];
                    i++;
                    switch (t2)
                    {
                        case "&1":
                            t += "ä";
                            break;
                        case "&2":
                            t += "ö";
                            break;
                        case "&3":
                            t += "ü";
                            break;
                        case "&4":
                            t += "Ä";
                            break;
                        case "&5":
                            t += "Ö";
                            break;
                        case "&6":
                            t += "Ü";
                            break;
                        case "&7":
                            t += "ß";
                            break;
                        case "&8":
                            t += "&";
                            break;
                        case "&9":
                            t += "<";
                            break;
                        case "&A":
                            t += ">";
                            break;
                    }

                }
                else
                    t += s[i];
            return t;
        }

        public TreeNode ToTree()
        {
            TreeNode t = new TreeNode("Languages");
            t.Nodes.Add(new TreeNode(CurrentLanguage.ToString()));
            for (int i = 0; i < GlobalLang.Count; i++)
            {
                TreeNode t2 = new TreeNode(GlobalLang[i].name);
                for (int j = 0; j < GlobalLang[i].Entries.Count; j++)
                {
                    TreeNode t3 = new TreeNode(StrToXML(GlobalLang[i].Entries[j]));
                    t2.Nodes.Add(t3);
                }
                t.Nodes.Add(t2);
            }
            return t;
        }

        
    }
}
