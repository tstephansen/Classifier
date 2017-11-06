using LandmarkDevs.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.ViewModels
{
    public class MainWindowViewModel : Observable
    {
        public MainWindowViewModel()
        {
            CreateAndRemoveDirectories();
        }
        public void CreateAndRemoveDirectories()
        {
            if (!Directory.Exists(AppStorage)) Directory.CreateDirectory(AppStorage);
            if (!Directory.Exists(PdfPath)) Directory.CreateDirectory(PdfPath);
            if (Directory.Exists(TempStorage)) Directory.Delete(TempStorage, true);
            System.Threading.Thread.Sleep(2000);
            if (!Directory.Exists(TempStorage)) Directory.CreateDirectory(TempStorage);
        }

        public string AppStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier";
        public string TempStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\temp";
        public string PdfPath = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\PDFs";
    }
}
