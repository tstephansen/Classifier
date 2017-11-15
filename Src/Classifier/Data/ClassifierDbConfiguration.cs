using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Classifier.Data
{
    internal class ClassifierDbConfiguration : DbConfiguration
    {
        public ClassifierDbConfiguration()
        {
            SetDatabaseInitializer(new MigrateDatabaseToLatestVersion<ClassifierContext, Migrations.Configuration>());
        }
    }
}
