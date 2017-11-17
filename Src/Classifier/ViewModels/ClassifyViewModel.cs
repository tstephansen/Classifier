using Classifier.Core;
using Classifier.Data;
using Emgu.CV;
using LandmarkDevs.Core.Infrastructure;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Classifier.Models;
using System.Data;
using System.Threading;
using System.Windows.Data;

namespace Classifier.ViewModels
{
    public class ClassifyViewModel : Observable
    {
        public ClassifyViewModel()
        {
            PdfFiles = new ObservableCollection<string>();
            PdfFiles.CollectionChanged += PdfFiles_CollectionChanged;
            PdfImages = new Dictionary<string, string>();
            BrowseForFilesCommand = new RelayCommand(BrowseForFiles);
            RemoveFileCommand = new RelayCommand(RemoveSelectedFile);
            ClassifyCommand = new RelayCommand(async () => await ProcessDocumentsAsync());
            BrowseForSpreadsheetCommand = new RelayCommand(BrowseForSpreadsheet);
            ResultsFolderCommand = new RelayCommand(GotoResultsFolder);
            ReloadDocumentTypesCommand = new RelayCommand(LoadDocumentTypes);
            CancelClassifyCommand = new RelayCommand(CancelClassify);
            ConfirmDialogCommand = new RelayCommand(CloseDialog);
            RemoveResultsCommand = new RelayCommand(RemoveResults);
            DocumentSelectionList = new ObservableCollection<DocumentSelectionModel>();
            UniquenessThreshold = 0.60;
            KNearest = 2;
            SelectedDetectionMethod = 0;
            LoadDocumentTypes();
        }

        #region Commands
        public IRelayCommand ClassifyCommand { get; }
        public IRelayCommand BrowseForFilesCommand { get; }
        public IRelayCommand RemoveFileCommand { get; }
        public IRelayCommand BrowseForSpreadsheetCommand { get; }
        public IRelayCommand ResultsFolderCommand { get; }
        public IRelayCommand ReloadDocumentTypesCommand { get; }
        public IRelayCommand CancelClassifyCommand { get; }
        public IRelayCommand ConfirmDialogCommand { get; }
        public IRelayCommand RemoveResultsCommand { get; }
        #endregion

        #region Methods
        #region Misc Methods
        public void CloseDialog()
        {
            DialogVisible = false;
        }

        public void LoadDocumentTypes()
        {
            var models = Common.LoadDocumentSelectionModels();
            DocumentSelectionList = new ObservableCollection<DocumentSelectionModel>(models);
            SelectionViewSource = new CollectionView(DocumentSelectionList);
        }

        public static void GotoResultsFolder() => System.Diagnostics.Process.Start(Common.ResultsStorage);

        public static void RemoveResults()
        {
            var dirInfo = new DirectoryInfo(Common.ResultsStorage);
            var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
            foreach(var file in files)
            {
                File.Delete(file.FullName);
            }
        }

        private void PdfFiles_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            ClassifyEnabled = PdfFiles.Count > 0;
        }
        #endregion

        #region File Methods
        public void BrowseForFiles()
        {
            PdfFilesList = new List<PdfFileModel>();
            SelectedFile = null;
            var filesDialog = Common.BrowseForFiles(true, "PDF (*.pdf)|*.pdf|Excel (*.xlsx)|*.xlsx|PNG (*.png)|*.png");
            if (filesDialog.ShowDialog() != true)
                return;
            foreach (var o in filesDialog.FileNames)
            {
                PdfFiles.Add(o);
                var info = new FileInfo(o);
                PdfFilesList.Add(new PdfFileModel
                {
                    Name = info.Name,
                    Path = info.FullName,
                    Matches = 0
                });
            }
            FilesViewSource = new CollectionView(PdfFilesList);
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
            var pdf = PdfFiles.First(c => c == SelectedFile.Path);
            PdfFiles.Remove(pdf);
            PdfFilesList.Remove(SelectedFile);
            SelectedFile = null;
        }
        #endregion

        #region Classification Methods
        public void CancelClassify()
        {
            CancelTokenSource.Cancel();
            CancelEnabled = false;
            ClassifyEnabled = true;
        }

