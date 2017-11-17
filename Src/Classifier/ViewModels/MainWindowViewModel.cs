using Classifier.Core;
using Classifier.Data;
using LandmarkDevs.Core.Infrastructure;
using System.IO;
using System.Linq;

namespace Classifier.ViewModels
{
    public class MainWindowViewModel : Observable
    {
        public MainWindowViewModel()
        {
            using(var context = new ClassifierContext())
            {
                var documentTypes = context.DocumentTypes.ToList();
                foreach(var type in documentTypes)
                {
                    var resultPath = Path.Combine(Common.ResultsStorage, type.DocumentType);
                    if (!Directory.Exists(resultPath)) Directory.CreateDirectory(resultPath);
                }
            }
        }
    }
}
