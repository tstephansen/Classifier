using Classifier.Data;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Classifier
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            var currentDir = System.IO.Directory.GetCurrentDirectory();
            AppDomain.CurrentDomain.SetData("DataDirectory", System.IO.Directory.GetCurrentDirectory());
            base.OnStartup(e);
            if (!System.Diagnostics.Debugger.IsAttached)
            {
                Copy64BitBinaries(currentDir);
            }
        }

        private static void Copy64BitBinaries(string currentDir)
        {
            var x64Path = Path.Combine(currentDir, "x64");
            var x64Dir = new DirectoryInfo(x64Path);
            var x64Files = x64Dir.GetFiles();
            foreach(var file in x64Files)
            {
                var filePath = Path.Combine(currentDir, file.Name);
                if (!File.Exists(filePath))
                    File.Copy(file.FullName, filePath);
            }
        }
    }
}
