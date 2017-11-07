using Classifier.Core;
using Classifier.Data;
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
            Classifier = new DocClassifier();
            PdfFiles = new ObservableCollection<string>();
            PdfImages = new Dictionary<string, string>();
            BrowseForFilesCommand = new RelayCommand(BrowseForFiles);
            RemoveFileCommand = new RelayCommand(RemoveSelectedFile);
            ClassifyCommand = new RelayCommand(async () => await ClassifyDocumentsAsync());
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
            await ConvertPdfsToImagesAsync();
            using(var context = new DataContext())
            {
                var types = context.DocumentTypes.ToList();
                foreach(var type in types)
                {
                    await CreateCriteriaFiles(context, type);
                    var tempDirectoryInfo = new DirectoryInfo(TempStorage);
                    var files = tempDirectoryInfo.GetFiles();
                    var criteriaDirectoryInfo = new DirectoryInfo(CriteriaStorage);
                    var criteriaFiles = criteriaDirectoryInfo.GetFiles();
                    foreach (FileInfo file in files)
                    {
                        if(file == null)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                System.Windows.MessageBox.Show("Completed!");
                            });
                            return;
                        }
                        var totalMatches = 0;
                        foreach (FileInfo criteriaFile in criteriaFiles)
                        {
                            var criteriaFileSplit = criteriaFile.Name.Split('-');
                            var critName = criteriaFileSplit[1].Substring(0, criteriaFileSplit[1].Length - 4);
                            var crit = context.DocumentCriteria.First(c => c.DocumentTypeId == type.Id && c.CriteriaName == critName);
                            CreateImagePart(file.FullName, crit);
                            var cropPath = Path.Combine(TempStorage, "cropped.png");
                            totalMatches += Classify(criteriaFile.FullName, cropPath);
                        }
                        Console.WriteLine($@"Total Matches: {totalMatches}");
                        if (totalMatches > 100)
                        {
                            ExtractPageFromPdf(file, type.DocumentType);
                        }
                    }
                }
            }
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show("Completed!");
            });
        }

        public void CreateImagePart(string filePath, DocumentCriteria criteria)
        {
            var cropPath = Path.Combine(TempStorage, "cropped.png");
            if (File.Exists(cropPath)) File.Delete(cropPath);
            using (var original = new Bitmap(filePath))
            {
                var rect = new Rectangle(new Point(criteria.PositionX, criteria.PositionY), new Size(criteria.Width, criteria.Height));
                var croppedImage = (Bitmap)original.Clone(rect, original.PixelFormat);
                croppedImage.Save(cropPath);
            }
        }

        public int Classify(string criteriaPath, string obsPath)
        {
            var matches = 0;
            using (var modelImage = CvInvoke.Imread(criteriaPath, ImreadModes.Grayscale))
            {
                using (var observedImage = CvInvoke.Imread(obsPath))
                {
                    matches = Classifier.DetermineMatch(modelImage, observedImage, UniquenessThreshold, KNearest, out _);
                    //var result = Classifier.Classify(modelImage, observedImage, UniquenessThreshold, KNearest, out _);
                    //ImageViewer.Show(result);
                }
            }
            return matches;
        }

        public Task CreateCriteriaFiles(DataContext context, DocumentTypes type)
        {
            return Task.Run(() =>
            {
                var criteriaDirectoryInfo = new DirectoryInfo(CriteriaStorage);
                var files = criteriaDirectoryInfo.GetFiles();
                foreach(FileInfo file in files)
                {
                    File.Delete(file.FullName);
                }
                var criterion = context.DocumentCriteria.Where(c => c.DocumentTypeId == type.Id).ToList();
                foreach(var criteria in criterion)
                {
                    var image = Common.ConvertStringToImage(criteria.CriteriaBytes);
                    var imagePath = Path.Combine(CriteriaStorage, $"{type.DocumentType}-{criteria.CriteriaName}.png");
                    image.Save(imagePath);
                }
            });
        }

        public void ExtractPageFromPdf(FileInfo file, string type)
        {
            var fileName = file.Name.Substring(0, file.Name.Length - 4);
            var origPdf = PdfImages.First(c => c.Key == file.FullName);
            var imagePath = origPdf.Key.Replace(".png", "");
            var pageSplit = imagePath.Split('.');
            var page = Convert.ToInt32(pageSplit[pageSplit.Length - 1]);
            var loadedDocument = new PdfLoadedDocument(origPdf.Value);
            var resultPath = Path.Combine(ResultsStorage, type);
            using (var document = new PdfDocument())
            {
                var startIndex = 0;
                var endIndex = page - 1;
                document.ImportPageRange(loadedDocument, startIndex, endIndex);
                var savePath = Path.Combine(resultPath, $"{fileName}.pdf");
                if (File.Exists(savePath)) File.Delete(savePath);
                document.Save(savePath);
                loadedDocument.Close(true);
                document.Close(true);
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
        #endregion

        #region Fields
        public string AppStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier";
        public string TempStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\temp";
        public string PdfPath = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\PDFs";
        public string CriteriaStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\Criteria";
        public string ResultsStorage = $"C:\\Users\\{Environment.UserName}\\AppData\\Local\\DocumentClassifier\\Results";

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
