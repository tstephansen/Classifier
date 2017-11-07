using System;
using System.Linq;

namespace Classifier.Models
{
    public class DocumentCriteria
    {
        public string CriteriaName { get; set; }
        public virtual long DocumentTypeId { get; set; }
        public virtual DocumentTypes DocumentType { get; set; }
    }
}
