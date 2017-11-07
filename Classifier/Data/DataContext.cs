using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Data
{
    public class DataContext : DbContext
    {
        public DataContext()
        {

        }

        public DbSet<DocumentTypes> DocumentTypes { get; set; }
        public DbSet<DocumentCriteria> DocumentCriteria { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=ClassifierDb;Trusted_Connection=True;");
        }
    }
}
