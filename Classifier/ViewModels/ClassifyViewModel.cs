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
using Classifier.Models;
using System.Data;

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
            //ClassifyCommand = new RelayCommand(async () => await ClassifyDocumentsAsync());
            ClassifyCommand = new RelayCommand(async () => await ProcessDocumentsAsync());
            BrowseForSpreadsheetCommand = new RelayCommand(BrowseForSpreadsheet);
            UniquenessThreshold = 0.60;
            KNearest = 2;
            ViewResults = false;
            Scores = new List<ImageScores>();
        }

        #region Commands
        public IRelayCommand ClassifyCommand { get; }
        public IRelayCommand BrowseForFilesCommand { get; }
        public IRelayCommand RemoveFileCommand { get; }
        public IRelayCommand BrowseForSpreadsheetCommand { get; }
        #endregion

        #region Methods
        public void BrowseForFiles()
        {
            SelectedFile = null;
            var filesDialog = Common.BrowseForFiles(true, "PDF (*.pdf)|*.pdf|Excel (*.xlsx)|*.xlsx");
            if (filesDialog.ShowDialog() != true)
                return;
            PdfFiles = new ObservableCollection<string>(filesDialog.FileNames);
        }

        public void BrowseForSpreadsheet()
        {
            var file = Common.BrowseForFiles("XLSX (*.xlsx)|*.xlsx");
            if (file.ShowDialog() != true)
                return;
            NamingSpreadsheetPath = file.FileName;
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

        public async Task ProcessDocumentsAsync()
        {
            List<DocumentTypes> types = null;
            List<DocumentCriteria> documentCriteria = null;
            using (var context = new DataContext())
            {
                types = context.DocumentTypes.ToList();
                documentCriteria = context.DocumentCriteria.ToList();
            }
            if (!string.IsNullOrWhiteSpace(NamingSpreadsheetPath))
            {
                var spreadsheetDataTable = await Common.GetSpreadsheetDataTableAsync(NamingSpreadsheetPath, "Reference");
                if (spreadsheetDataTable != null)
                {
                    NamingModels = new List<FileNamingModel>();
                    foreach (DataRow dr in spreadsheetDataTable.Rows)
                    {
                        var serial = string.Empty;
                        var tag = string.Empty;
                        if (!string.IsNullOrWhiteSpace(dr["Serial"].ToString()))
                            serial = dr["Serial"].ToString();
                        if (!string.IsNullOrWhiteSpace(dr["Tag"].ToString()))
                            tag = dr["Tag"].ToString();
                        NamingModels.Add(new FileNamingModel { Serial = serial, Tag = tag });
                    }
                }
            }
            await ConvertPdfsToImagesAsync();
            await Common.CreateCriteriaFilesAsync(documentCriteria, types);
            var criteriaDirectoryInfo = new DirectoryInfo(Common.CriteriaStorage);
            var criteriaFiles = criteriaDirectoryInfo.GetFiles();
            var tempDirectoryInfo = new DirectoryInfo(Common.TempStorage);
            var files = tempDirectoryInfo.GetFiles();
            foreach (var file in files)
            {
                var criteriaMatches = types.Select(o => new CriteriaMatchModel
                {
                    DocumentType = o
                }).ToList();
                foreach (var criteriaFile in criteriaFiles)
                {
                    var criteriaFileSplit = criteriaFile.Name.Split('-');
                    var type = types.First(c => c.DocumentType == criteriaFileSplit[0]);
                    var cropPath = file.FullName;
                    //var matches = Classify(criteriaFile.FullName, cropPath);
                    var score = Classify(criteriaFile.FullName, cropPath);
                    var existingModel = criteriaMatches.First(c => c.DocumentType == type);
                    //existingModel.Matches += matches;
                    existingModel.Score += score;
                    existingModel.PdfFile = file.FullName;
                }
                var matchedCriteria = criteriaMatches.First(c => c.Score == criteriaMatches.Max(p => p.Score));
                Console.WriteLine($@"Total Matches: {matchedCriteria.Score}");
                if (matchedCriteria.Matches > 50)
                {
                    ExtractPageFromPdf(file, matchedCriteria.DocumentType.DocumentType, NamingModels);
                }
            }
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show("Completed!");
            });
        }

        public long Classify(string criteriaPath, string obsPath)
        {
            var matches = 0;
            var score = 0L;
            using (var modelImage = CvInvoke.Imread(criteriaPath, ImreadModes.Grayscale))
            {
                using (var observedImage = CvInvoke.Imread(obsPath))
                {
                    if (UseSurf)
                    {
                        matches = Surfer.DetermineMatch(modelImage, observedImage, out _);
                        if (!ViewResults) return matches;
                        var result = Surfer.Classify(modelImage, observedImage, out _);
                        ImageViewer.Show(result);
                    }
                    else
                    {
                        score = Classifier.ProcessImage(modelImage, observedImage, UniquenessThreshold, KNearest);
                        if (!ViewResults) return score;
                        var result = Classifier.ProcessImageAndShowResult(modelImage, observedImage, UniquenessThreshold, KNearest);
                        ImageViewer.Show(result);
                        //System.Windows.MessageBox.Show($"Score: {score}");
                        //matches = Classifier.DetermineMatch(modelImage, observedImage, UniquenessThreshold, KNearest, out _);
                        //if (!ViewResults) return matches;
                        //var result = Classifier.Classify(modelImage, observedImage, UniquenessThreshold, KNearest, out _);
                        //ImageViewer.Show(result);
                    }
                }
            }
            return score;
        }

        //public async Task ClassifyDocumentsAsync()
        //{
        //    List<DocumentTypes> types = null;
        //    List<DocumentCriteria> documentCriteria = null;
        //    using (var context = new DataContext())
        //    {
        //        types = context.DocumentTypes.ToList();
        //        documentCriteria = context.DocumentCriteria.ToList();
        //    }
        //    if (!string.IsNullOrWhiteSpace(NamingSpreadsheetPath))
        //    {
        //        var spreadsheetDataTable = await Common.GetSpreadsheetDataTableAsync(NamingSpreadsheetPath, "Reference");
        //        if(spreadsheetDataTable != null)
        //        {
        //            NamingModels = new List<FileNamingModel>();
        //            foreach(DataRow dr in spreadsheetDataTable.Rows)
        //            {
        //                var serial = string.Empty;
        //                var tag = string.Empty;
        //                if (!string.IsNullOrWhiteSpace(dr["Serial"].ToString()))
        //                    serial = dr["Serial"].ToString();
        //                if (!string.IsNullOrWhiteSpace(dr["Tag"].ToString()))
        //                    tag = dr["Tag"].ToString();
        //                NamingModels.Add(new FileNamingModel { Serial = serial, Tag = tag });
        //            }
        //        }
        //    }
        //    await ConvertPdfsToImagesAsync();
        //    //var types = context.DocumentTypes.ToList();
        //    //await CreateCriteriaFilesAsync(context, types);
        //    await CreateCriteriaFilesAsync(documentCriteria, types);
        //    var criteriaDirectoryInfo = new DirectoryInfo(Common.CriteriaStorage);
        //    var criteriaFiles = criteriaDirectoryInfo.GetFiles();
        //    var tempDirectoryInfo = new DirectoryInfo(Common.TempStorage);
        //    var files = tempDirectoryInfo.GetFiles();
        //    foreach (var file in files)
        //    {
        //        var criteriaMatches = types.Select(o => new CriteriaMatchModel
        //        {
        //            DocumentType = o
        //        }).ToList();
        //        foreach (var criteriaFile in criteriaFiles)
        //        {
        //            var criteriaFileSplit = criteriaFile.Name.Split('-');
        //            var type = types.First(c => c.DocumentType == criteriaFileSplit[0]);
        //            var critName = criteriaFileSplit[1].Substring(0, criteriaFileSplit[1].Length - 4);
        //            var crit = documentCriteria.First(c => c.DocumentTypeId == type.Id && c.CriteriaName == critName);
        //            //CreateImagePart(file.FullName, crit);
        //            //var cropPath = Path.Combine(Common.TempStorage, "cropped.png");
        //            var cropPath = file.FullName;
        //            var matches = Classify(criteriaFile.FullName, cropPath);
        //            var existingModel = criteriaMatches.First(c => c.DocumentType == type);
        //            existingModel.Matches += matches;
        //            existingModel.PdfFile = file.FullName;
        //        }
        //        var matchedCriteria = criteriaMatches.First(c => c.Matches == criteriaMatches.Max(p => p.Matches));
        //        Console.WriteLine($@"Total Matches: {matchedCriteria.Matches}");
        //        if (matchedCriteria.Matches > 50)
        //        {
        //            ExtractPageFromPdf(file, matchedCriteria.DocumentType.DocumentType, NamingModels);
        //        }
        //    }
        //    System.Windows.Application.Current.Dispatcher.Invoke(() =>
        //    {
        //        System.Windows.MessageBox.Show("Completed!");
        //    });
        //}

        public static void CreateImagePart(string filePath, DocumentCriteria criteria)
        {
            var cropPath = Path.Combine(Common.TempStorage, "cropped.png");
            if (File.Exists(cropPath)) File.Delete(cropPath);
            using (var original = new Bitmap(filePath))
            {
                var originalWidth = original.Width;
                var originalHeight = original.Height;
                var positionX = criteria.PositionX;
                var positionY = criteria.PositionY;
                var scaleFactorX = 1.0;
                var scaleFactorY = 1.0;
                if ((criteria.BaseWidth != 0 && originalWidth != 0) && criteria.BaseWidth != originalWidth)
                    scaleFactorX = ConvertDimensions(criteria.BaseWidth, originalWidth);
                if ((criteria.BaseHeight != 0 && originalHeight != 0) && criteria.BaseHeight != originalHeight)
                    scaleFactorY = ConvertDimensions(criteria.BaseHeight, originalHeight);
                var scaledPositionX = criteria.PositionX / scaleFactorX;
                var scaledPositionY = criteria.PositionY / scaleFactorY;
                var scaledWidth = criteria.Width / scaleFactorX;
                var scaledHeight = criteria.Height / scaleFactorY;
                if ((scaledWidth + criteria.PositionX) > originalWidth)
                {
                    positionX = Convert.ToInt32(scaledPositionX);
                }
                if ((scaledHeight + criteria.PositionY) > originalHeight)
                {
                    positionY = Convert.ToInt32(scaledPositionY);
                }
                var scaledSize = new Size(Convert.ToInt32(scaledWidth), Convert.ToInt32(scaledHeight));
                var startPoint = new Point(positionX, positionY);
                var rect = new Rectangle(startPoint, scaledSize);
                var croppedImage = (Bitmap)original.Clone(rect, original.PixelFormat);
                croppedImage.Save(cropPath);
            }
        }

        public static double ConvertDimensions(int first, int second)
        {
            var doubleFirst = Convert.ToDouble(first);
            var doubleSecond = Convert.ToDouble(second);
            var value = doubleFirst / doubleSecond;
            return value;
        }

        

        public void ExtractPageFromPdf(FileInfo file, string type, List<FileNamingModel> models = null)
        {
            var fileName = file.Name.Substring(0, file.Name.Length - 4);
            if(models != null)
            {
                var fileNameSplit = fileName.Split('.');
                var serialNumber = fileNameSplit[0];
                var model = models.FirstOrDefault(c => c.Serial == serialNumber);
                if(model != null)
                {
                    fileName = model.Tag;
                }
            }
            var origPdf = PdfImages.First(c => c.Key == file.FullName);
            var imagePath = origPdf.Key.Replace(".png", "");
            var pageSplit = imagePath.Split('.');
            var page = Convert.ToInt32(pageSplit[pageSplit.Length - 1]);
            var loadedDocument = new PdfLoadedDocument(origPdf.Value);

            var resultPath = Path.Combine(Common.ResultsStorage, type);
            using (var document = new PdfDocument())
            {
                var startIndex = page -1;
                var endIndex = page - 1;
                document.ImportPageRange(loadedDocument, startIndex, endIndex);
                var savePath = Path.Combine(resultPath, $"{fileName}.pdf");
                if (File.Exists(savePath))
                {
                    savePath = Path.Combine(resultPath, $"{fileName}-1.pdf");
                }
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
                foreach (var file in files.Where(c=>c.Extension == ".pdf"))
                {
                    viewer.Load(file.FullName);
                    var images = viewer.ExportAsImage(0, viewer.PageCount - 1);
                    var imgCount = 1;
                    foreach (var image in images)
                    {
                        var imgPath = Path.Combine(Common.TempStorage, $"{file.Name.Substring(0, file.Name.Length - 4)}.{imgCount}.png");
                        PdfImages.Add(imgPath, file.FullName);
                        image.Save(imgPath);
                        imgCount++;
                    }
                }
            }
        }
        #endregion

        #region Fields
        public List<ImageScores> Scores
        {
            get => _scores;
            set => Set(ref _scores, value);
        }
        private List<ImageScores> _scores;

        public List<FileNamingModel> NamingModels { get; set; }
        public bool ClassifyEnabled
        {
            get => _classifyEnabled;
            set => Set(ref _classifyEnabled, value);
        }
        private bool _classifyEnabled;

        public bool ViewResults
        {
            get => _viewResults;
            set => Set(ref _viewResults, value);
        }
        private bool _viewResults;

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
            set
            {
                ClassifyEnabled = value.Count > 0 ? true : false;
                Set(ref _pdfFiles, value);
            }
        }
        private ObservableCollection<string> _pdfFiles;

        public string SelectedFile
        {
            get => _selectedFile;
            set => Set(ref _selectedFile, value);
        }
        private string _selectedFile;

        public string NamingSpreadsheetPath
        {
            get => _namingSpreadsheetPath;
            set => Set(ref _namingSpreadsheetPath, value);
        }
        private string _namingSpreadsheetPath;

        public bool UseSurf
        {
            get => _useSurf;
            set => Set(ref _useSurf, value);
        }
        private bool _useSurf;
        #endregion
    }

    public struct ImageScores
    {
        public string Path { get; set; }
        public long Score { get; set; }
        public string PdfFile { get; set; }
        public string CriteriaFile { get; set; }
    }
}
