using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using George.Common;
using George.DB;

namespace George.Data
{
	public class RegistryUnitStorage : StorageBase
	{
		//***********************  Data members/Constants  ***********************//


		//**************************    Construction    **************************//
		public RegistryUnitStorage(GeorgeDBContext dbContext, ILogger<RegistryUnitStorage> logger) : base(dbContext, logger)
		{
		}


		//*************************    Public Methods    *************************//

		public async Task<DataListResult<RegistryUnit>> GetRegistryUnitsAsync(RegistryUnitFilter filter, PagingExDto paging, CancellationToken cancelToken = default)
		{
			DataListResult<RegistryUnit> res = new DataListResult<RegistryUnit>();

			// Build the query.
			var query = _dbContext.RegistryUnits.AsNoTracking();

			// Filter
			if (filter.HasRegistrationMethod != null)
				query = query.Where(a => a.RegistrationMethodId != null);
			if (filter.HasLandUse != null)
				query = query.Where(a => a.LandUseId != null);
			if (filter.HasAddress != null)
				query = query.Where(a => a.Address != null);
			if (filter.HasArea != null)
				query = query.Where(a => a.Area != null);
			//if (filter.IsFilled != null)
			//	query = query.Where(a => a.IsFilled == filter.IsFilled.Value);
			


			if (filter.OwnershipTypeIds.HasValue())
				query = query.Where(a => filter.OwnershipTypeIds!.Contains((OwnershipType)a.OwnershipTypeId));
			if (filter.RegistrationMethodIds.HasValue())
				query = query.Where(a => a.RegistrationMethodId != null && filter.RegistrationMethodIds!.Contains((RegistrationMethod)a.RegistrationMethodId));
			if (filter.LandUseIds.HasValue())
				query = query.Where(a => a.LandUseId != null && filter.LandUseIds!.Contains(a.LandUseId.Value));
			if (filter.Block.HasValue)
			{
				query = query.Where(a => a.Block == filter.Block);
				if (filter.Parcel.HasValue)
					query = query.Where(a => a.Parcel == filter.Parcel);
			}
			if (filter.MinArea.HasValue)
				query = query.Where(a => a.Area >= filter.MinArea);
			if (filter.MaxArea.HasValue)
				query = query.Where(a => a.Area <= filter.MaxArea);
			if (filter.Address.HasValue())
				query = query.Where(a => a.Address != null && a.Address.Contains(filter.Address!));
			//if (filter.SearchTerm != null && filter.SearchTerm.HasValue())
			//	query = query.Where(a => a.FullName.Contains(filter.SearchTerm!) || (a.Email != null && a.Email.Contains(filter.SearchTerm!)));


			// Projection (all except the Data field).
			query = query.Select(a => new RegistryUnit() {
													Id = a.Id,
													OwnershipTypeId = a.OwnershipTypeId,
													RegistrationMethodId = a.RegistrationMethodId,
													LandUseId = a.LandUseId,
													Block = a.Block,
													Parcel = a.Parcel,
													Address = a.Address,
													Area = a.Area,
													IsFilled = a.IsFilled
												});

			// Get the total from the DB.
			if (paging.IncludeTotal)
				res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

			// Add sorting.
			switch (paging.SortField)
			{
				case SortField.OwnershipType:
					if (paging.SortOrder == SortOrder.Descending)
						query = query.OrderByDescending(a => a.OwnershipTypeId);
					else
						query = query.OrderBy(a => a.OwnershipTypeId);
					break;
				case SortField.RegistrationMethod:
					if (paging.SortOrder == SortOrder.Descending)
						query = query.OrderByDescending(a => a.RegistrationMethodId);
					else
						query = query.OrderBy(a => a.RegistrationMethodId);
					break;
				case SortField.LandUse:
					if (paging.SortOrder == SortOrder.Descending)
						query = query.OrderByDescending(a => a.LandUseId);
					else
						query = query.OrderBy(a => a.LandUseId);
					break;
				case SortField.Block:
					if (paging.SortOrder == SortOrder.Descending)
						query = query.OrderByDescending(a => a.Block);
					else
						query = query.OrderBy(a => a.Block);
					break;
				case SortField.Parcel:
					if (paging.SortOrder == SortOrder.Descending)
						query = query.OrderByDescending(a => a.Parcel);
					else
						query = query.OrderBy(a => a.Parcel);
					break;
				case SortField.Address:
					if (paging.SortOrder == SortOrder.Descending)
						query = query.OrderByDescending(a => a.Address);
					else
						query = query.OrderBy(a => a.Address);
					break;
				case SortField.Area:
					if (paging.SortOrder == SortOrder.Descending)
						query = query.OrderByDescending(a => a.Area);
					else
						query = query.OrderBy(a => a.Area);
					break;
				case SortField.IsFilled:
					if (paging.SortOrder == SortOrder.Descending)
						query = query.OrderByDescending(a => a.IsFilled);
					else
						query = query.OrderBy(a => a.IsFilled);
					break;
				default:
					query = query.OrderBy(a => a.Block).ThenBy(a => a.Parcel);
					break;
			}


			// Add paging.
			query = query.Skip(paging.Skip).Take(paging.Take);

			// Add includes.

			// Get the data from the DB.
			res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

			return res;
		}

