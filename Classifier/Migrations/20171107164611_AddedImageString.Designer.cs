﻿// <auto-generated />
using Classifier.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Internal;
using System;

namespace Classifier.Migrations
{
    [DbContext(typeof(DataContext))]
    [Migration("20171107164611_AddedImageString")]
    partial class AddedImageString
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "2.0.0-rtm-26452")
                .HasAnnotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn);

            modelBuilder.Entity("Classifier.Data.DocumentCriteria", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("CriteriaBytes");

                    b.Property<string>("CriteriaName");

                    b.Property<Guid>("DocumentTypeId");

                    b.HasKey("Id");

                    b.HasIndex("DocumentTypeId");

                    b.ToTable("DocumentCriteria");
                });

            modelBuilder.Entity("Classifier.Data.DocumentTypes", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<string>("DocumentType");

                    b.HasKey("Id");

                    b.ToTable("DocumentTypes");
                });

            modelBuilder.Entity("Classifier.Data.DocumentCriteria", b =>
                {
                    b.HasOne("Classifier.Data.DocumentTypes", "DocumentType")
                        .WithMany("Criteria")
                        .HasForeignKey("DocumentTypeId")
                        .OnDelete(DeleteBehavior.Cascade);
                });
#pragma warning restore 612, 618
        }
    }
}
