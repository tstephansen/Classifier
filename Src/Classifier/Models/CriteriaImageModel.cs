using Emgu.CV;
using Emgu.CV.Util;
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
        public UMat Image { get; set; }
        public Mat ModelDescriptors { get; set; }
        public VectorOfKeyPoint ModelKeyPoints { get; set; }
    }
}
