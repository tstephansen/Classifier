using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Models
{
    public class ImageCriteriaModel
    {
        public Point MouseDownPosition { get; set; }
        public Point MouseUpPosition { get; set; }
        public double SelectionWidth { get; set; }
        public double SelectionHeight { get; set; }

        public Size SelectionSize
        {
            get
            {
                return new Size(Convert.ToInt32(SelectionWidth), Convert.ToInt32(SelectionHeight));
            }
        }
    }
}
