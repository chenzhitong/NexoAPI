﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NexoAPI.Data;

#nullable disable

namespace NexoAPI.Migrations
{
    [DbContext(typeof(NexoAPIContext))]
    [Migration("20230324034903_InWitness")]
    partial class InWitness
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("NexoAPI.Models.Account", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Address")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Owners")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("PublicKeys")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Threshold")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.ToTable("Account");
                });

            modelBuilder.Entity("NexoAPI.Models.Remark", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<int>("AccountId")
                        .HasColumnType("int");

                    b.Property<DateTime>("CreateTime")
                        .HasColumnType("datetime2");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("RemarkName")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("UserId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.HasIndex("UserId");

                    b.ToTable("Remark");
                });

            modelBuilder.Entity("NexoAPI.Models.SignResult", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<bool>("Approved")
                        .HasColumnType("bit");

                    b.Property<bool>("InWitness")
                        .HasColumnType("bit");

                    b.Property<string>("Signature")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("SignerId")
                        .HasColumnType("int");

                    b.Property<int>("TransactionId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("SignerId");

                    b.HasIndex("TransactionId");

                    b.ToTable("SignResult");
                });

            modelBuilder.Entity("NexoAPI.Models.Transaction", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<int>("AccountId")
                        .HasColumnType("int");

                    b.Property<string>("Amount")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("ContractHash")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreateTime")
                        .HasColumnType("datetime2");

                    b.Property<string>("Creator")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Destination")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("ExecuteTime")
                        .HasColumnType("datetime2");

                    b.Property<string>("FeePayer")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Hash")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Operation")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Params")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("RawData")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<int>("Status")
                        .HasColumnType("int");

                    b.Property<int>("Type")
                        .HasColumnType("int");

                    b.Property<long>("ValidUntilBlock")
                        .HasColumnType("bigint");

                    b.HasKey("Id");

                    b.HasIndex("AccountId");

                    b.ToTable("Transaction");
                });

            modelBuilder.Entity("NexoAPI.Models.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("Address")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<DateTime>("CreateTime")
                        .HasColumnType("datetime2");

                    b.Property<string>("PublicKey")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.Property<string>("Token")
                        .IsRequired()
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.ToTable("User");
                });

            modelBuilder.Entity("NexoAPI.Models.Remark", b =>
                {
                    b.HasOne("NexoAPI.Models.Account", "Account")
                        .WithMany("Remark")
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("NexoAPI.Models.User", "User")
                        .WithMany("Remark")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Account");

                    b.Navigation("User");
                });

            modelBuilder.Entity("NexoAPI.Models.SignResult", b =>
                {
                    b.HasOne("NexoAPI.Models.User", "Signer")
                        .WithMany("SignResult")
                        .HasForeignKey("SignerId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("NexoAPI.Models.Transaction", "Transaction")
                        .WithMany("SignResult")
                        .HasForeignKey("TransactionId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Signer");

                    b.Navigation("Transaction");
                });

            modelBuilder.Entity("NexoAPI.Models.Transaction", b =>
                {
                    b.HasOne("NexoAPI.Models.Account", "Account")
                        .WithMany()
                        .HasForeignKey("AccountId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Account");
                });

            modelBuilder.Entity("NexoAPI.Models.Account", b =>
                {
                    b.Navigation("Remark");
                });

            modelBuilder.Entity("NexoAPI.Models.Transaction", b =>
                {
                    b.Navigation("SignResult");
                });

            modelBuilder.Entity("NexoAPI.Models.User", b =>
                {
                    b.Navigation("Remark");

                    b.Navigation("SignResult");
                });
#pragma warning restore 612, 618
        }
    }
}
