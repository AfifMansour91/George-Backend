using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class GeorgeDBContextBase : DbContext
{
    public GeorgeDBContextBase(DbContextOptions<GeorgeDBContextBase> options)
        : base(options)
    {
    }

    public virtual DbSet<Color> Colors { get; set; }

    public virtual DbSet<LandUse> LandUses { get; set; }

    public virtual DbSet<Medium> Media { get; set; }

    public virtual DbSet<RegistryUnit> RegistryUnits { get; set; }

    public virtual DbSet<SubParcel> SubParcels { get; set; }

    public virtual DbSet<SystemConfiguration> SystemConfigurations { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Color>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<LandUse>(entity =>
        {
            entity.Property(e => e.Id).ValueGeneratedNever();
        });

        modelBuilder.Entity<Medium>(entity =>
        {
            entity.HasOne(d => d.RegistryUnit).WithMany(p => p.Media).HasConstraintName("FK_Medium_RegistryUnit");
        });

        modelBuilder.Entity<RegistryUnit>(entity =>
        {
            entity.Property(e => e.IsFilled).HasComputedColumnSql("(case when [Address] IS NOT NULL AND [Area] IS NOT NULL AND [LandUseId] IS NOT NULL then (1) else (0) end)", true);

            entity.HasOne(d => d.LandUse).WithMany(p => p.RegistryUnits).HasConstraintName("FK_RegistryUnit_LandUse");
        });

        modelBuilder.Entity<SubParcel>(entity =>
        {
            entity.HasOne(d => d.RegistryUnit).WithMany(p => p.SubParcels).HasConstraintName("FK_SubParcel_RegistryUnit");
        });

        modelBuilder.Entity<SystemConfiguration>(entity =>
        {
            entity.HasIndex(e => e.Key, "IX_SystemConfiguration_Key_Unique")
                .IsUnique()
                .IsClustered();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(e => e.Email, "IX_User_Email_Unique")
                .IsUnique()
                .HasFilter("([IsDeleted]=(0) AND [Email] IS NOT NULL)");

            entity.Property(e => e.FullName).HasComputedColumnSql("(([Firstname]+' ')+[LastName])", true);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
