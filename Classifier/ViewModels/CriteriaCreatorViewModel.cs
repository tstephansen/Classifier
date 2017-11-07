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
            CreateCriteriaCommand = new RelayCommand(CreateCriteria);
            DocumentTypeList = new List<DocumentTypes>
            {
                new DocumentTypes
                {
                    Id = 1,
                    DocumentType = "YCA Factory Calibration Certificate",
                    Criteria = new List<DocumentCriteria>()
                },
                new DocumentTypes
                {
                    Id = 2,
                    DocumentType = "M1100UT",
                    Criteria = new List<DocumentCriteria>()
                }
            };
        }

        #region Commands
        public IRelayCommand LoadImageCommand { get; }
        public IRelayCommand CreateCriteriaCommand { get; }
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
            //if(SelectedDocumentType == null || CriteriaName == string.Empty)
            //{
            //    return;
            //}
            CreateCriteriaFromImage(FilePath);
        }

        /// <summary>
        /// Rectangle location is upper left corner.
        /// </summary>
        /// <param name="filePath"></param>
        public void CreateCriteriaFromImage(string filePath)
        {
            var savePath = Path.Combine(Common.TempStorage, "criteria.png");
            using (var original = new Bitmap(filePath))
            {
                var originalWidth = original.Width;
                var originalHeight = original.Height;
                var scaleFactorX = PreviewImageWidth / originalWidth;
                var scaleFactorY = PreviewImageHeight / originalHeight;
                var userWidth = ReleasePosition.X - InitialPosition.X;
                var userHeight = ReleasePosition.Y - InitialPosition.Y;
                var scaledWidth = userWidth / scaleFactorX;
                var scaledHeight = userHeight / scaleFactorY;
                var scaledSize = new Size(Convert.ToInt32(scaledWidth), Convert.ToInt32(scaledHeight));
                var startPoint = new Point(Convert.ToInt32(InitialPosition.X), Convert.ToInt32(InitialPosition.Y));
                var rect = new Rectangle(startPoint, scaledSize);
                var cropped = (Bitmap)original.Clone(rect, original.PixelFormat);
                cropped.Save(savePath);
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

        public double PreviewImageWidth { get; set; }
        public double PreviewImageHeight { get; set; }
        #endregion
    }
}
