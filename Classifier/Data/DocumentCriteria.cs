using System;
using System.Linq;

namespace Classifier.Data
{
    public class DocumentCriteria
    {
        public Guid Id { get; set; }
        public string CriteriaName { get; set; }
        public string CriteriaBytes { get; set; }
        public int PositionX { get; set; }
        public int PositionY { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int BaseWidth { get; set; }
        public int BaseHeight { get; set; }
        public int MatchThreshold { get; set; }
        public virtual Guid DocumentTypeId { get; set; }
        public virtual DocumentTypes DocumentType { get; set; }
    }
}
