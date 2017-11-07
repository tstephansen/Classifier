using Classifier.Core;
using LandmarkDevs.Core.Infrastructure;
using Syncfusion.Windows.Forms.PdfViewer;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.ViewModels
{
    public class HelpersViewModel : Observable
    {
        public HelpersViewModel()
        {
            PageNumber = 1;
            BrowseCommand = new RelayCommand(Browse);
            ConvertPdfToImageCommand = new RelayCommand(ConvertPdfToImage);
        }

        #region Commands
        public IRelayCommand BrowseCommand { get; }
        public IRelayCommand ConvertPdfToImageCommand { get; }
        #endregion

        #region Methods
        public void Browse()
        {
            var files = Common.BrowseForFiles("PDF (*.pdf)|*.pdf");
            if (files.ShowDialog() != true)
                return;
            InputFilePath = files.FileName;
        }

        public void ConvertPdfToImage()
        {
            using (var viewer = new PdfDocumentView())
            {
                var file = new FileInfo(InputFilePath);
                viewer.Load(file.FullName);
                if (((PageNumber - 1) < 0) || (PageNumber - 1) > viewer.PageCount) return;
                var images = viewer.ExportAsImage(PageNumber - 1, PageNumber - 1);
                var imgPath = Path.Combine(Common.TempStorage, $"{file.Name.Substring(0, file.Name.Length - 4)}.png");
                var resizedPath = Path.Combine(Common.TempStorage, $"{file.Name.Substring(0, file.Name.Length - 4)}-R.png");
                var image = images[0];
                image.Save(imgPath);
                double scaleFactor = 0;
                var resize = false;
                using (var bmp = Image.FromFile(imgPath))
                {
                    if (bmp.Size.Width > 1428)
                    {
                        scaleFactor = 1428.0 / Convert.ToDouble(bmp.Size.Width);
                        resize = true;
                    }
                }
                if (resize) Common.Resize(imgPath, resizedPath, scaleFactor);
            }
        }
        #endregion

        #region Fields
        public string InputFilePath
        {
            get => _inputFilePath;
            set => Set(ref _inputFilePath, value);
        }
        private string _inputFilePath;

        public int PageNumber
        {
            get => _pageNumber;
            set => Set(ref _pageNumber, value);
        }
        private int _pageNumber;
        #endregion
    }
}
