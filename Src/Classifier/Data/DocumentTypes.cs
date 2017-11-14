using System;
using System.Collections.Generic;

namespace Classifier.Data
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
    public partial class DocumentTypes
    {
        
        public DocumentTypes()
        {
            DocumentCriteria = new HashSet<DocumentCriteria>();
        }

        public Guid Id { get; set; }

        public string DocumentType { get; set; }

        public long AverageScore { get; set; }

        public virtual ICollection<DocumentCriteria> DocumentCriteria { get; set; }
    }
}
