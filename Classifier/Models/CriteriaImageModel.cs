using Emgu.CV;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Models
{
    public class CriteriaImageModel
    {
        public FileInfo Info { get; set; }
        public Mat Image { get; set; }
    }
}
