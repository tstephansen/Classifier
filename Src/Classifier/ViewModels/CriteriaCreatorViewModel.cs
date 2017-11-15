using Classifier.Core;
using Classifier.Data;
using LandmarkDevs.Core.Infrastructure;
using LandmarkDevs.Core.Shared;
using Syncfusion.Windows.Forms.PdfViewer;
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
            LoadImageCommand = new RelayCommand(LoadFile);
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
            using(var context = new ClassifierContext())
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

        public void LoadFile()
        {
            var files = Common.BrowseForFiles("PDF (*.pdf)|*.pdf|PNG (*.png)|*.png");
            if (files.ShowDialog() != true)
                return;
            var fileInfo = new FileInfo(files.FileName);
            FilePath = files.FileName;
            if (fileInfo.Extension == ".pdf")
            {
                if (string.IsNullOrWhiteSpace(PdfPageNumber)) return;
                PageNumber = Convert.ToInt32(PdfPageNumber);
                var imgPath = ConvertPdfToImage(fileInfo.FullName);
                FilePath = imgPath;
            }
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

        public string ConvertPdfToImage(string inputFilePath)
        {
            var imgPath = string.Empty;
            using (var viewer = new PdfDocumentView())
            {
                var file = new FileInfo(inputFilePath);
                viewer.Load(file.FullName);
                var images = viewer.ExportAsImage(PageNumber - 1, PageNumber - 1);
                imgPath = Path.Combine(Common.TempStorage, $"{file.Name.Substring(0, file.Name.Length - 4)}.png");
                var resizedPath = Path.Combine(Common.UserCriteriaStorage, $"{file.Name.Substring(0, file.Name.Length - 4)}-R.png");
                var image = images[0];
                if (File.Exists(imgPath)) File.Delete(imgPath);
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
            return imgPath;
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
                var userWidth = 0.0;
                var userHeight = 0.0;
                var scaledPositionX = 0.0;
                var scaledPositionY = 0.0;
                if(ReleasePosition.X < InitialPosition.X)
                {
                    userWidth = InitialPosition.X - ReleasePosition.X;
                    scaledPositionX = ReleasePosition.X / scaleFactorX;
                }
                else
                {
                    userWidth = ReleasePosition.X - InitialPosition.X;
                    scaledPositionX = InitialPosition.X / scaleFactorX;
                }
                if(ReleasePosition.Y < InitialPosition.Y)
                {
                    userHeight = InitialPosition.Y - ReleasePosition.Y;
                    scaledPositionY = ReleasePosition.Y / scaleFactorY;
                }
                else
                {
                    userHeight = ReleasePosition.Y - InitialPosition.Y;
                    scaledPositionY = InitialPosition.Y / scaleFactorY;
                }
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
            return saved;
        }

        public int AddCriteriaToDatabase(string imageBytes, int expandedWidth, int expandedHeight, int expandedX, int expandedY, int origW, int origH)
        {
            using(var context = new ClassifierContext())
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

        public string PdfPageNumber
        {
            get => _pdfPageNumber;
            set => Set(ref _pdfPageNumber, value);
        }
        private string _pdfPageNumber;

        public int PageNumber
        {
            get => _pageNumber;
            set => Set(ref _pageNumber, value);
        }
        private int _pageNumber;
        #endregion
    }
}
