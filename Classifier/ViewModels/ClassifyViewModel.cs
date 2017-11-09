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
using LandmarkDevs.Core.Shared;
using System.Threading;

namespace Classifier.ViewModels
{
    public class ClassifyViewModel : Observable
    {
        public ClassifyViewModel()
        {
            Classifier = new DocClassifier();
            PdfFiles = new ObservableCollection<string>();
            _matchedFiles = new List<CriteriaMatchModel>();
            PdfFiles.CollectionChanged += PdfFiles_CollectionChanged;
            PdfImages = new Dictionary<string, string>();
            BrowseForFilesCommand = new RelayCommand(BrowseForFiles);
            RemoveFileCommand = new RelayCommand(RemoveSelectedFile);
            ClassifyCommand = new RelayCommand(async () => await ProcessDocumentsAsync());
            BrowseForSpreadsheetCommand = new RelayCommand(BrowseForSpreadsheet);
            ResultsFolderCommand = new RelayCommand(GotoResultsFolder);
            ReloadDocumentTypesCommand = new RelayCommand(LoadDocumentTypes);
            CancelClassifyCommand = new RelayCommand(CancelClassify);
            DocumentSelectionList = new ObservableCollection<DocumentSelectionModel>();
            UniquenessThreshold = 0.60;
            KNearest = 2;
            ViewResults = false;
            Scores = new List<ImageScores>();
            SelectedDetectionMethod = 0;
            LoadDocumentTypes();
        }

        private void PdfFiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ClassifyEnabled = PdfFiles.Count > 0 ? true : false;
        }

        #region Commands
        public IRelayCommand ClassifyCommand { get; }
        public IRelayCommand BrowseForFilesCommand { get; }
        public IRelayCommand RemoveFileCommand { get; }
        public IRelayCommand BrowseForSpreadsheetCommand { get; }
        public IRelayCommand ResultsFolderCommand { get; }
        public IRelayCommand ReloadDocumentTypesCommand { get; }
        public IRelayCommand CancelClassifyCommand { get; }
        #endregion

        #region Methods
        public void CancelClassify()
        {
            CancelTokenSource.Cancel();
            CancelEnabled = false;
            ClassifyEnabled = true;
        }

        public void LoadDocumentTypes()
        {
            using(var context = new DataContext())
            {
                var documentTypes = context.DocumentTypes.ToList();
                foreach(var o in documentTypes)
                {
                    DocumentSelectionList.Add(new DocumentSelectionModel { DocumentTypeId = o.Id, DocumentType = o.DocumentType, Selected = true });
                }
            }
        }

