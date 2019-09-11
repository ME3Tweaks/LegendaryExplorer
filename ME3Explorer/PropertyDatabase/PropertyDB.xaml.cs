using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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

namespace ME3Explorer.PropertyDatabase
{
    /// <summary>
    /// Interaction logic for Page1.xaml
    /// </summary>
    public partial class PropertyDB : WPFBase
    {
        #region Declarations

        private MEGame currentGameDB;

        
        public ICommand GenerateDBCommand { get; set; }
        public ICommand SaveDBCommand { get; set; }
        public ICommand switchME1Command { get; set; }
        public ICommand switchME2Command { get; set; }
        public ICommand switchME3Command { get; set; }

        #endregion

        #region Startup/Exit

        public PropertyDB()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Property Database WPF", new WeakReference(this));
            LoadCommands();

            //Get default path
            //Get default / last game - set currentGameDB
            //Load database
            
            InitializeComponent();
        }

        private void LoadCommands()
        {
            GenerateDBCommand = new GenericCommand(GenerateDatabase);
            //SaveDBCommand = new GenericCommand();
            switchME1Command = new GenericCommand(SwitchGameME1);
            switchME2Command = new GenericCommand(SwitchGameME2);
            switchME3Command = new GenericCommand(SwitchGameME3);
        }

        private void PropertyDB_Closing(object sender, CancelEventArgs e)
        {
            if (e.Cancel)
                return;
            //Dump Database
            //Save settings (path, currentGame)
        }

        #endregion


        public void GenerateDatabase()
        {
            //Use Property Dumper - run directly? to generate?
        }

        public void LoadDatabase()
        {
            //Dump existing database
            //Load database
        }

        public void SwitchGameME1()
        {
            currentGameDB = MEGame.ME1;
            LoadDatabase();
        }

        public void SwitchGameME2()
        {
            currentGameDB = MEGame.ME2;
            LoadDatabase();
        }

        public void SwitchGameME3()
        {
            currentGameDB = MEGame.ME3;
            LoadDatabase();
        }

        public override void handleUpdate(List<PackageUpdate> updates)
        {
            throw new NotImplementedException();
        }
    }
}
