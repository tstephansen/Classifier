using Classifier.Core;
using LandmarkDevs.Core.Infrastructure;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.ViewModels
{
    public class ImageResizerViewModel : Observable
    {
        public ImageResizerViewModel()
        {
            BrowseForImageCommand = new RelayCommand(GetImageToResize);
            ResizeImageCommand = new RelayCommand(ResizeImage);
        }

        #region Commands
        public IRelayCommand BrowseForImageCommand { get; }
        public IRelayCommand ResizeImageCommand { get; }
        #endregion

        #region Methods
        public void GetImageToResize()
        {
            var imagePath = LocateFile();
            if (string.IsNullOrEmpty(imagePath)) return;
            ResizeImagePath = imagePath;
        }

        public static string LocateFile()
        {
            var filesDialog = new OpenFileDialog
            {
                Filter = "PDF (*.pdf)|*.pdf|All files (*.*)|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                FileName = "",
                Multiselect = false
            };
            if (filesDialog.ShowDialog() != true)
                return string.Empty;
            return filesDialog.FileName;
        }

        public void ResizeImage()
        {
            if (string.IsNullOrWhiteSpace(ResizeImagePath)) return;
            var asy = Assembly.GetEntryAssembly();
            var asyLoc = asy.Location.Split('\\');
            var localDir = "C:";
            for (var i = 1; i < asyLoc.Length - 1; i++)
            {
                localDir = $"{localDir}\\{asyLoc[i]}";
            }
            var fi = new FileInfo(ResizeImagePath);
            var outputPath = $"{localDir}\\{fi.Name.Substring(0, fi.Name.Length - 4)}-R.png";
            double scaleFactor = 0;
            using (var bmp = Image.FromFile(ResizeImagePath))
            {
                if (bmp.Size.Width > 1428)
                {
                    scaleFactor = 1428.0 / Convert.ToDouble(bmp.Size.Width);
                }
            }
            Common.Resize(ResizeImagePath, outputPath, scaleFactor);
        }
        #endregion

        #region Fields
        public string ResizeImagePath
        {
            get => _resizeImagePath;
            set => Set(ref _resizeImagePath, value);
        }
        private string _resizeImagePath;
        #endregion
    }
}
