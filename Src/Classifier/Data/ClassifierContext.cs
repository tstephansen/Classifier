using System.Data.Entity;
using System.Data.SqlClient;
using System.IO;
using Classifier.Core;

namespace Classifier.Data
{
    public class ClassifierContext : DbContext
    {
        public ClassifierContext() : base(BuildConnectionString())
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<ClassifierContext>());
        }

        public static string BuildConnectionString()
        {
            if(System.Environment.UserDomainName == "DEVOPS")
            {
                return @"Server=(localdb)\v11.0;Database=ClassifierDb;Integrated Security=true;MultipleActiveResultSets=True;App=EntityFramework;";
            }
            var builder = new SqlConnectionStringBuilder(@"Server=(localdb)\v11.0;Integrated Security=true;AttachDbFileName=|DataDirectory|ClassifierDb.mdf;")
            {
                AttachDBFilename = Path.Combine(Common.AppStorage, "ClassifierDb.mdf")
            };
            return builder.ConnectionString;
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
