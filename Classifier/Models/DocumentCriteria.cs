using System;
using System.Linq;

namespace Classifier.Models
{
    public class DocumentCriteria
    {
        public string CriteriaName { get; set; }
        public virtual Guid DocumentTypeId { get; set; }
        public virtual DocumentTypes DocumentType { get; set; }
    }
}
