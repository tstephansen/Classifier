using Classifier.Core;
using LandmarkDevs.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAPICodePack.Dialogs;
using Microsoft.WindowsAPICodePack.Shell;
using System.Collections.ObjectModel;
using Classifier.Data;
using System.IO;
using Classifier.Models;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.UI;

namespace Classifier.ViewModels
{
    public class TrainingViewModel : Observable
    {
        public TrainingViewModel()
        {
            BrowseCommand = new RelayCommand(Browse);
            StartTrainingCommand = new RelayCommand(async () => await StartTrainingAsync());
            RefreshDocTypes();
            UniquenessThreshold = 0.60;
            KNearest = 2;
            Classifier = new DocClassifier();
        }

        #region Commands
        public IRelayCommand BrowseCommand { get; }
        public IRelayCommand StartTrainingCommand { get; }
        public IRelayCommand ShowMatchesCommand { get; }
        public IRelayCommand BrowseForTestCommand { get; }
        #endregion

        #region Methods
        public void Browse()
        {
            using (CommonOpenFileDialog dialog = new CommonOpenFileDialog())
            {
                dialog.IsFolderPicker = true;
                if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    TrainingFolderPath = dialog.FileName;
                }
            }
        }

        public async Task StartTrainingAsync()
        {
            List<DocumentCriteria> documentCriteria = null;
            using (var context = new DataContext())
            {
                documentCriteria = context.DocumentCriteria.ToList();
            }
            await CreateCriteriaFilesAsync(documentCriteria, SelectedDocumentType);
            var criteriaDirectoryInfo = new DirectoryInfo(Common.CriteriaStorage);
            var criteriaFiles = criteriaDirectoryInfo.GetFiles();
            var tempDirectoryInfo = new DirectoryInfo(TrainingFolderPath);
            var files = tempDirectoryInfo.GetFiles();
            var scores = new List<long>();
            foreach (var file in files)
            {
                var addedScore = 0L;
                var resizedFile = Path.Combine(@"C:\Users\30016976\Desktop\Classifier Stuff\Results\Working", file.Name);
                Common.Resize(file.FullName, resizedFile, 0.4);
                var criteriaMatch = new CriteriaMatchModel { DocumentType = SelectedDocumentType };
                foreach (var criteriaFile in criteriaFiles)
                {
                    var cropPath = resizedFile;
                    var score = Classify(criteriaFile.FullName, cropPath);
                    addedScore += score;
                    criteriaMatch.Score += score;
                    criteriaMatch.PdfFile = file.FullName;
                }
                scores.Add(addedScore);
            }
            var avg = scores.Average();
            var min = scores.Min();
            var max = scores.Max();
            AverageScore = Convert.ToInt64(avg);
            MinScore = Convert.ToInt64(min);
            MaxScore = Convert.ToInt64(max);
        }

        public static Task CreateCriteriaFilesAsync(List<DocumentCriteria> documentCriteria, DocumentTypes type)
        {
            return Task.Run(() =>
            {
                var criteriaDirectoryInfo = new DirectoryInfo(Common.CriteriaStorage);
                var files = criteriaDirectoryInfo.GetFiles();
                foreach (var file in files)
                {
                    File.Delete(file.FullName);
                }
                var criterion = documentCriteria.Where(c => c.DocumentTypeId == type.Id).ToList();
                foreach (var criteria in criterion)
                {
                    var image = Common.ConvertStringToImage(criteria.CriteriaBytes);
                    var imagePath = Path.Combine(Common.CriteriaStorage, $"{type.DocumentType}-{criteria.CriteriaName}.png");
                    image.Save(imagePath);
                }
            });
        }

        public void RefreshDocTypes()
        {
            using (var context = new DataContext())
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

        public long Classify(string criteriaPath, string obsPath)
        {
            var matches = 0;
            var score = 0L;
            using (var modelImage = CvInvoke.Imread(criteriaPath))
            {
                using (var observedImage = CvInvoke.Imread(obsPath))
                {
                    //score = Classifier.ProcessImage(modelImage, observedImage, UniquenessThreshold, KNearest, 0);
                    //if (!ViewResults) return score;
                    //var result = Classifier.ProcessImageAndShowResult(modelImage, observedImage, UniquenessThreshold, KNearest);
                    //ImageViewer.Show(result);
                }
            }
            return score;
        }
        #endregion

        #region Fields
        public string TrainingFolderPath
        {
            get => _trainingFolderPath;
            set => Set(ref _trainingFolderPath, value);
        }
        private string _trainingFolderPath;

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

        public long? AverageScore
        {
            get => _averageScore;
            set => Set(ref _averageScore, value);
        }
        private long? _averageScore;

        public long? MinScore
        {
            get => _minScore;
            set => Set(ref _minScore, value);
        }
        private long? _minScore;

        public long? MaxScore
        {
            get => _maxScore;
            set => Set(ref _maxScore, value);
        }
        private long? _maxScore;

        public string TestPdfPath
        {
            get => _testPdfPath;
            set => Set(ref _testPdfPath, value);
        }
        private string _testPdfPath;
        #endregion
    }
}
