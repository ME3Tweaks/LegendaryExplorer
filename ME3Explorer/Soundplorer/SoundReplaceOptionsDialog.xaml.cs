using System.Windows;
using System.Windows.Input;
using ME3Explorer.SharedUI;
using ME3ExplorerCore.Misc;

namespace ME3Explorer.Soundplorer
{
    /// <summary>
    /// Interaction logic for SoundReplaceOptionsDialog.xaml
    /// </summary>
    public partial class SoundReplaceOptionsDialog : TrackingNotifyPropertyChangedWindowBase
    {
        public ObservableCollectionExtended<int> SampleRates { get; } = new ObservableCollectionExtended<int>();
        private static readonly int[] AcceptedSampleRates = {24000, 32000}; //may add more later
        public WwiseConversionSettingsPackage ChosenSettings; 

        public SoundReplaceOptionsDialog() : base("Sound Replace Options Dialog", false)
        {
            DataContext = this;
            SampleRates.AddRange(AcceptedSampleRates);
            LoadCommands();
            InitializeComponent();
            SampleRate_Combobox.SelectedIndex = 0;
        }

        public SoundReplaceOptionsDialog(Window w) : this()
        {
            Owner = w;
        }


        public ICommand ConvertAudioCommand { get; private set; }

        void LoadCommands()
        {
            ConvertAudioCommand = new GenericCommand(ReturnSettings, CanReturnSettings);
        }

        private void ReturnSettings()
        {
            ChosenSettings = new WwiseConversionSettingsPackage
            {
                TargetSamplerate = (int)SampleRate_Combobox.SelectedItem
            };
            DialogResult = true;
            Close();
        }

        private bool CanReturnSettings() => SampleRate_Combobox.SelectedIndex >= 0;

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
    public class WwiseConversionSettingsPackage
    {
        public int TargetSamplerate = 0;
    }
}
