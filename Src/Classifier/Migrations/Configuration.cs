using System.Data.Entity.Migrations;
#pragma warning disable S125

namespace Classifier.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<Classifier.Data.ClassifierContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }
    }
}
