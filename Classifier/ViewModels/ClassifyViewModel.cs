using Classifier.Core;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.UI;
using LandmarkDevs.Core.Infrastructure;
using Microsoft.Win32;
using MoreLinq;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;
using Syncfusion.Windows.Forms.PdfViewer;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.ViewModels
{
    public class ClassifyViewModel : Observable
    {
        public ClassifyViewModel()
        {
            BrowseForFilesCommand = new RelayCommand(BrowseForFiles);
            RemoveFileCommand = new RelayCommand(RemoveSelectedFile);
            ClassifyCommand = new RelayCommand(async () => await ClassifyDocumentsAsync());
            PdfFiles = new ObservableCollection<string>();
            UniquenessThreshold = 0.60;
            KNearest = 2;
        }

        #region Commands
        public IRelayCommand ClassifyCommand { get; }
        public IRelayCommand BrowseForFilesCommand { get; }
        public IRelayCommand RemoveFileCommand { get; }
        #endregion

        #region Methods
        public void BrowseForFiles()
        {
            SelectedFile = null;
            var filesDialog = Common.BrowseForFiles(true);
            if (filesDialog.ShowDialog() != true)
                return;
            PdfFiles = new ObservableCollection<string>(filesDialog.FileNames);
        }

        public void RemoveSelectedFile()
        {
            if (SelectedFile == null)
            {
                System.Windows.MessageBox.Show("Please select a file to remove.", "No File Selected");
                return;
            }
            PdfFiles.Remove(SelectedFile);
            SelectedFile = null;
        }

        public async Task ClassifyDocumentsAsync()
        {
            await Task.Run(ClassifyDocuments);
        }

        public async Task ClassifyDocuments()
        {
            var dpHarpTest = Path.Combine(TempStorage, "dpharptest.png");
            var headerTest = Path.Combine(TempStorage, "headertest.png");
            var dpHarpModel = Path.Combine(AppStorage, "dpharp.png");
            var headerModel = Path.Combine(AppStorage, "header.png");
            await ConvertPdfsToImagesAsync();
            var directoryInfo = new DirectoryInfo(TempStorage);
            var files = directoryInfo.GetFiles();
            foreach (FileInfo file in files)
            {
                if (file.Name == "dpharp.png" || file.Name == "header.png") continue;
                CreateImageParts(file.FullName);
                var totalMatches = 0;
                using (var modelImage = CvInvoke.Imread(headerModel, ImreadModes.Grayscale))
                {
                    using (var observedImage = CvInvoke.Imread(headerTest))
                    {
                        var matches = Classifier.DetermineMatch(modelImage, observedImage, UniquenessThreshold, KNearest, out _);
                        totalMatches += matches;
                        var result = Classifier.Classify(modelImage, observedImage, UniquenessThreshold, KNearest, out _);
                        ImageViewer.Show(result);
                    }
                }
                using (var modelImage = CvInvoke.Imread(dpHarpModel, ImreadModes.Grayscale))
                {
                    using (var observedImage = CvInvoke.Imread(dpHarpTest))
                    {
                        var matches = Classifier.DetermineMatch(modelImage, observedImage, UniquenessThreshold, KNearest, out _);
                        totalMatches += matches;
                    }
                }
                Console.WriteLine($@"Total Matches: {totalMatches}");
                if (totalMatches > 50)
                {
                    ExtractPageFromPdf(file);
                }
            }
        }

        public async Task ConvertPdfsToImagesAsync()
        {
            await Task.Run(() => ConvertPdfsToImages());
        }

        public void ConvertPdfsToImages()
        {
            using (var viewer = new PdfDocumentView())
            {
                var files = new List<FileInfo>();
                PdfFiles.ForEach(c => files.Add(new FileInfo(c)));
                foreach (var file in files)
                {
                    viewer.Load(file.FullName);
                    var images = viewer.ExportAsImage(0, viewer.PageCount - 1);
                    var imgCount = 1;
                    foreach (var image in images)
                    {
                        var imgPath = Path.Combine(TempStorage, $"{file.Name.Substring(0, file.Name.Length - 4)}.{imgCount}.png");
                        PdfImages.Add(imgPath, file.FullName);
                        image.Save(imgPath);
                        imgCount++;
                    }
                }
            }
        }

        public void DeleteImageParts()
        {
            var testOnePath = Path.Combine(TempStorage, "testone.png");
            var testTwoPath = Path.Combine(TempStorage, "testtwo.png");
            if (File.Exists(testOnePath))
                File.Delete(testOnePath);
            if (File.Exists(testTwoPath))
                File.Delete(testTwoPath);
        }

        public void CreateImageParts(string filePath)
        {
            var dpHarpTestPath = Path.Combine(TempStorage, "testone.png");
            var testTwoPath = Path.Combine(TempStorage, "testtwo.png");
            DeleteImageParts();
            using (var original = new Bitmap(filePath))
            {
                var dpW = Convert.ToInt32(original.Width / 3.06);
                var dpH = Convert.ToInt32(original.Height / 6.6);
                var dpX = Convert.ToInt32(original.Width / 31.19);
                var dpY = Convert.ToInt32(original.Height / 18.48);
                var dpPoint = new Point(dpX, dpY);
                var dpSize = new Size(dpW, dpH);
                // Header Dimensions
                var headX = Convert.ToInt32(original.Width / 3.05);
                var headY = Convert.ToInt32(original.Height / 12.57);
                var headW = Convert.ToInt32(original.Width / 2.8);
                var testOneRect = new Rectangle(dpPoint, dpSize);
                var testTwoRect = new Rectangle(new Point(headX, headY), new Size(headW, dpH));

                var testOneCropped = (Bitmap)original.Clone(testOneRect, original.PixelFormat);
                testOneCropped.Save(dpHarpTestPath, ImageFormat.Png);
                var testTwoCropped = (Bitmap)original.Clone(testTwoRect, original.PixelFormat);
                testTwoCropped.Save(testTwoPath, ImageFormat.Png);
            }
        }

        public void ExtractPageFromPdf(FileInfo file)
        {
            var fileName = file.Name.Substring(0, file.Name.Length - 4);
            var origPdf = PdfImages.First(c => c.Key == file.FullName);
            var imagePath = origPdf.Key.Replace(".png", "");
            var pageSplit = imagePath.Split('.');
            var page = Convert.ToInt32(pageSplit[pageSplit.Length - 1]);
            var loadedDocument = new PdfLoadedDocument(origPdf.Value);
            using (var document = new PdfDocument())
            {
                var startIndex = 0;
                var endIndex = page - 1;
                document.ImportPageRange(loadedDocument, startIndex, endIndex);
                var savePath = Path.Combine(PdfPath, $"{fileName}.pdf");
                document.Save(savePath);
                loadedDocument.Close(true);
                document.Close(true);
            }
        }
        #endregion

        #region Fields
        public string AppStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier";
        public string TempStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\temp";
        public string PdfPath = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\PDFs";

        public Dictionary<string, string> PdfImages { get; set; }

        public DocClassifier Classifier { get; set; }

        public double UniquenessThreshold
        {
            get => _uniquenessThreshold;
            set => Set(ref _uniquenessThreshold, value);
        }
        private double _uniquenessThreshold;

        public int KNearest
        {
            get => _kNearest;
            set => Set(ref _kNearest, value);
        }
        private int _kNearest;

        public ObservableCollection<string> PdfFiles
        {
            get => _pdfFiles;
            set => Set(ref _pdfFiles, value);
        }
        private ObservableCollection<string> _pdfFiles;

        public string SelectedFile
        {
            get => _selectedFile;
            set => Set(ref _selectedFile, value);
        }
        private string _selectedFile;
        #endregion
    }
}