        public async Task ProcessDocumentsAsync()
        {
            CancelEnabled = true;
            ClassifyEnabled = false;
            var tempDirectory = new DirectoryInfo(Common.TempStorage);
            var tempFiles = tempDirectory.GetFiles();
            foreach(var file in tempFiles)
            {
                File.Delete(file.FullName);
            }
            CancelTokenSource = new CancellationTokenSource();
            var token = CancelTokenSource.Token;
            var prog = new Progress<TaskProgress>();
            prog.ProgressChanged += (sender, exportProgress) =>
            {
                ProgressPercentage = Math.Round(exportProgress.ProgressPercentage, 2);
                ProgressText = exportProgress.ProgressText;
                ProgressText2 = exportProgress.ProgressText2;
            };
            ProgressText = "Creating PDF Files.";
            var eta = new EtaCalculator(1, 30);
            var pdfImages = await Common.ConvertPdfsToImagesAsync(PdfFiles, prog, eta);
            var pngImages = await Common.CopyImagesToTempFolderAsync(PdfFiles);
            PdfImages = new Dictionary<string, string>(pdfImages);
            foreach(var png in pngImages)
            {
                PdfImages.Add(png.Key, png.Value);
            }
            var types = new List<DocumentTypes>();
            List<DocumentCriteria> documentCriteria = null;
            using (var context = new ClassifierContext())
            {
                var dTypes = context.DocumentTypes.ToList();
                foreach (var o in dTypes)
                {
                    if (DocumentSelectionList.First(c => c.DocumentTypeId == o.Id).Selected)
                        types.Add(o);
                }
                documentCriteria = context.DocumentCriteria.ToList();
            }
            ProgressText = "Finding Matches";
            ProgressPercentage = 0.0;
            await Common.CreateCriteriaFilesAsync(documentCriteria, types);
            var criteriaAndNaming = Common.SetNamingAndCriteria(NamingSpreadsheetPath);
            NamingModels = new List<FileNamingModel>(criteriaAndNaming.Item1);
            _criteriaImages = new List<CriteriaImageModel>(criteriaAndNaming.Item2);
            var files = tempDirectory.GetFiles();
            eta = new EtaCalculator(3, 30);
            await ProcessSelectedDocumentsAsync(prog, token, types, eta, files.ToList());
            DialogTitle = "Complete";
            DialogText = "The documents you selected have been classified.";
            DialogVisible = true;
        }

