using Classifier.Data;
using System.IO;

namespace Classifier.Models
{
    public class CriteriaMatchModel
    {
        public string PdfFile { get; set; }
        public DocumentTypes DocumentType { get; set; }
        public long Score { get; set; }
        public FileInfo MatchedFileInfo { get; set; }
    }
}
