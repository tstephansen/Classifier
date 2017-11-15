using Classifier.Core;
using Classifier.Data;
using Classifier.Models;
using Classifier.Views;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.UI;
using LandmarkDevs.Core.Infrastructure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace Classifier.ViewModels
{
    public class SingleClassifyViewModel : Observable
    {
        public SingleClassifyViewModel()
        {
            KNearest = 2;
            UniquenessThreshold = 0.6;
            DocClass = new DocumentClassification();
            _criteriaImages = new List<CriteriaImageModel>();
            BrowseCommand = new RelayCommand(BrowseForFiles);
            ReloadDocumentTypesCommand = new RelayCommand(LoadDocumentTypes);
            SelectAllCommand = new RelayCommand(SelectAll);
            SelectNoneCommand = new RelayCommand(SelectNone);
            ClassifyCommand = new RelayCommand(async ()=> await ProcessDocumentsAsync());
            LoadDocumentTypes();
        }

        #region Commands
        public IRelayCommand ClassifyCommand { get; }
        public IRelayCommand BrowseCommand { get; }
        public IRelayCommand ReloadDocumentTypesCommand { get; }
        public IRelayCommand SelectAllCommand { get; }
        public IRelayCommand SelectNoneCommand { get; }
        #endregion

        #region Methods
        public void LoadDocumentTypes()
        {
            var models = Common.LoadDocumentSelectionModels();
            DocumentSelectionList = new ObservableCollection<DocumentSelectionModel>(models);
            ViewSource = new CollectionView(DocumentSelectionList);
        }

        public void SelectAll()
        {
            foreach(var o in DocumentSelectionList)
            {
                o.Selected = true;
            }
            ViewSource.Refresh();
        }

        public void SelectNone()
        {
            foreach (var o in DocumentSelectionList)
            {
                o.Selected = false;
            }
            ViewSource.Refresh();
        }

        public void BrowseForFiles()
        {
            var filesDialog = Common.BrowseForFiles(false, "PDF (*.pdf)|*.pdf");
            if (filesDialog.ShowDialog() != true)
                return;
            PdfPath = filesDialog.FileName;
        }

        public Task CreateCriteriaFilesAsync(List<DocumentCriteria> documentCriteria, List<DocumentTypes> types)
        {
            return Task.Run(() =>
            {
                var criteriaDirectoryInfo = new DirectoryInfo(Common.CriteriaStorage);
                var files = criteriaDirectoryInfo.GetFiles();
                foreach (var file in files)
                {
                    File.Delete(file.FullName);
                }
                foreach (var type in types)
                {
                    var criterion = documentCriteria.Where(c => c.DocumentTypeId == type.Id).ToList();
                    foreach (var criteria in criterion)
                    {
                        var image = Common.ConvertStringToImage(criteria.CriteriaBytes);
                        var imagePath = Path.Combine(Common.CriteriaStorage, $"{type.DocumentType}-{criteria.CriteriaName}.png");
                        image.Save(imagePath);
                    }
                }
            });
        }

        public List<CriteriaImageModel> SetNamingAndCriteria()
        {
            var criteriaImages = new List<CriteriaImageModel>();
            var criteriaDirectoryInfo = new DirectoryInfo(Common.CriteriaStorage);
            var criteriaFiles = criteriaDirectoryInfo.GetFiles();
            foreach(var o in criteriaFiles)
            {
                _criteriaFilePaths.Add(o.FullName);
            }
            var images = Common.CreateCriteriaArrays(criteriaFiles);
            criteriaImages.AddRange(images);
            return criteriaImages;
        }

        public async Task ProcessDocumentsAsync()
        {
            _criteriaFilePaths = new List<string>();
            _criteriaImages = new List<CriteriaImageModel>();
            _matchedFiles = new List<CriteriaMatchModel>();
            ClassifyEnabled = false;
            var pdfFiles = new List<string> { PdfPath };
            await Common.ConvertPdfsToImagesAsync(pdfFiles);
            var images = await Common.CopyImagesToTempFolderAsync(pdfFiles);
            PdfImages = new Dictionary<string, string>(images);
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
            await Common.CreateCriteriaFilesAsync(documentCriteria, types);
            var criteriaImages = SetNamingAndCriteria();
            _criteriaImages.AddRange(criteriaImages);
            var tempDirectoryInfo = new DirectoryInfo(Common.TempStorage);
            var files = tempDirectoryInfo.GetFiles();
            await ProcessSelectedDocumentsAsync(files.ToList());
            ClassifyEnabled = true;
        }

        public Task ProcessSelectedDocumentsAsync(List<FileInfo> files)
        {
            return Task.Run(() =>
            {
                foreach(var file in files)
                {
                    foreach(var criteriaFile in _criteriaFilePaths)
                    {
                        var score = 0L;
                        using(var modelImage = CvInvoke.Imread(criteriaFile, Emgu.CV.CvEnum.ImreadModes.Grayscale))
                        {
                            using (var observedImage = CvInvoke.Imread(file.FullName))
                            {
                                score = Classify(modelImage, observedImage);
                                Console.WriteLine($"Score: {score}");
                            }
                        }
                    }
                }
            });
        }

        public long Classify(Mat modelImage, Mat observedImage)
        {
            var score = 0L;
            var result = DocClass.ClassifyAndShowResult(modelImage, observedImage, UniquenessThreshold, KNearest, out score);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var viewer = new ImagePreviewWindow(result, score);
                viewer.ShowDialog();
            });
            return score;
        }
        #endregion

        #region Fields
        private List<CriteriaImageModel> _criteriaImages;
        private List<string> _criteriaFilePaths;
        public string PdfPath
        {
            get => _pdfPath;
            set
            {
                ClassifyEnabled = value != null ? true : false;
                Set(ref _pdfPath, value);
            }
        }
        private string _pdfPath;

        public bool ClassifyEnabled
        {
            get => _classifyEnabled;
            set => Set(ref _classifyEnabled, value);
        }
        private bool _classifyEnabled;

        private List<CriteriaMatchModel> _matchedFiles;

        public ObservableCollection<DocumentSelectionModel> DocumentSelectionList
        {
            get => _documentSelectionList;
            set => Set(ref _documentSelectionList, value);
        }
        private ObservableCollection<DocumentSelectionModel> _documentSelectionList;

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

        public DocumentClassification DocClass { get; set; }

        public Dictionary<string, string> PdfImages { get; set; }

        public CollectionView ViewSource
        {
            get => _viewSource;
            set => Set(ref _viewSource, value);
        }
        private CollectionView _viewSource;
        #endregion
    }
}
