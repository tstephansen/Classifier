using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Core
{
    public static class Common
    {
        public static OpenFileDialog BrowseForFiles(string filter)
        {
            return BrowseForFiles(false, filter);
        }

        public static OpenFileDialog BrowseForFiles(bool multiSelect)
        {
            return BrowseForFiles(multiSelect, null);
        }

        public static OpenFileDialog BrowseForFiles(bool multiSelect = false, string filter = null)
        {
            filter = filter == null ? "All files (*.*)|*.*" : $"{filter}|All files (*.*)|*.*";
            var filesDialog = new OpenFileDialog
            {
                Filter = filter,
                FilterIndex = 1,
                RestoreDirectory = true,
                FileName = "",
                Multiselect = multiSelect
            };
            return filesDialog;
        }

        public static void Resize(string imageFile, string outputFile, double scaleFactor)
        {
            using (var srcImage = Image.FromFile(imageFile))
            {
                var newWidth = (int)(srcImage.Width * scaleFactor);
                var newHeight = (int)(srcImage.Height * scaleFactor);
                using (var newImage = new Bitmap(newWidth, newHeight))
                using (var graphics = Graphics.FromImage(newImage))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(srcImage, new Rectangle(0, 0, newWidth, newHeight));
                    newImage.Save(outputFile);
                }
            }
        }

        public static Bitmap ConvertStringToImage(string base64String)
        {
            Image image;
            var bytes = Convert.FromBase64String(base64String);
            using (var ms = new MemoryStream(bytes))
            {
                image = Image.FromStream(ms);
            }
            return (Bitmap)image;
        }

        public static string CreateStringFromImage(string filePath)
        {
            using (Image image = Image.FromFile(filePath))
            {
                using (MemoryStream m = new MemoryStream())
                {
                    image.Save(m, image.RawFormat);
                    var imageBytes = m.ToArray();
                    var base64String = Convert.ToBase64String(imageBytes);
                    return base64String;
                }
            }
        }

        public static string AppStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier";
        public static string TempStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\temp";
        public static string PdfPath = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\PDFs";
    }
}
