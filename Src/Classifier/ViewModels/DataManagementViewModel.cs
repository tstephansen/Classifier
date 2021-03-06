﻿using Classifier.Core;
using Classifier.Data;
using LandmarkDevs.Core.Infrastructure;
using LandmarkDevs.Core.Shared;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using System.Data;
using System.Data.SqlClient;

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
            ImportCriteriaCommand = new RelayCommand(async () => await ImportCriteriaAsync());
            LoadDocumentTypes();
        }

        #region Commands
        public IRelayCommand AddDocumentTypeCommand { get; }
        public IRelayCommand RemoveCriteriaCommand { get; }
        public IRelayCommand RemoveDocumentTypeCommand { get; }
        public IRelayCommand DocumentTypeChangedCommand { get; }
        public IRelayCommand SetRequiredScoreCommand { get; }
        public IRelayCommand ImportCriteriaCommand { get; }
        #endregion

        #region Methods
        public Task ImportCriteriaAsync()
        {
            return Task.Run(() =>
            {
                var files = Common.BrowseForFiles(false, "SQL Script (*.sql)|*.sql");
                if (files.ShowDialog() != true)
                    return;
                try
                {
                    var builder = new SqlConnectionStringBuilder(@"Server=(localdb)\v11.0;Integrated Security=true;AttachDbFileName=|DataDirectory|ClassifierDb.mdf;")
                    {
                        AttachDBFilename = Path.Combine(Common.AppStorage, "ClassifierDb.mdf")
                    };
                    var connString = builder.ConnectionString;
                    using (var conn = new SqlConnection(connString))
                    {
                        try
                        {
                            if (conn.State == ConnectionState.Closed) conn.Open();
                            var commands = File.ReadLines(files.FileName);
                            foreach(var line in commands)
                            {
                                using (var cmd = new SqlCommand(line, conn))
                                {
                                    cmd.ExecuteNonQuery();
                                }
                            }
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                LoadDocumentTypes();
                                System.Windows.MessageBox.Show("Import has finished.", "Complete");
                            });
                        }
                        catch(Exception ex)
                        {
                            Common.Logger.Log(LogLevel.Error, ex);
                        }
                        finally
                        {
                            if (conn.State == ConnectionState.Open) conn.Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.Logger.Log(LogLevel.Error, ex);
                }
            });
        }

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
                        DocumentType = DocumentTypeText,
                        AverageScore = RequiredScore
                    });
                    context.SaveChanges();
                }
                var newTypePath = Path.Combine(Common.ResultsStorage, DocumentTypeText);
                if (!Directory.Exists(newTypePath)) Directory.CreateDirectory(newTypePath);
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
                    var type = context.DocumentTypes.First(c => c.Id == SelectedDocumentType.Id);
                    context.DocumentTypes.Remove(type);
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
                    var item = context.DocumentCriteria.First(c => c.Id == SelectedCriteria.Id);
                    context.DocumentCriteria.Remove(item);
                    context.SaveChanges();
                }
                LoadCriterion();
            }
        }

        public void SetRequiredScore()
        {
            using(var context = new ClassifierContext())
            {
                var item = context.DocumentTypes.First(c => c.Id == SelectedDocumentType.Id);
                item.AverageScore = RequiredScore;
                context.SaveChanges();
            }
            LoadDocumentTypes();
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
