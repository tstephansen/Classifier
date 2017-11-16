using System;

namespace Classifier.Models
{
    public class DocumentSelectionModel
    {
        public Guid DocumentTypeId { get; set; }
        public string DocumentType { get; set; }
        public bool Selected { get; set; }
        public long Matches { get; set; }
        public string Display => $"{DocumentType} - Matches: {Matches}";
    }
}
