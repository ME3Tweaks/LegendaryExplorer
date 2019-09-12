using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml;
using System.Xml.Serialization;

namespace ME3Explorer.PropertyDatabase
{
    /// <summary>
    /// Interaction logic for PropertyDB
    /// </summary>
    public partial class PropertyDB : WPFBase
    {
        #region Declarations

        public MEGame currentGame { get; set; }

        public PropsDataBase CurrentDataBase { get; set; }
        private string CurrentDBPath { get; set; }

        public ICommand GenerateDBCommand { get; set; }
        public ICommand SaveDBCommand { get; set; }
        public ICommand SwitchMECommand { get; set; }


        public override void handleUpdate(List<PackageUpdate> updates)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Startup/Exit

        public PropertyDB()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Property Database WPF", new WeakReference(this));
            LoadCommands();

            //Get default path
            CurrentDBPath = Properties.Settings.Default.PropertyDBPath;
            Enum.TryParse<MEGame>(Properties.Settings.Default.PropertyDBGame, out MEGame game);

            InitializeComponent();

            if (CurrentDBPath != null && CurrentDBPath.EndsWith("xml") && File.Exists(CurrentDBPath) && game != MEGame.Unknown)
            {
                SwitchGame(game.ToString());
            }
            else
            {
                CurrentDBPath = null;
                SwitchGame("ME3");
                StatusBar_LeftMostText.Text = "No database found.";
            }

            
        }

        private void LoadCommands()
        {
            GenerateDBCommand = new GenericCommand(GenerateDatabase);
            //SaveDBCommand = new GenericCommand();
            SwitchMECommand = new RelayCommand(SwitchGame);
        }

        private void PropertyDB_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;
            //Dump Database
            //Save settings (path, currentGame)
            Properties.Settings.Default.PropertyDBPath = CurrentDBPath;
            Properties.Settings.Default.PropertyDBGame = currentGame.ToString();
        }

        #endregion

        #region UserCommands
        public void GenerateDatabase()
        {
            //Use Property Dumper - run directly? to generate?
            //Save to XML
        }

        public void LoadDatabase()
        {
            if(CurrentDBPath == null)
            {
                //Open XML file
                return;
            }
            //Select DB from Game
            var filename = $"{System.IO.Path.GetDirectoryName(CurrentDBPath)}propertyDB{currentGame}.xml";
            //Load database
            CurrentDataBase = XmlHelper.FromXmlFile<PropsDataBase>(filename);


        }

        public void SaveDatabase()
        {
            //Set filename
            //Save database to XML
            var result = XmlHelper.ToXml(CurrentDataBase);
        }


        public void SwitchGame(object param)
        {
            var p = param as string;
            switchME1_menu.IsChecked = false;
            switchME2_menu.IsChecked = false;
            switchME3_menu.IsChecked = false;
            switch (p)
            {
                case "ME1":
                    currentGame = MEGame.ME1;
                    StatusBar_GameID_Text.Text = "ME1";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Navy);
                    switchME1_menu.IsChecked = true;
                    break;
                case "ME2":
                    currentGame = MEGame.ME2;
                    StatusBar_GameID_Text.Text = "ME2";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.Maroon);
                    switchME2_menu.IsChecked = true;
                    break;
                default:
                    currentGame = MEGame.ME3;
                    StatusBar_GameID_Text.Text = "ME3";
                    StatusBar_GameID_Text.Background = new SolidColorBrush(Colors.DarkSeaGreen);
                    switchME3_menu.IsChecked = true;
                    break;
            }
            
            
            LoadDatabase();
        }

        #endregion


    }
    #region Database
    /// <summary>
    /// Database Classes
    /// </summary>
    /// 
    public class PropsDataBase
    {
        public MEGame meGame { get; set; }
        public string GenerationDate { get; set; }
        public List<ClassRecord> ClassRecords { get; set; }


    }

    public class ClassRecord
    {
        public string Class { get; set; }
        public string Definition_package { get; set; }
        public List<PropertyRecord> PropertyRecords { get; set; }

    }

    public class PropertyRecord
    {
        public string Property { get; set; }
        public List<PropertyUsage> PropertyUsages { get; set; }

    }

    public class PropertyUsage
    {
        public string Filename { get; set; }
        public string ExportUID { get; set; }
        public bool IsDefault { get; set; }
        public string Value { get; set; }

    }
    #endregion

    #region XMLGen
    public static class XmlHelper
    {
        public static bool NewLineOnAttributes { get; set; }
        /// <summary>
        /// Serializes an object to an XML string, using the specified namespaces.
        /// </summary>
        public static string ToXml(object obj, XmlSerializerNamespaces ns)
        {
            Type T = obj.GetType();

            var xs = new XmlSerializer(T);
            var ws = new XmlWriterSettings { Indent = true, NewLineOnAttributes = NewLineOnAttributes, OmitXmlDeclaration = true };

            var sb = new StringBuilder();
            using (XmlWriter writer = XmlWriter.Create(sb, ws))
            {
                xs.Serialize(writer, obj, ns);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Serializes an object to an XML string.
        /// </summary>
        public static string ToXml(object obj)
        {
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            return ToXml(obj, ns);
        }

        /// <summary>
        /// Deserializes an object from an XML string.
        /// </summary>
        public static PropsDataBase FromXml<T>(string xml)
        {
            XmlSerializer xs = new XmlSerializer(typeof(PropsDataBase));
            using (StringReader sr = new StringReader(xml))
            {
                return (PropsDataBase)xs.Deserialize(sr);
            }
        }

        /// <summary>
        /// Deserializes an object from an XML string, using the specified type name.
        /// </summary>
        public static object FromXml(string xml, string typeName)
        {
            Type T = Type.GetType(typeName);
            XmlSerializer xs = new XmlSerializer(T);
            using (StringReader sr = new StringReader(xml))
            {
                return xs.Deserialize(sr);
            }
        }

        /// <summary>
        /// Serializes an object to an XML file.
        /// </summary>
        public static void ToXmlFile(Object obj, string filePath)
        {
            var xs = new XmlSerializer(obj.GetType());
            var ns = new XmlSerializerNamespaces();
            var ws = new XmlWriterSettings { Indent = true, NewLineOnAttributes = NewLineOnAttributes, OmitXmlDeclaration = true };
            ns.Add("", "");

            using (XmlWriter writer = XmlWriter.Create(filePath, ws))
            {
                xs.Serialize(writer, obj);
            }
        }

        /// <summary>
        /// Deserializes an object from an XML file.
        /// </summary>
        public static PropsDataBase FromXmlFile<T>(string filePath)
        {
            StreamReader sr = new StreamReader(filePath);
            try
            {
                var result = FromXml<PropsDataBase>(sr.ReadToEnd());
                return result;
            }
            catch (Exception e)
            {
                throw new Exception("There was an error attempting to read the file " + filePath + "\n\n" + e.InnerException.Message);
            }
            finally
            {
                sr.Close();
            }
        }
    }
    #endregion
}