        public void BrowseForFiles()
        {
            SelectedFile = null;
            var filesDialog = Common.BrowseForFiles(true, "PDF (*.pdf)|*.pdf|Excel (*.xlsx)|*.xlsx");
            if (filesDialog.ShowDialog() != true)
                return;
            foreach(var o in filesDialog.FileNames)
            {
                PdfFiles.Add(o);
            }
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

        public void CreateCriteriaArrays(FileInfo[] criteriaFiles)
        {
            _criteriaImages = new List<CriteriaImageModel>();
            foreach (var o in criteriaFiles)
            {
                var image = CvInvoke.Imread(o.FullName, ImreadModes.Grayscale);
                _criteriaImages.Add(new CriteriaImageModel
                {
                    Info = o,
                    Image = image
                });
            }
        }

        public Task CopyImagesToTempFolderAsync()
        {
            return Task.Run(() =>
            {
                var files = new List<FileInfo>();
                PdfFiles.ForEach(c => files.Add(new FileInfo(c)));
                foreach (var file in files.Where(c => c.Extension == ".png"))
                {
                    var copyPath = Path.Combine(Common.TempStorage, file.Name);
                    File.Copy(file.FullName, copyPath);
                    PdfImages.Add(copyPath, file.FullName);
                }
            });
        }

        public async Task ProcessDocumentsAsync()
        {
            CancelEnabled = true;
            ClassifyEnabled = false;
            CancelTokenSource = new CancellationTokenSource();
            var token = CancelTokenSource.Token;
            var prog = new Progress<TaskStatusHelper>();
            prog.ProgressChanged += (sender, exportProgress) =>
            {
                ProgressPercentage = Math.Round(exportProgress.ProgressPercentage, 2);
                ProgressText = exportProgress.ProgressText;
            };
            await ConvertPdfsToImagesAsync();
            await CopyImagesToTempFolderAsync();
            var types = new List<DocumentTypes>();
            List<DocumentCriteria> documentCriteria = null;
            using (var context = new DataContext())
            {
                var dTypes = context.DocumentTypes.ToList();
                foreach (var o in dTypes)
                {
                    if (DocumentSelectionList.First(c => c.DocumentTypeId == o.Id).Selected)
                        types.Add(o);
                }
                documentCriteria = context.DocumentCriteria.ToList();
            }
            await Common.CreateCriteriaFilesAsync(documentCriteria, types);
            await ProcessDocumentsTaskAsync(prog, token, types, documentCriteria);
            await SaveMatchedFilesAsync(token);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                System.Windows.MessageBox.Show("Completed!");
            });
        }

        public async Task ProcessDocumentsTaskAsync(IProgress<TaskStatusHelper> prog, CancellationToken token, List<DocumentTypes> types, List<DocumentCriteria> documentCriteria)
        {
            await Task.Run(async () =>
            {
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
                var criteriaDirectoryInfo = new DirectoryInfo(Common.CriteriaStorage);
                var criteriaFiles = criteriaDirectoryInfo.GetFiles();
                CreateCriteriaArrays(criteriaFiles);
                var tempDirectoryInfo = new DirectoryInfo(Common.TempStorage);
                var files = tempDirectoryInfo.GetFiles();
                var fileCount = Convert.ToDouble(files.Count());
                var currentFile = 0.0;
                Parallel.ForEach(files, (file) =>
                {
                    var criteriaMatches = types.Select(o => new CriteriaMatchModel { DocumentType = o }).ToList();
                    using (var observedImage = CvInvoke.Imread(file.FullName))
                    {
                        Parallel.ForEach(_criteriaImages, (criteriaImage) =>
                        {
                            var criteriaFile = criteriaImage.Info;
                            var criteriaFileSplit = criteriaFile.Name.Split('-');
                            var type = types.First(c => c.DocumentType == criteriaFileSplit[0]);
                            var score = Classify(criteriaImage.Image, observedImage);
                            var critName = criteriaFileSplit[1].Substring(0, criteriaFileSplit[1].Length - 4);
                            var crit = documentCriteria.First(c => c.DocumentTypeId == type.Id && c.CriteriaName == critName);
                            var existingModel = criteriaMatches.First(c => c.DocumentType == type);
                            existingModel.Score += score;
                            existingModel.PdfFile = file.FullName;
                            ScoreLog = $"File Name: {file.FullName}\nDocument Type: {existingModel.DocumentType.DocumentType}\nCriteria: {crit.CriteriaName}\nScore: {score}\n--------------------{ScoreLog}";
                        });
                    }
                    if (token.IsCancellationRequested)
                        return;
                    var matchedCriteria = criteriaMatches.First(c => c.Score == criteriaMatches.Max(p => p.Score));
                    Console.WriteLine($@"Total Matches: {matchedCriteria.Score}");
                    if (matchedCriteria.Score >= matchedCriteria.DocumentType.AverageScore)
                    {
                        matchedCriteria.MatchedFileInfo = file;
                        _matchedFiles.Add(matchedCriteria);
                        //var matchedFile = PdfFiles.First(c => c.Contains(file.Name.Substring(0, file.Name.Length - 4)));
                        //var matchedFileExtension = matchedFile.Substring(matchedFile.Length - 3);
                        //if (matchedFileExtension.Equals("pdf", StringComparison.CurrentCultureIgnoreCase))
                        //{
                        //    ExtractPageFromPdf(file, matchedCriteria.DocumentType.DocumentType, NamingModels);
                        //}
                        //else
                        //{
                        //    CreatePdfFromImage(file, matchedCriteria.DocumentType.DocumentType, NamingModels);
                        //}
                    }
                    currentFile++;
                    var progress = (currentFile / fileCount) * 100;
                    prog.Report(new TaskStatusHelper
                    {
                        ProgressText = file.Name,
                        ProgressPercentage = progress
                    });
                });
                //foreach (var file in files)
                //{
                //    var criteriaMatches = types.Select(o => new CriteriaMatchModel { DocumentType = o }).ToList();
                //    using (var observedImage = CvInvoke.Imread(file.FullName))
                //    {
                //        Parallel.ForEach(_criteriaImages, (criteriaImage) =>
                //        {
                //            var criteriaFile = criteriaImage.Info;
                //            var criteriaFileSplit = criteriaFile.Name.Split('-');
                //            var type = types.First(c => c.DocumentType == criteriaFileSplit[0]);
                //            var score = Classify(criteriaImage.Image, observedImage);
                //            var critName = criteriaFileSplit[1].Substring(0, criteriaFileSplit[1].Length - 4);
                //            var crit = documentCriteria.First(c => c.DocumentTypeId == type.Id && c.CriteriaName == critName);
                //            var existingModel = criteriaMatches.First(c => c.DocumentType == type);
                //            existingModel.Score += score;
                //            existingModel.PdfFile = file.FullName;
                //            ScoreLog = $"File Name: {file.FullName}\nDocument Type: {existingModel.DocumentType.DocumentType}\nCriteria: {crit.CriteriaName}\nScore: {score}\n--------------------{ScoreLog}";
                //        });
                //    }
                //    if (token.IsCancellationRequested)
                //        return;
                //    var matchedCriteria = criteriaMatches.First(c => c.Score == criteriaMatches.Max(p => p.Score));
                //    Console.WriteLine($@"Total Matches: {matchedCriteria.Score}");
                //    if (matchedCriteria.Score >= matchedCriteria.DocumentType.AverageScore)
                //    {
                //        var theFileName = PdfFiles.First(c => c.Contains(file.Name.Substring(0, file.Name.Length - 4)));
                //        var theExt = theFileName.Substring(theFileName.Length - 3);
                //        if (theExt.Equals("pdf", StringComparison.CurrentCultureIgnoreCase))
                //        {
                //            ExtractPageFromPdf(file, matchedCriteria.DocumentType.DocumentType, NamingModels);
                //        }
                //        else
                //        {
                //            CreatePdfFromImage(file, matchedCriteria.DocumentType.DocumentType, NamingModels);
                //        }
                //    }
                //    currentFile++;
                //    var progress = (currentFile / fileCount) * 100;
                //    prog.Report(new TaskStatusHelper
                //    {
                //        ProgressText = file.Name,
                //        ProgressPercentage = progress
                //    });
                //}
            });
        }

        public long Classify(Mat modelImage, Mat observedImage)
        {
            var score = 0L;
            score = Classifier.ProcessImage(modelImage, observedImage, UniquenessThreshold, KNearest, SelectedDetectionMethod);
            if (!ViewResults) return score;
            var result = Classifier.ProcessImageAndShowResult(modelImage, observedImage, UniquenessThreshold, KNearest, SelectedDetectionMethod);
            ImageViewer.Show(result);
            return score;
        }

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

        public Task SaveMatchedFilesAsync(CancellationToken token)
        {
            return Task.Run(() =>
            {
                foreach (var item in _matchedFiles)
                {
                    if (token.IsCancellationRequested)
                        return;
                    var matchedFile = PdfFiles.First(c => c.Contains(file.Name.Substring(0, file.Name.Length - 4)));
                    var matchedFileExtension = matchedFile.Substring(matchedFile.Length - 3);
                    if (matchedFileExtension.Equals("pdf", StringComparison.CurrentCultureIgnoreCase))
                    {
                        ExtractPageFromPdf(item.MatchedFileInfo, item.DocumentType.DocumentType, NamingModels);
                    }
                    else
                    {
                        CreatePdfFromImage(item.MatchedFileInfo, item.DocumentType.DocumentType, NamingModels);
                    }
                }
            });
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

        public void CreatePdfFromImage(FileInfo file, string type, List<FileNamingModel> models = null)
        {
            var fileName = file.Name.Substring(0, file.Name.Length - 4);
            if (models != null)
            {
                var fileNameSplit = fileName.Split('.');
                var serialNumber = fileNameSplit[0];
                var model = models.FirstOrDefault(c => c.Serial == serialNumber);
                if (model != null)
                {
                    fileName = model.Tag;
                }
            }
            var origPdf = PdfImages.First(c => c.Key == file.FullName);
            var resultPath = Path.Combine(Common.ResultsStorage, type);
            using (var pdf = new PdfDocument())
            {
                var section = pdf.Sections.Add();
                var image = new PdfBitmap(origPdf.Key);
                var frameCount = image.FrameCount;
                for (var i = 0; i < frameCount; i++)
                {
                    var page = section.Pages.Add();
                    section.PageSettings.Margins.All = 0;
                    var graphics = page.Graphics;
                    image.ActiveFrame = i;
                    graphics.DrawImage(image, 0, 0, page.GetClientSize().Width, page.GetClientSize().Height);
                }
                var savePath = Path.Combine(resultPath, $"{fileName}.pdf");
                if (File.Exists(savePath))
                {
                    savePath = Path.Combine(resultPath, $"{fileName}-1.pdf");
                }
                pdf.Save(savePath);
                pdf.Close(true);
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

        public void GotoResultsFolder()
        {
            System.Diagnostics.Process.Start(Common.ResultsStorage);
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

        public string ProgressText
        {
            get => _progressText;
            set => Set(ref _progressText, value);
        }
        private string _progressText;

        public double ProgressPercentage
        {
            get => _progressPercentage;
            set => Set(ref _progressPercentage, value);
        }
        private double _progressPercentage;

        public ObservableCollection<DocumentSelectionModel> DocumentSelectionList
        {
            get => _documentSelectionList;
            set => Set(ref _documentSelectionList, value);
        }
        private ObservableCollection<DocumentSelectionModel> _documentSelectionList;

        public string ScoreLog
        {
            get => _scoreLog;
            set => Set(ref _scoreLog, value);
        }
        private string _scoreLog;

        public int SelectedDetectionMethod
        {
            get => _selectedDetectionMethod;
            set => Set(ref _selectedDetectionMethod, value);
        }
        private int _selectedDetectionMethod;

        public CancellationTokenSource CancelTokenSource
        {
            get => _cancelTokenSource;
            set => Set(ref _cancelTokenSource, value);
        }
        private CancellationTokenSource _cancelTokenSource;

        private List<CriteriaImageModel> _criteriaImages;

        public bool CancelEnabled
        {
            get => _cancelEnabled;
            set => Set(ref _cancelEnabled, value);
        }
        private bool _cancelEnabled;

        private List<CriteriaMatchModel> _matchedFiles;
        #endregion
    }

    public struct ImageScores
    {
        public string Path { get; set; }
        public long Score { get; set; }
        public string PdfFile { get; set; }
        public string CriteriaFile { get; set; }
    }

    //public async Task ProcessDocumentsTaskAsync(IProgress<TaskStatusHelper> prog, CancellationToken token)
    //{
    //    await Task.Run(async () =>
    //    {
    //        var types = new List<DocumentTypes>();
    //        List<DocumentCriteria> documentCriteria = null;
    //        using (var context = new DataContext())
    //        {
    //            var dTypes = context.DocumentTypes.ToList();
    //            foreach(var o in dTypes)
    //            {
    //                if (DocumentSelectionList.First(c => c.DocumentTypeId == o.Id).Selected)
    //                    types.Add(o);
    //            }
    //            documentCriteria = context.DocumentCriteria.ToList();
    //        }
    //        if (!string.IsNullOrWhiteSpace(NamingSpreadsheetPath))
    //        {
    //            var spreadsheetDataTable = await Common.GetSpreadsheetDataTableAsync(NamingSpreadsheetPath, "Reference");
    //            if (spreadsheetDataTable != null)
    //            {
    //                NamingModels = new List<FileNamingModel>();
    //                foreach (DataRow dr in spreadsheetDataTable.Rows)
    //                {
    //                    var serial = string.Empty;
    //                    var tag = string.Empty;
    //                    if (!string.IsNullOrWhiteSpace(dr["Serial"].ToString()))
    //                        serial = dr["Serial"].ToString();
    //                    if (!string.IsNullOrWhiteSpace(dr["Tag"].ToString()))
    //                        tag = dr["Tag"].ToString();
    //                    NamingModels.Add(new FileNamingModel { Serial = serial, Tag = tag });
    //                }
    //            }
    //        }
    //        await ConvertPdfsToImagesAsync();
    //        await Common.CreateCriteriaFilesAsync(documentCriteria, types);
    //        var criteriaDirectoryInfo = new DirectoryInfo(Common.CriteriaStorage);
    //        var criteriaFiles = criteriaDirectoryInfo.GetFiles();
    //        var tempDirectoryInfo = new DirectoryInfo(Common.TempStorage);
    //        var files = tempDirectoryInfo.GetFiles();
    //        var fileCount = Convert.ToDouble(files.Count());
    //        var currentFile = 0.0;
    //        foreach (var file in files)
    //        {
    //            var criteriaMatches = types.Select(o => new CriteriaMatchModel { DocumentType = o }).ToList();
    //            foreach (var criteriaFile in criteriaFiles)
    //            {
    //                var criteriaFileSplit = criteriaFile.Name.Split('-');
    //                var type = types.First(c => c.DocumentType == criteriaFileSplit[0]);
    //                var score = Classify(criteriaFile.FullName, file.FullName);
    //                var critName = criteriaFileSplit[1].Substring(0, criteriaFileSplit[1].Length - 4);
    //                var crit = documentCriteria.First(c => c.DocumentTypeId == type.Id && c.CriteriaName == critName);
    //                var existingModel = criteriaMatches.First(c => c.DocumentType == type);
    //                existingModel.Score += score;
    //                existingModel.PdfFile = file.FullName;
    //                ScoreLog = $"File Name: {file.FullName}\nDocument Type: {existingModel.DocumentType.DocumentType}\nCriteria: {crit.CriteriaName}\nScore: {score}\n--------------------{ScoreLog}";
    //                if (token.IsCancellationRequested)
    //                    return;
    //            }
    //            var matchedCriteria = criteriaMatches.First(c => c.Score == criteriaMatches.Max(p => p.Score));
    //            Console.WriteLine($@"Total Matches: {matchedCriteria.Score}");
    //            if (matchedCriteria.Score >= matchedCriteria.DocumentType.AverageScore)
    //            {
    //                ExtractPageFromPdf(file, matchedCriteria.DocumentType.DocumentType, NamingModels);
    //            }
    //            currentFile++;
    //            var progress = (currentFile / fileCount) * 100;
    //            prog.Report(new TaskStatusHelper
    //            {
    //                ProgressText = file.Name,
    //                ProgressPercentage = progress
    //            });
    //        }
    //    });
    //}


    //public long Classify(string criteriaPath, string obsPath)
    //{
    //    var score = 0L;
    //    using (var modelImage = CvInvoke.Imread(criteriaPath, ImreadModes.Grayscale))
    //    {
    //        using (var observedImage = CvInvoke.Imread(obsPath))
    //        {
    //            score = Classifier.ProcessImage(modelImage, observedImage, UniquenessThreshold, KNearest, SelectedDetectionMethod);
    //            if (!ViewResults) return score;
    //            var result = Classifier.ProcessImageAndShowResult(modelImage, observedImage, UniquenessThreshold, KNearest, SelectedDetectionMethod);
    //            ImageViewer.Show(result);
    //        }
    //    }
    //    return score;
    //}
}
