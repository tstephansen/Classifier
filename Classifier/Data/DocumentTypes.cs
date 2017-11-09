using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Data
{
    public class DocumentTypes
    {
        public DocumentTypes()
        {
            Criteria = new List<DocumentCriteria>();
        }

        public Guid Id { get; set; }
        public string DocumentType { get; set; }
        public long MinScore { get; set; }
        public long AverageScore { get; set; }
        public long MaxScore { get; set; }
        public virtual ICollection<DocumentCriteria> Criteria { get; set; }
    }
}
