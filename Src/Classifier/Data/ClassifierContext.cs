using System.Data.Entity;

namespace Classifier.Data
{
    public partial class ClassifierContext : DbContext
    {
        public ClassifierContext()
            : base("name=ClassifierDatabase")
        {
        }

        
        public virtual DbSet<DocumentCriteria> DocumentCriteria { get; set; }
        public virtual DbSet<DocumentTypes> DocumentTypes { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DocumentTypes>().HasKey(c => c.Id);
            modelBuilder.Entity<DocumentCriteria>().HasKey(c => c.Id);
            modelBuilder.Entity<DocumentTypes>().HasMany(c=>c.DocumentCriteria).WithRequired(c => c.DocumentType).HasForeignKey(c=>c.DocumentTypeId);
        }
    }
}
