using Classifier.Core;
using Classifier.Models;
using LandmarkDevs.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.ViewModels
{
    public class CriteriaCreatorViewModel : Observable
    {
        public CriteriaCreatorViewModel()
        {
            LoadImageCommand = new RelayCommand(LoadImage);
        }

        #region Commands
        public IRelayCommand LoadImageCommand { get; }
        #endregion

        #region Methods
        public void LoadImage()
        {
            var files = Common.BrowseForFiles("PNG (*.png)|*.png");
            if (files.ShowDialog() != true)
                return;
            FilePath = files.FileName;
            System.Windows.Media.Imaging.BitmapImage bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(FilePath);
            bmp.EndInit();
            var width = Convert.ToInt32(bmp.Width);
            var height = Convert.ToInt32(bmp.Height);
            ImageSize = new Size(width, height);
            ImageSource = bmp;
        }

        public void CreateCriteria()
        {
            if(InitialPosition == null || ReleasePosition == null || SelectedDocumentType == null || CriteriaName == string.Empty)
            {
                return;
            }
            CreateCriteriaFromImage(FilePath);
        }

        public void CreateCriteriaFromImage(string filePath)
        {
            using (var original = new Bitmap(filePath))
            {
                var savePath = Path.Combine(Common.TempStorage, "criteria.png");
                var rect = new Rectangle(new Point(Convert.ToInt32(InitialPosition.X), Convert.ToInt32(InitialPosition.Y)), SelectionSize);
                var cropped = (Bitmap)original.Clone(rect, original.PixelFormat);
                cropped.Save(savePath);

                //var dpW = Convert.ToInt32(original.Width / 3.06);
                //var dpH = Convert.ToInt32(original.Height / 6.6);
                //var dpX = Convert.ToInt32(original.Width / 31.19);
                //var dpY = Convert.ToInt32(original.Height / 18.48);
                //var dpPoint = new Point(dpX, dpY);
                //var dpSize = new Size(dpW, dpH);
                //// Header Dimensions
                //var headX = Convert.ToInt32(original.Width / 3.05);
                //var headY = Convert.ToInt32(original.Height / 12.57);
                //var headW = Convert.ToInt32(original.Width / 2.8);
                //var headerRect = new Rectangle(new Point(headX, headY), new Size(headW, dpH));
                //var dpHarpRect = new Rectangle(dpPoint, dpSize);
                //var dpHarpCropped = (Bitmap)original.Clone(dpHarpRect, original.PixelFormat);
                //dpHarpCropped.Save(dpHarpTestPath, ImageFormat.Png);
                //var headerCropped = (Bitmap)original.Clone(headerRect, original.PixelFormat);
                //headerCropped.Save(headerTestPath, ImageFormat.Png);
            }
        }
        #endregion

        #region Fields
        public string FilePath
        {
            get => _filePath;
            set => Set(ref _filePath, value);
        }
        private string _filePath;

        public System.Windows.Media.Imaging.BitmapImage ImageSource
        {
            get => _imageSource;
            set => Set(ref _imageSource, value);
        }
        private System.Windows.Media.Imaging.BitmapImage _imageSource;

        public Size ImageSize
        {
            get => _imageSize;
            set => Set(ref _imageSize, value);
        }
        private Size _imageSize;

        public System.Windows.Point InitialPosition
        {
            get => _initialPosition;
            set => Set(ref _initialPosition, value);
        }
        private System.Windows.Point _initialPosition;

        public System.Windows.Point ReleasePosition
        {
            get => _releasePosition;
            set => Set(ref _releasePosition, value);
        }
        private System.Windows.Point _releasePosition;

        public Size SelectionSize
        {
            get => _selectionSize;
            set => Set(ref _selectionSize, value);
        }
        private Size _selectionSize;

        public List<DocumentTypes> DocumentTypeList
        {
            get => _documentTypeList;
            set => Set(ref _documentTypeList, value);
        }
        private List<DocumentTypes> _documentTypeList;

        public DocumentTypes SelectedDocumentType
        {
            get => _selectedDocumentType;
            set => Set(ref _selectedDocumentType, value);
        }
        private DocumentTypes _selectedDocumentType;

        public string CriteriaName
        {
            get => _criteriaName;
            set => Set(ref _criteriaName, value);
        }
        private string _criteriaName;

        public Point SelectionLocation
        {
            get => _selectionLocation;
            set => Set(ref _selectionLocation, value);
        }
        private Point _selectionLocation;
        #endregion
    }
}
