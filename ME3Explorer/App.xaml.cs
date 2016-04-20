using System;
using System.Reflection;
using System.Windows;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string GetVersion()
        {
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;
            return ver.Major + "." + ver.Minor + "." + ver.Build;
        }
    }
}
