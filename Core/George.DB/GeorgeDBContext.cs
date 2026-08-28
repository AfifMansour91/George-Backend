using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Configuration;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace George.DB
{
	public class GeorgeDBContext : GeorgeDBContextBase
	{
		//***********************  Data members/Constants  ***********************//
		private const string PROP_IS_DELETED = "IsDeleted";
		private const string PROP_IS_UPDATE_TIME = "UpdateTime";
		private const string PROP_IS_CREATION_TIME = "CreationTime";
		private GeorgeStoredProcedures _storedProcedures;
		private bool _skipOnBeforeSaving = false; // Used to prevent calling twice to OnBeforeSaving() in the same flow.

		//**************************    Construction    **************************//
		
		//public GeorgeDBContext()
		//{
		//	this.ChangeTracker.LazyLoadingEnabled = false;
		//	_storedProcedures = new GeorgeStoredProcedures(this);
		//}

		public GeorgeDBContext(DbContextOptions<GeorgeDBContextBase> options) : base(options)
		{
			this.ChangeTracker.LazyLoadingEnabled = false;

			_storedProcedures = new GeorgeStoredProcedures(this);
		}


		//**************************    Properties    **************************//
		public bool SkipSave { get; set; }

		public GeorgeStoredProcedures StoredProcedures { get { return _storedProcedures; } }

		// MultiSite Phase 2 - per-site override layer (configured in MapNonScaffoldEntities).
		public virtual DbSet<ProductSiteOverride> ProductSiteOverride { get; set; }
		public virtual DbSet<ProductSiteVariantStock> ProductSiteVariantStock { get; set; }
		public virtual DbSet<ProductSiteCategory> ProductSiteCategory { get; set; }
		public virtual DbSet<ProductSiteImage> ProductSiteImage { get; set; }
		public virtual DbSet<ProductSiteWooId> ProductSiteWooId { get; set; }
		public virtual DbSet<ProductSiteVariantWooId> ProductSiteVariantWooId { get; set; }

		public virtual DbSet<CategorySiteWooId> CategorySiteWooId { get; set; }

		// DB Views mapping


		//*************************    Public/Protected Methods    *************************//
		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			// Override to verify that is it not implemented by the base.

			optionsBuilder.EnableDetailedErrors(true);
			//optionsBuilder.EnableSensitiveDataLogging(true);
			
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			// Force all non-defined delete behaviors to NoAction (override Scaffold-DbContext).
			foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(a => a.GetForeignKeys()))
			{
				if (fk.DeleteBehavior == DeleteBehavior.ClientSetNull)
					fk.DeleteBehavior = DeleteBehavior.NoAction;
			}

			// Configuring views without scaffolding.
			MapNonScaffoldEntities(modelBuilder);

			// Add query filters for soft-deleted entities.
			SetQueryFilters(modelBuilder);
		}

		public override int SaveChanges(bool acceptAllChangesOnSuccess)
		{
			OnBeforeSaving();

		#if DEBUG
			if (SkipSave)
				return 1;
		#endif

			return base.SaveChanges(acceptAllChangesOnSuccess);
		}

		public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
		{
			OnBeforeSaving();

#if DEBUG
			if (SkipSave)
				return Task<int>.Factory.StartNew(() => 1);
		#endif

			var res = base.SaveChangesAsync(cancellationToken);
			_skipOnBeforeSaving = false; // reset for next call.

			return res;
		}

		public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
		{
			OnBeforeSaving();

		#if DEBUG
			if (SkipSave)
				return Task<int>.Factory.StartNew(() => 1);
		#endif

			var res = base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
			_skipOnBeforeSaving = false; // reset for next call.

			return res;
		}

		/// <summary>
		/// Detaches all cashed (tracked) entities.
		/// </summary>
		public void ClearCache()
		{
			foreach (var entry in ChangeTracker.Entries().ToList())
			{
				entry.State = EntityState.Detached;
			}
		}

		//*************************    Private Methods    *************************//

		private void OnBeforeSaving()
		{
			if (_skipOnBeforeSaving)
				return;

			foreach (var entry in ChangeTracker.Entries())
			{
				switch (entry.State)
				{
					case EntityState.Added:
						HandleAdd(entry);
						break;

					case EntityState.Modified:
						HandleUpdate(entry);
						break;

					case EntityState.Deleted:
						HandleDelete(entry);
						break;
				}
			}

			_skipOnBeforeSaving = true;
		}

		private void HandleAdd(EntityEntry entry)
		{
			// Set the IsDeleted property to false.
			if (entry.Entity.GetType().GetProperty(PROP_IS_DELETED) != null)
				entry.CurrentValues[PROP_IS_DELETED] = false;

			var utcNow = DateTime.UtcNow;

			// Update the creation and update time.
			if (entry.Entity.GetType().GetProperty(PROP_IS_CREATION_TIME) != null)
				entry.CurrentValues[PROP_IS_CREATION_TIME] = utcNow;
			if (entry.Entity.GetType().GetProperty(PROP_IS_UPDATE_TIME) != null)
				entry.CurrentValues[PROP_IS_UPDATE_TIME] = utcNow;
		}

		private void HandleUpdate(EntityEntry entry)
		{
			// NOTE: The following causes a bug when deleting (find another way to do it).
			//// Set the IsDeleted property to false.
			//if (entry.Entity.GetType().GetProperty(PROP_IS_DELETED) != null)
			//	entry.CurrentValues[PROP_IS_DELETED] = false;

			if (entry.Entity.GetType().GetProperty(PROP_IS_UPDATE_TIME) != null)
			{
				// Update the update time.
				entry.CurrentValues[PROP_IS_UPDATE_TIME] = DateTime.UtcNow;

				// Do not change the creation time.
				if(entry.Entity.GetType().GetProperty(PROP_IS_CREATION_TIME) != null)
					entry.Property(PROP_IS_CREATION_TIME).IsModified = false;
				//entry.CurrentValues[PROP_IS_CREATIONTIME] = entry.OriginalValues[PROP_IS_CREATIONTIME];
			}
		}

		private void HandleDelete(EntityEntry entry)
		{
			if (entry.Entity.GetType().GetProperty(PROP_IS_DELETED) != null)
			{
				entry.State = EntityState.Modified;
				entry.CurrentValues[PROP_IS_DELETED] = true;

				if (entry.Entity.GetType().GetProperty(PROP_IS_UPDATE_TIME) != null)
				{
					// Update the update time.
					entry.CurrentValues[PROP_IS_UPDATE_TIME] = DateTime.UtcNow;

					// Do not change the creation time.
					entry.Property(PROP_IS_UPDATE_TIME).IsModified = false;
					//entry.CurrentValues[PROP_IS_CREATIONTIME] = entry.OriginalValues[PROP_IS_CREATIONTIME];
				}
			}
		}

		private void MapNonScaffoldEntities(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<OrderStatusHistory>(entity =>
			{
				entity.ToTable("OrderStatusHistory");
				entity.HasIndex(e => new { e.OrderId, e.OccurredAt }, "IX_OrderStatusHistory_OrderId_OccurredAt");
				entity.HasOne(d => d.Order)
					.WithMany(p => p.OrderStatusHistory)
					.HasForeignKey(d => d.OrderId)
					.OnDelete(DeleteBehavior.Cascade)
					.HasConstraintName("FK_OrderStatusHistory_Order");
			});

			modelBuilder.Entity<UserPreference>(entity =>
			{
				entity.HasKey(e => e.UserId);
				entity.Property(e => e.PreferencesJson).HasMaxLength(-1); // nvarchar(max)
				entity.HasOne(d => d.User)
					.WithOne(p => p.UserPreference)
					.HasForeignKey<UserPreference>(d => d.UserId)
					.OnDelete(DeleteBehavior.Cascade)
					.HasConstraintName("FK_UserPreference_User");
			});

			// MultiSite Phase 2 - per-site product override layer.
			modelBuilder.Entity<ProductSiteOverride>(entity =>
			{
				entity.ToTable("ProductSiteOverride");
				entity.HasOne(d => d.Product).WithMany(p => p.ProductSiteOverride)
					.HasForeignKey(d => d.ProductId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteOverride_Product");
				entity.HasOne(d => d.Site).WithMany()
					.HasForeignKey(d => d.SiteId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteOverride_Site");
			});

			modelBuilder.Entity<ProductSiteVariantStock>(entity =>
			{
				entity.ToTable("ProductSiteVariantStock");
				entity.HasOne(d => d.ProductVariant).WithMany(p => p.ProductSiteVariantStock)
					.HasForeignKey(d => d.ProductVariantId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteVariantStock_ProductVariant");
				entity.HasOne(d => d.Site).WithMany()
					.HasForeignKey(d => d.SiteId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteVariantStock_Site");
			});

			modelBuilder.Entity<ProductSiteCategory>(entity =>
			{
				entity.ToTable("ProductSiteCategory");
				entity.HasOne(d => d.Product).WithMany(p => p.ProductSiteCategory)
					.HasForeignKey(d => d.ProductId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteCategory_Product");
				entity.HasOne(d => d.Site).WithMany()
					.HasForeignKey(d => d.SiteId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteCategory_Site");
				entity.HasOne(d => d.Category).WithMany()
					.HasForeignKey(d => d.CategoryId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteCategory_Category");
			});

			modelBuilder.Entity<ProductSiteImage>(entity =>
			{
				entity.ToTable("ProductSiteImage");
				entity.HasOne(d => d.Product).WithMany(p => p.ProductSiteImage)
					.HasForeignKey(d => d.ProductId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteImage_Product");
				entity.HasOne(d => d.Site).WithMany()
					.HasForeignKey(d => d.SiteId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteImage_Site");
			});

			modelBuilder.Entity<ProductSiteWooId>(entity =>
			{
				entity.ToTable("ProductSiteWooId");
				entity.HasOne(d => d.Product).WithMany(p => p.ProductSiteWooId)
					.HasForeignKey(d => d.ProductId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteWooId_Product");
				entity.HasOne(d => d.Site).WithMany()
					.HasForeignKey(d => d.SiteId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteWooId_Site");
			});

			modelBuilder.Entity<ProductSiteVariantWooId>(entity =>
			{
				entity.ToTable("ProductSiteVariantWooId");
				entity.HasOne(d => d.ProductVariant).WithMany(p => p.ProductSiteVariantWooId)
					.HasForeignKey(d => d.ProductVariantId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteVariantWooId_ProductVariant");
				entity.HasOne(d => d.Site).WithMany()
					.HasForeignKey(d => d.SiteId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_ProductSiteVariantWooId_Site");
			});

			modelBuilder.Entity<CategorySiteWooId>(entity =>
			{
				entity.ToTable("CategorySiteWooId");
				entity.HasOne(d => d.Category).WithMany()
					.HasForeignKey(d => d.CategoryId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_CategorySiteWooId_Category");
				entity.HasOne(d => d.Site).WithMany()
					.HasForeignKey(d => d.SiteId)
					.OnDelete(DeleteBehavior.NoAction)
					.HasConstraintName("FK_CategorySiteWooId_Site");
			});
		}

		private void SetQueryFilters(ModelBuilder modelBuilder)
		{
			// Add query filters for soft-deleted entities.
			modelBuilder.Entity<User>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<Account>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<TemplateAttribute>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<Attribute>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<TemplateProductVariant>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<ProductVariant>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<Site>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<Category>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<BusinessType>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<BusinessTypeCategory>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<ProductOption>().HasQueryFilter(a => a.IsDeleted == false);
			modelBuilder.Entity<TemplateProductOption>().HasQueryFilter(a => a.IsDeleted == false);
            modelBuilder.Entity<Product>().HasQueryFilter(a => a.IsDeleted == false);
            modelBuilder.Entity<TemplateProduct>().HasQueryFilter(a => a.IsDeleted == false);
            modelBuilder.Entity<GlobalCategory>().HasQueryFilter(a => a.IsDeleted == false);
            modelBuilder.Entity<GlobalBrand>().HasQueryFilter(a => a.IsDeleted == false);
            modelBuilder.Entity<Media>().HasQueryFilter(a => a.IsDeleted == false);
            modelBuilder.Entity<Promotion>().HasQueryFilter(a => a.IsDeleted == false);
            modelBuilder.Entity<ProductSiteOverride>().HasQueryFilter(a => a.IsDeleted == false);
            modelBuilder.Entity<ProductSiteVariantStock>().HasQueryFilter(a => a.IsDeleted == false);
            //modelBuilder.Entity<Account>().HasQueryFilter(ent => EF.Property<bool>(ent, PROP_IS_DELETED) == false);

        }
	}
}
