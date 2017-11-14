using System;
using System.Drawing;

namespace Classifier.Models
{
    public class ImageCriteriaModel
    {
        public Point MouseDownPosition { get; set; }
        public Point MouseUpPosition { get; set; }
        public double SelectionWidth { get; set; }
        public double SelectionHeight { get; set; }

        public Size SelectionSize => new Size(Convert.ToInt32(SelectionWidth), Convert.ToInt32(SelectionHeight));
    }
}
