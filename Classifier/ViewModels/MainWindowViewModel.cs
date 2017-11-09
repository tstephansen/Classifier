using Classifier.Core;
using Classifier.Data;
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
            using(var context = new DataContext())
            {
                var documentTypes = context.DocumentTypes.ToList();
                foreach(var type in documentTypes)
                {
                    var resultPath = Path.Combine(Common.ResultsStorage, type.DocumentType);
                    if (!Directory.Exists(resultPath)) Directory.CreateDirectory(resultPath);
                }
            }
        }
        public static void CreateAndRemoveDirectories()
        {
            if (!Directory.Exists(Common.AppStorage)) Directory.CreateDirectory(Common.AppStorage);
            if (!Directory.Exists(Common.PdfPath)) Directory.CreateDirectory(Common.PdfPath);
            if (Directory.Exists(Common.TempStorage)) Directory.Delete(Common.TempStorage, true);
            System.Threading.Thread.Sleep(2000);
            if (!Directory.Exists(Common.TempStorage)) Directory.CreateDirectory(Common.TempStorage);
            if (!Directory.Exists(Common.CriteriaStorage)) Directory.CreateDirectory(Common.CriteriaStorage);
            if (!Directory.Exists(Common.ResultsStorage)) Directory.CreateDirectory(Common.ResultsStorage);
        }
    }
}
