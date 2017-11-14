namespace Classifier.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialMigration : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.DocumentCriterias",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        CriteriaName = c.String(),
                        DocumentTypeId = c.Guid(nullable: false),
                        CriteriaBytes = c.String(),
                        Height = c.Int(nullable: false),
                        MatchThreshold = c.Int(nullable: false),
                        PositionX = c.Int(nullable: false),
                        PositionY = c.Int(nullable: false),
                        Width = c.Int(nullable: false),
                        BaseHeight = c.Int(nullable: false),
                        BaseWidth = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.DocumentTypes", t => t.DocumentTypeId, cascadeDelete: true)
                .Index(t => t.DocumentTypeId);
            
            CreateTable(
                "dbo.DocumentTypes",
                c => new
                    {
                        Id = c.Guid(nullable: false),
                        DocumentType = c.String(),
                        AverageScore = c.Long(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.DocumentCriterias", "DocumentTypeId", "dbo.DocumentTypes");
            DropIndex("dbo.DocumentCriterias", new[] { "DocumentTypeId" });
            DropTable("dbo.DocumentTypes");
            DropTable("dbo.DocumentCriterias");
        }
    }
}
