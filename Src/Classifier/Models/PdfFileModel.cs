using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Models
{
    public class PdfFileModel
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public long Matches { get; set; }
        public string Display => $"{Name} - Matches: {Matches}";
    }
}