		public async Task<RegistryUnit?> GetRegistryUnitAsync(int id, CancellationToken cancelToken = default)
		{
			return await _dbContext.RegistryUnits.AsNoTracking()
						.Where(a => a.Id == id)
						.Include(a => a.SubParcels)
						.Include(a => a.Media)
						.FirstOrDefaultAsync(cancelToken)
						.ConfigureAwait(false);
		}

		public async Task<RegistryUnit?> CreateRegistryUnitAsync(RegistryUnit model, CancellationToken cancelToken = default)
		{
			// Add the data to the DB.
			await _dbContext.RegistryUnits.AddAsync(model, cancelToken).ConfigureAwait(false);

			// Save to the DB.
			await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);

			return model;
		}

		public async Task<RegistryUnit?> UpdateRegistryUnitAsync(RegistryUnit model, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			var dbModel = await _dbContext.RegistryUnits
								.Where(a => a.Id == model.Id)
								.Include(a => a.SubParcels)
								.FirstOrDefaultAsync(cancelToken)
								.ConfigureAwait(false);
			if (dbModel != null)
			{
				// Update fields.
				dbModel.OwnershipTypeId = model.OwnershipTypeId;
				dbModel.RegistrationMethodId = model.RegistrationMethodId;
				dbModel.LandUseId = model.LandUseId;
				dbModel.Block = model.Block;
				dbModel.Parcel = model.Parcel;
				dbModel.Address = model.Address;
				dbModel.Area = model.Area;
				dbModel.Data = model.Data;

				if(dbModel.Address.HasValue())
					dbModel.UpdateTime = DateTime.UtcNow;

				if(model.SubParcels.HasValue())
				{
					dbModel.SubParcels.Clear(); // Remove all existing.
					model.SubParcels.ForEach(a => dbModel.SubParcels.Add(a)); // Add all.
				}

				// Save to the DB.
				await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
			}

			return dbModel;
		}

		public async Task<RegistryUnit?> UpdateRegistryUnitAddressAsync(int id, string? address, string? data, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			var dbModel = await _dbContext.RegistryUnits
								.Where(a => a.Id == id)
								.FirstOrDefaultAsync(cancelToken)
								.ConfigureAwait(false);
			if (dbModel != null)
			{
				// Update fields.
				if(address.HasValue()) // Do not update empty address.
					dbModel.Address = address;
				if(data.HasValue())
					dbModel.Data = data;
				dbModel.UpdateTime = DateTime.UtcNow;

				// Save to the DB.
				await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
			}

			return dbModel;
		}

		public async Task<RegistryUnit?> DeleteRegistryUnitAsync(int id, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			var dbModel = await _dbContext.RegistryUnits
								.Where(a => a.Id == id)
								.FirstOrDefaultAsync(cancelToken)
								.ConfigureAwait(false);
			if (dbModel != null)
			{
				// Delete the entity.
				_dbContext.RegistryUnits.Remove(dbModel);

				// Save to the DB.
				await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
			}

			return dbModel;
		}

		public async Task<List<RegistryUnit>> GetRegistryUnitsForUpdateAsync(PagingDto paging, CancellationToken cancelToken = default)
		{
			return await _dbContext.RegistryUnits.AsNoTracking()
							.Where(a => a.UpdateTime == null)
							.OrderBy(a => a.Id)
							.Skip(paging.Skip).Take(paging.Take)
							.ToListAsync(cancelToken).ConfigureAwait(false);

		}



		public async Task<DataListResult<Medium>> GetMediaAsync(int registryUnitId, PagingExDto paging, CancellationToken cancelToken = default)
		{
			DataListResult<Medium> res = new();

			var query = _dbContext.Media
						.Where(a => a.RegistryUnitId == registryUnitId);

			// Get the total from the DB.
			if (paging.IncludeTotal)
				res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

			// Add paging.
			query = query.Skip(paging.Skip).Take(paging.Take);

			// Get the data from the DB.
			res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

			return res;
		}

		public async Task<Medium?> GetMediumAsync(int id, CancellationToken cancelToken = default)
		{
			return await _dbContext.Media
						.Where(a => a.Id == id)
						.FirstOrDefaultAsync(cancelToken)
						.ConfigureAwait(false);
		}

		public async Task<Medium?> CreateMediumAsync(Medium model, CancellationToken cancelToken = default)
		{
			// Add the data to the DB.
			await _dbContext.Media.AddAsync(model, cancelToken).ConfigureAwait(false);

			// Save to the DB.
			await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);

			return model;
		}

		public async Task<Medium?> UpdateMediumAsync(Medium model, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			var dbModel = await _dbContext.Media
								.Where(a => a.Id == model.Id)
								.FirstOrDefaultAsync(cancelToken)
								.ConfigureAwait(false);
			if (dbModel != null)
			{
				// Update fields.
				dbModel.Name = model.Name;
				dbModel.Url = model.Url;
				dbModel.Description = model.Description;

				if(model.FileUrl != null)
					dbModel.FileUrl = model.FileUrl;

				// Save to the DB.
				await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
			}

			return dbModel;
		}

		public async Task<Medium?> DeleteMediumAsync(int id, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			var dbModel = await _dbContext.Media
								.Where(a => a.Id == id)
								.FirstOrDefaultAsync(cancelToken)
								.ConfigureAwait(false);
			if (dbModel != null)
			{
				// Delete the entity.
				_dbContext.Media.Remove(dbModel);

				// Save to the DB.
				await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
			}

			return dbModel;
		}

		//*************************    Private Methods    *************************//
	}
}

