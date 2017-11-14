using Classifier.Data;
using LandmarkDevs.Core.Infrastructure;
using LandmarkDevs.Core.Shared;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.ViewModels
{
    public class DataManagementViewModel : Observable
    {
        public DataManagementViewModel()
        {
            AddDocumentTypeCommand = new RelayCommand(AddDocumentType);
            RemoveDocumentTypeCommand = new RelayCommand(RemoveDocumentType);
            RemoveCriteriaCommand = new RelayCommand(RemoveCriteria);
            DocumentTypeChangedCommand = new RelayCommand(LoadCriterion);
            SetRequiredScoreCommand = new RelayCommand(SetRequiredScore);
            LoadDocumentTypes();
        }

        #region Commands
        public IRelayCommand AddDocumentTypeCommand { get; }
        public IRelayCommand RemoveCriteriaCommand { get; }
        public IRelayCommand RemoveDocumentTypeCommand { get; }
        public IRelayCommand DocumentTypeChangedCommand { get; }
        public IRelayCommand SetRequiredScoreCommand { get; }
        #endregion

        #region Methods
        public void LoadDocumentTypes()
        {
            using(var context = new ClassifierContext())
            {
                var docTypes = context.DocumentTypes.ToList();
                DocumentTypeList = new ObservableCollection<DocumentTypes>(docTypes);
            }
        }

        public void LoadCriterion()
        {
            if (SelectedDocumentType == null) return;
            RequiredScore = SelectedDocumentType.AverageScore;
            using(var context = new ClassifierContext())
            {
                var criterion = context.DocumentCriteria.Where(c=>c.DocumentTypeId == SelectedDocumentType.Id).ToList();
                Criterion = new ObservableCollection<DocumentCriteria>(criterion);
            }
        }

        public void AddDocumentType()
        {
            if (!string.IsNullOrWhiteSpace(DocumentTypeText))
            {
                using(var context = new ClassifierContext())
                {
                    if (context.DocumentTypes.Any(c => c.DocumentType.Equals(DocumentTypeText, StringComparison.CurrentCultureIgnoreCase))) return;
                    context.DocumentTypes.Add(new DocumentTypes
                    {
                        Id = GuidGenerator.GenerateTimeBasedGuid(),
                        DocumentType = DocumentTypeText
                    });
                    context.SaveChanges();
                }
                LoadDocumentTypes();
            }
        }

        public void RemoveDocumentType()
        {
            if (SelectedDocumentType != null)
            {
                using (var context = new ClassifierContext())
                {
                    var criteria = context.DocumentCriteria.Where(c => c.DocumentTypeId == SelectedDocumentType.Id).ToList();
                    foreach (var o in criteria)
                    {
                        context.DocumentCriteria.Remove(o);
                    }
                    context.DocumentTypes.Remove(SelectedDocumentType);
                    context.SaveChanges();
                }
                LoadDocumentTypes();
            }
        }

        public void RemoveCriteria()
        {
            if(SelectedCriteria != null)
            {
                using(var context = new ClassifierContext())
                {
                    context.DocumentCriteria.Remove(SelectedCriteria);
                }
                LoadCriterion();
            }
        }

        public void SetRequiredScore()
        {
            using(var context = new ClassifierContext())
            {
                SelectedDocumentType.AverageScore = RequiredScore;
                context.SaveChanges();
            }
        }
        #endregion

        #region Fields
        public string DocumentTypeText
        {
            get => _documentTypeText;
            set => Set(ref _documentTypeText, value);
        }
        private string _documentTypeText;

        public DocumentCriteria SelectedCriteria
        {
            get => _selectedCriteria;
            set => Set(ref _selectedCriteria, value);
        }
        private DocumentCriteria _selectedCriteria;

        public DocumentTypes SelectedDocumentType
        {
            get => _selectedDocumentType;
            set => Set(ref _selectedDocumentType, value);
        }
        private DocumentTypes _selectedDocumentType;

        public ObservableCollection<DocumentTypes> DocumentTypeList
        {
            get => _documentTypeList;
            set => Set(ref _documentTypeList, value);
        }
        private ObservableCollection<DocumentTypes> _documentTypeList;

        public ObservableCollection<DocumentCriteria> Criterion
        {
            get => _criterion;
            set => Set(ref _criterion, value);
        }
        private ObservableCollection<DocumentCriteria> _criterion;

        public long RequiredScore
        {
            get => _requiredScore;
            set => Set(ref _requiredScore, value);
        }
        private long _requiredScore;
        #endregion
    }
}
