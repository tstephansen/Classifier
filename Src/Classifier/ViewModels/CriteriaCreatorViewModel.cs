using Classifier.Core;
using Classifier.Data;
using LandmarkDevs.Core.Infrastructure;
using LandmarkDevs.Core.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            RefreshDocTypesCommand = new RelayCommand(RefreshDocTypes);
            SavedDialogOpen = false;
            RefreshDocTypes();
        }

        #region Commands
        public IRelayCommand LoadImageCommand { get; }
        public IRelayCommand CreateCriteriaCommand { get; }
        public IRelayCommand RefreshDocTypesCommand { get; }
        public IRelayCommand CloseDialogCommand { get; }
        #endregion

        #region Methods
        public void CloseDialog()
        {
            SavedDialogOpen = false;
        }

        public void RefreshDocTypes()
        {
            using(var context = new DataContext())
            {
                var docTypes = context.DocumentTypes.ToList();
                DocumentTypeList = new ObservableCollection<DocumentTypes>(docTypes);
                foreach (var type in DocumentTypeList)
                {
                    var resultPath = Path.Combine(Common.ResultsStorage, type.DocumentType);
                    if (!Directory.Exists(resultPath)) Directory.CreateDirectory(resultPath);
                }
            }
        }

        public void LoadImage()
        {
            var files = Common.BrowseForFiles("PNG (*.png)|*.png");
            if (files.ShowDialog() != true)
                return;
            FilePath = files.FileName;
            var bmp = new System.Windows.Media.Imaging.BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(FilePath);
            bmp.EndInit();
            var width = Convert.ToInt32(bmp.Width);
            var height = Convert.ToInt32(bmp.Height);
            ImageSize = new Size(width, height);
            ImageSource = bmp;
        }

        public void LoadCriteriaResult(string filePath)
        {
            ImageSource = new System.Windows.Media.Imaging.BitmapImage();
            ImageSource.BeginInit();
            ImageSource.UriSource = new Uri(filePath);
            ImageSource.EndInit();
            var width = Convert.ToInt32(ImageSource.Width);
            var height = Convert.ToInt32(ImageSource.Height);
            ImageSize = new Size(width, height);
        }

        public void CreateCriteria()
        {
            if (SelectedDocumentType == null || CriteriaName == string.Empty)
            {
                return;
            }
            ImageSource = null;
            ImageSize = new Size(0,0);
            var saved = CreateCriteriaFromImage(FilePath);
            if (saved < 1)
            {
                SavedDialogTitle = "Error";
                SavedDialogText = "There was an error saving the criteria.";
                SavedDialogOpen = true;
            }
            else
            {
                CriteriaName = null;
                SavedDialogTitle = "Saved";
                SavedDialogText = "The criteria was saved successfully";
                SavedDialogOpen = true;
            }
        }

        /// <summary>
        /// Rectangle location is upper left corner.
        /// </summary>
        /// <param name="filePath"></param>
        public int CreateCriteriaFromImage(string filePath)
        {
            var saved = 0;
            var savePath = Path.Combine(Common.TempStorage, "criteria.png");
            if (File.Exists(savePath)) File.Delete(savePath);
            using (var original = new Bitmap(filePath))
            {
                var originalWidth = original.Width;
                var originalHeight = original.Height;
                var scaleFactorX = PreviewImageWidth / originalWidth;
                var scaleFactorY = PreviewImageHeight / originalHeight;
                var userWidth = ReleasePosition.X - InitialPosition.X;
                var userHeight = ReleasePosition.Y - InitialPosition.Y;
                var scaledPositionX = InitialPosition.X / scaleFactorX;
                var scaledPositionY = InitialPosition.Y / scaleFactorY;
                var scaledWidth = userWidth / scaleFactorX;
                var scaledHeight = userHeight / scaleFactorY;
                var scaledSize = new Size(Convert.ToInt32(scaledWidth), Convert.ToInt32(scaledHeight));
                var startPoint = new Point(Convert.ToInt32(scaledPositionX), Convert.ToInt32(scaledPositionY));
                var expandedWidth = Convert.ToInt32(scaledWidth + 80);
                var expandedHeight = Convert.ToInt32(scaledHeight + 80);
                var expandedX = startPoint.X - 40;
                var expandedY = startPoint.Y - 40;
                if (expandedX < 0) expandedX = 0;
                if (expandedY < 0) expandedY = 0;
                var rect = new Rectangle(startPoint, scaledSize);
                var cropped = (Bitmap)original.Clone(rect, original.PixelFormat);
                cropped.Save(savePath);
                var imageString = Common.CreateStringFromImage(savePath);
                saved = AddCriteriaToDatabase(imageString, expandedWidth, expandedHeight, expandedX, expandedY, originalWidth, originalHeight);
            }
            //LoadCriteriaResult(savePath);
            return saved;
        }

        public int AddCriteriaToDatabase(string imageBytes, int expandedWidth, int expandedHeight, int expandedX, int expandedY, int origW, int origH)
        {
            using(var context = new DataContext())
            {
                context.DocumentCriteria.Add(new DocumentCriteria
                {
                    CriteriaName = CriteriaName,
                    DocumentTypeId = SelectedDocumentType.Id,
                    CriteriaBytes = imageBytes,
                    PositionX = expandedX,
                    PositionY = expandedY,
                    Width = expandedWidth,
                    Height = expandedHeight,
                    BaseWidth = origW,
                    BaseHeight = origH,
                    MatchThreshold = 74,
                    Id = GuidGenerator.GenerateTimeBasedGuid()
                });
                return context.SaveChanges();
            }
        }
        #endregion

        #region Fields
        public bool SavedDialogOpen
        {
            get => _savedDialogOpen;
            set => Set(ref _savedDialogOpen, value);
        }
        private bool _savedDialogOpen;

        public string SavedDialogText
        {
            get => _savedDialogText;
            set => Set(ref _savedDialogText, value);
        }
        private string _savedDialogText;

        public string SavedDialogTitle
        {
            get => _savedDialogTitle;
            set => Set(ref _savedDialogTitle, value);
        }
        private string _savedDialogTitle;

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

        public ObservableCollection<DocumentTypes> DocumentTypeList
        {
            get => _documentTypeList;
            set => Set(ref _documentTypeList, value);
        }
        private ObservableCollection<DocumentTypes> _documentTypeList;

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
