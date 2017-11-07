using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Models
{
    public class DocumentTypes
    {
        public DocumentTypes()
        {
            Criteria = new List<DocumentCriteria>();
        }

        public long Id { get; set; }
        public string DocumentType { get; set; }
        public virtual ICollection<DocumentCriteria> Criteria { get; set; }
    }
}
