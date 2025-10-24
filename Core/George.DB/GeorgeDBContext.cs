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
		}

		private void SetQueryFilters(ModelBuilder modelBuilder)
		{
			// Add query filters for soft-deleted entities.
			modelBuilder.Entity<User>().HasQueryFilter(a => a.IsDeleted == false);
			//modelBuilder.Entity<Account>().HasQueryFilter(ent => EF.Property<bool>(ent, PROP_IS_DELETED) == false);

		}
	}
}
