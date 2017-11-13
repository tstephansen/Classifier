using System;
using System.Collections.Generic;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Core
{
    public class StorageManager
    {
        private const string AppDirectory = "Classifier";
        private const string CriteriaDirectory = "Classifier/Criteria";

        public static void CreateIsoStorage()
        {
            using (IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Domain | IsolatedStorageScope.Assembly, null, null))
            {
                if (!isoStore.DirectoryExists(AppDirectory)) isoStore.CreateDirectory(AppDirectory);
                if (!isoStore.DirectoryExists(CriteriaDirectory)) isoStore.CreateDirectory(CriteriaDirectory);
                isoStore.CreateDirectory("Classifier/Criteria");
            }
        }
    }
}
