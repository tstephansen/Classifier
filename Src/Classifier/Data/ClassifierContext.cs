using Classifier.Migrations;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Deployment.Application;
using System.IO;

namespace Classifier.Data
{
    public partial class ClassifierContext : DbContext
    {
        public ClassifierContext() : base(BuildConnectionString())
        {
            Database.SetInitializer(new ClassifierContextInitializer());
        }

        public static string BuildConnectionString()
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                var builder = new SqlConnectionStringBuilder(@"Server=(localdb)\v11.0;Integrated Security=true;AttachDbFileName=|DataDirectory|ClassifierDb.mdf;")
                {
                    AttachDBFilename = Path.Combine(Directory.GetCurrentDirectory(), "ClassifierDb.mdf")
                };
                return builder.ConnectionString;
            }
            else
            {
                var builder = new SqlConnectionStringBuilder(@"Server=(localdb)\v11.0;Integrated Security=true;AttachDbFileName=|DataDirectory|ClassifierDb.mdf;")
                {
                    AttachDBFilename = Path.Combine(ApplicationDeployment.CurrentDeployment.DataDirectory, "ClassifierDb.mdf")
                };
                return builder.ConnectionString;
            }
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
