using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Classifier.Views
{
    /// <summary>
    /// Interaction logic for ImagePreviewWindow.xaml
    /// </summary>
    public partial class ImagePreviewWindow : Window
    {
        public ImagePreviewWindow(Mat matImage, long score)
        {
            InitializeComponent();
            Title = "Score: " + score.ToString();
            if (matImage != null)
            {
                var image = matImage.ToImage<Bgr, Byte>();
                if (image != null)
                {
                    var ms = new MemoryStream();
                    ((System.Drawing.Bitmap)image.Bitmap).Save(ms, System.Drawing.Imaging.ImageFormat.Bmp);
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    ms.Seek(0, SeekOrigin.Begin);
                    bmp.StreamSource = ms;
                    bmp.EndInit();
                    var width = Convert.ToInt32(bmp.Width);
                    var height = Convert.ToInt32(bmp.Height);
                    ImageBox.Width = width;
                    ImageBox.Height = height;
                    ImageBox.Source = bmp;
                }
            }
        }
    }
}
