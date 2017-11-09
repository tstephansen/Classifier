using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Models
{
    public class DocumentSelectionModel
    {
        public Guid DocumentTypeId { get; set; }
        public string DocumentType { get; set; }
        public bool Selected { get; set; }
    }
}
