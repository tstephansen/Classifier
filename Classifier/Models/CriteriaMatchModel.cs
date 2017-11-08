using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Classifier.Data;

namespace Classifier.Models
{
    public class CriteriaMatchModel
    {
        public string PdfFile { get; set; }
        public string CriteriaFile { get; set; }
        public DocumentTypes DocumentType { get; set; }
        public DocumentCriteria DocumentCriteria { get; set; }
        public int Matches { get; set; }
    }
}