        public Task ProcessSelectedDocumentsAsync(IProgress<TaskProgress> prog, CancellationToken token, List<DocumentTypes> types, EtaCalculator pc, List<FileInfo> files)
        {
            var currentFile = 0.0;
            var fileCount = Convert.ToDouble(files.Count);
            return Task.Run(() =>
            {
                foreach (var file in files)
                {
                    var criteriaMatches = types.Select(o => new CriteriaMatchModel { DocumentType = o }).ToList();
                    using (var observedImage = CvInvoke.Imread(file.FullName))
                    {
                        Parallel.ForEach(_criteriaImages, (criteriaImage) =>
                        {
                            var criteriaFile = criteriaImage.Info;
                            var criteriaFileSplit = criteriaFile.Name.Split('-');
                            var type = types.First(c => c.DocumentType == criteriaFileSplit[0]);
                            var score = Classify(criteriaImage, observedImage);
                            var existingModel = criteriaMatches.First(c => c.DocumentType == type);
                            existingModel.Score += score;
                            existingModel.PdfFile = file.FullName;
                        });
                    }
                    if (token.IsCancellationRequested)
                        return;
                    var matchedCriteria = criteriaMatches.First(c => c.Score == criteriaMatches.Max(p => p.Score));
                    Console.WriteLine($"Score: {matchedCriteria.Score}");
                    if (matchedCriteria.Score >= matchedCriteria.DocumentType.AverageScore)
                    {
                        DocumentSelectionList.First(c => c.DocumentType == matchedCriteria.DocumentType.DocumentType).Matches += 1;
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SelectionViewSource.Refresh();
                        });
                        var matchedFileName = file.Name.Substring(0, file.Name.Length - 4).Split('.')[0];
                        var matchedFile = PdfFiles.First(c => c.Contains(matchedFileName));
                        var matchedFileExtension = matchedFile.Substring(matchedFile.Length - 3);
                        if (matchedFileExtension.Equals("pdf", StringComparison.CurrentCultureIgnoreCase))
                        {
                            ExtractPageFromPdf(file, matchedCriteria.DocumentType.DocumentType, NamingModels);
                        }
                        else
                        {
                            CreatePdfFromImage(file, matchedCriteria.DocumentType.DocumentType, NamingModels);
                        }
                    }
                    currentFile++;
                    var rawProgress = (currentFile / fileCount);
                    var progress = rawProgress * 100;
                    var progressFloat = (float)rawProgress;
                    pc.Update(progressFloat);
                    if (pc.ETAIsAvailable)
                    {
                        var timeRemaining = pc.ETR.ToString(@"dd\.hh\:mm\:ss");
                        prog.Report(new TaskProgress
                        {
                            ProgressText = file.Name,
                            ProgressPercentage = progress,
                            ProgressText2 = timeRemaining
                        });
                    }
                    else
                    {
                        prog.Report(new TaskProgress
                        {
                            ProgressText = file.Name,
                            ProgressPercentage = progress,
                            ProgressText2 = "Calculating..."
                        });
                    }
                }
            });
        }

        public long Classify(CriteriaImageModel model, Mat observedImage)
        {
            var score = 0L;
            score = DocumentClassification.Classify(model.ModelDescriptors, observedImage, UniquenessThreshold, KNearest, SelectedDetectionMethod);
            return score;
        }
        #endregion

        #region Post Classification
        public void ExtractPageFromPdf(FileInfo file, string type, List<FileNamingModel> models = null)
        {
            var fileName = file.Name.Substring(0, file.Name.Length - 4);
            if (models != null)
            {
                var fileNameSplit = fileName.Split('.');
                var serialNumber = fileNameSplit[0];
                var model = models.FirstOrDefault(c => c.Serial == serialNumber);
                if (model != null)
                {
                    fileName = AppendSerialToFile ? $"{model.Tag} - {serialNumber}" : model.Tag;
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
                var startIndex = page - 1;
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
        #endregion
        #endregion

        #region Fields
        public List<FileNamingModel> NamingModels { get; set; }
        public bool ClassifyEnabled
        {
            get => _classifyEnabled;
            set => Set(ref _classifyEnabled, value);
        }
        private bool _classifyEnabled;

        public Dictionary<string, string> PdfImages { get; set; }

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
                ClassifyEnabled = value.Count > 0;
                Set(ref _pdfFiles, value);
            }
        }
        private ObservableCollection<string> _pdfFiles;

        public PdfFileModel SelectedFile
        {
            get => _selectedFile;
            set => Set(ref _selectedFile, value);
        }
        private PdfFileModel _selectedFile;

        public string NamingSpreadsheetPath
        {
            get => _namingSpreadsheetPath;
            set
            {
                AppendSerialToFileEnabled = !string.IsNullOrWhiteSpace(value);
                Set(ref _namingSpreadsheetPath, value);
            }
        }
        private string _namingSpreadsheetPath;

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

        public string ProgressText2
        {
            get => _progressText2;
            set => Set(ref _progressText2, value);
        }
        private string _progressText2;

        public bool DialogVisible
        {
            get => _dialogVisible;
            set => Set(ref _dialogVisible, value);
        }
        private bool _dialogVisible;

        public string DialogTitle
        {
            get => _dialogTitle;
            set => Set(ref _dialogTitle, value);
        }
        private string _dialogTitle;

        public string DialogText
        {
            get => _dialogText;
            set => Set(ref _dialogText, value);
        }
        private string _dialogText;

        public CollectionView SelectionViewSource
        {
            get => _selectionViewSource;
            set => Set(ref _selectionViewSource, value);
        }
        private CollectionView _selectionViewSource;

        public CollectionView FilesViewSource
        {
            get => _filesViewSource;
            set => Set(ref _filesViewSource, value);
        }
        private CollectionView _filesViewSource;

        public List<PdfFileModel> PdfFilesList
        {
            get => _pdfFilesList;
            set => Set(ref _pdfFilesList, value);
        }
        private List<PdfFileModel> _pdfFilesList;

        public bool AppendSerialToFile
        {
            get => _appendSerialToFile;
            set => Set(ref _appendSerialToFile, value);
        }
        private bool _appendSerialToFile;

        public bool AppendSerialToFileEnabled
        {
            get => _appendSerialToFileEnabled;
            set => Set(ref _appendSerialToFileEnabled, value);
        }
        private bool _appendSerialToFileEnabled;
        #endregion
    }
}
