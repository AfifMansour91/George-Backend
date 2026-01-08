using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using George.Common;
using George.Common.Utils;
using George.DB;

using Task = System.Threading.Tasks.Task;

namespace George.Data
{
	public class UserStorage : StorageBase
	{
		//***********************  Data members/Constants  ***********************//


		//**************************    Construction    **************************//
		public UserStorage(GeorgeDBContext dbContext, ILogger<UserStorage> logger) : base(dbContext, logger)
		{

		}


		//*************************    Public Methods    *************************//
		
		public async Task<DataListResult<User>> GetUsersAsync(UserFilter filter, PagingExDto paging, CancellationToken cancelToken = default)
		{
			DataListResult<User> res = new DataListResult<User>();

			// Build the query.
			var query = _dbContext.Users.AsNoTracking();

			// Filter
			if (filter.StatusId.HasValue)
				query = query.Where(a => a.StatusId == (int)filter.StatusId!);
			if (filter.Name.HasValue())
				query = query.Where(a => a.FullName.Contains(filter.Name!));
			if (filter.SearchTerm != null && filter.SearchTerm.HasValue())
				query = query.Where(a => a.FullName.Contains(filter.SearchTerm!) || (a.Email != null && a.Email.Contains(filter.SearchTerm!)));

			// Get the total from the DB.
			if (paging.IncludeTotal)
				res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

			// Add sorting.
			query = query.OrderBy(a => a.FullName);

			// Add paging.
			query = query.Skip(paging.Skip).Take(paging.Take);

			// Add includes.

			// Get the data from the DB.
			res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);
			if (res.Items.HasValue())
			{
				// Check for user dependencies.
				//List<int> userIds = res.Items.Select(a => a.Id).ToList();
				//var usersDependencies = await _dbContext.Users.AsNoTracking()
				//		.Where(a => userIds.Contains(a.Id))
				//		.Select(a => new {
				//			Id = a.Id
				//		})
				//		.ToListAsync(cancelToken);
				//if(usersDependencies.HasValue())
				//{
				//	foreach (var dependency in usersDependencies)
				//	{
				//		var user = res.Items.FirstOrDefault(a => a.Id == dependency.Id);
				//		if (user != null)
				//			user.HasOpenAlert = dependency.HasOpenAlert;
				//	}
				//}
			}


			return res;
		}


		//public async Task<User?> GetUserAsync(int id, CancellationToken cancelToken = default)
		//{
		//	return await _dbContext.Users.AsNoTracking()
		//					.Include(a => a.SiteUsers)
		//					.Include(a => a.AccountUsers).ThenInclude(b => b.Account)
		//					.Include(a => a.UserQualifications)
		//					.FirstOrDefaultAsync(a => a.Id == id, cancelToken);
		//}

		public async Task<User?> GetUserAsync(int id, CancellationToken cancelToken = default)
		{
			return await _dbContext.Users.AsNoTracking()
				.Where(a => a.Id == id)
				.FirstOrDefaultAsync(cancelToken);
		}

		public async Task<User?> GetThinUserAsync(int id, CancellationToken cancelToken = default)
		{
			return await _dbContext.Users.AsNoTracking()
							.FirstOrDefaultAsync(a => a.Id == id, cancelToken);
		}

		public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancelToken = default)
		{
			// NOTE: Email is a unique index so only one user can exist in the DB.

			if (!email.HasValue())
				return null;

			// Get the data from the DB.
			return await _dbContext.Users.AsNoTracking()
					.FirstOrDefaultAsync(a => a.Email == email, cancelToken).ConfigureAwait(false);
        }

        public async Task<User?> GetUserByPhoneAsync(string phone, CancellationToken cancelToken = default)
        {
            return await _dbContext.Users.AsNoTracking()
                            .FirstOrDefaultAsync(a => a.Phone == phone, cancelToken);
        }

        //public async Task<string?> SetLoginUserOtpAsync(int id, CancellationToken cancelToken = default)
        //{
        //    // Set the otp in the DB.
        //    return await _dbContext.StoredProcedures.SetLoginUserOtpAsync(id, SysConfig.Data.OtpExpirationInMin, cancelToken).ConfigureAwait(false);
        //}


        public async Task<bool> IsEmailExistAsync(string email, CancellationToken cancelToken = default)
		{
			if (!email.HasValue())
				return false;

			return await _dbContext.Users.AsNoTracking()
							.AnyAsync(a => a.Email == email, cancelToken);
		}

		public async Task<User?> GetUserByCredentialsAsync(string email, string password, CancellationToken cancelToken = default)
		{
			// Get user by email
			var user = await _dbContext.Users.AsNoTracking()
							.FirstOrDefaultAsync(a => a.Email == email, cancelToken);
			
			if (user == null || string.IsNullOrWhiteSpace(user.Password))
				return null;

			// Verify password hash
			// Support both hashed and plain text passwords (for migration)
			bool isPasswordValid = false;
			if (user.Password.Contains(':'))
			{
				// Password is hashed (format: "salt:hash")
				isPasswordValid = Cryptography.VerifyPasswordHash(password, user.Password);
			}
			else
			{
				// Plain text password (legacy support during migration)
				isPasswordValid = user.Password == password;
			}

			return isPasswordValid ? user : null;
		}


		public async Task<string?> GetUserNameAsync(int id, CancellationToken cancelToken = default)
		{
			return await _dbContext.Users.AsNoTracking()
							.Where(a => a.Id == id)
							.Select(a => a.FullName)
							.FirstOrDefaultAsync(cancelToken);
		}

		public async Task<Common.UserStatus?> GetUserStatusAsync(int id, CancellationToken cancelToken = default)
		{
			return await _dbContext.Users.AsNoTracking()
							.Where(a => a.Id == id)
							.Select(a => (Common.UserStatus?)a.StatusId)
							.FirstOrDefaultAsync(cancelToken);
		}

		public async Task<User?> UpdateUserProfileAsync(User model, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			User? dbModel = await _dbContext.Users
										.Where(a => a.Id == model.Id)
										.FirstOrDefaultAsync(cancelToken);
			if (dbModel != null)
			{
				// Update fields.
				dbModel.FirstName = model.FirstName;
				dbModel.LastName = model.LastName;

				dbModel.Email = model.Email;
				dbModel.IsEmailVerified = model.IsEmailVerified;
				//dbModel.IsEmailVerified = model.Email.HasValue(); // TODO: TEMP CODE UNTIL WE WILL HAVE EMAIL INTEGRATION.


				dbModel.StatusId = model.StatusId;


				// Save to the DB.
				await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
			}

			return dbModel;
		}

		public async Task<bool> IsEmailAvailableAsync(string email, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			bool isExists = await _dbContext.Users.AsNoTracking()
							.AnyAsync(a => a.Email == email, cancelToken).ConfigureAwait(false);

			return !isExists;
		}

		public async Task<User?> RemoveRefreshTokenAsync(int userId, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			var dbModel = await _dbContext.Users
									.Where(a => a.Id == userId)
									.FirstOrDefaultAsync(cancelToken);
			if (dbModel != null)
			{
				// Update fields.
				dbModel.RefreshToken = null;
				dbModel.RefreshTokenExpiration = null;

				// Save to the DB.
				await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
			}

			return dbModel;
		}

		
		public async Task UpdateUserLockoutFailCountAsync(int id, int lockoutFailCount, DateTime? lockoutExpiration = null, CancellationToken cancelToken = default)
		{
			// Check if the model is already in the local cache (no DB access).
			var model = _dbContext.Users.Local.FirstOrDefault(a => a.Id == id);
			if (model == null)
				model = new User() { Id = id, LockoutFailCount = lockoutFailCount, LockoutExpiration = lockoutExpiration };
			else
			{
				model.LockoutFailCount = lockoutFailCount;
				model.LockoutExpiration = lockoutExpiration;
			}

			// Attach the model to the DB context if it is not.
			if (_dbContext.Entry(model).State == EntityState.Detached)
				_dbContext.Attach(model);

			// Update only one property.
			_dbContext.Entry(model).Property(a => a.LockoutFailCount).IsModified = true;
			_dbContext.Entry(model).Property(a => a.LockoutExpiration).IsModified = true;

			// Save to the DB.
			await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
		}

		public async Task<User?> UpdateUserLoginAsync(int id, string refreshToken, DateTime refreshTokenExpiration, bool isSetUserEmailOtpToValid = false, Common.UserStatus? statusId = null, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			User? dbModel = await _dbContext.Users
									.Where(a => a.Id == id)
									.FirstOrDefaultAsync(cancelToken).ConfigureAwait(false);
			if (dbModel != null)
			{
				// Update fields.

				// Set the user email otp to valid?
				if (isSetUserEmailOtpToValid) // NOTE: This is just after we verified the user's email otp.
				{
					// Update fields.
					dbModel.IsEmailVerified = true;
					//dbModel.EmailVerificationToken = null;

					//// If the phone is also verified, then set the user as active.
					//if(dbModel.IsPhoneVerified  && dbModel.StatusId == (int)UserStatus.Pending)
					//	dbModel.StatusId = (int)UserStatus.Active;
				}

				// Update fields.
				dbModel.LastLoginDate = DateTime.UtcNow;
				dbModel.LockoutFailCount = 0;
				dbModel.RefreshToken = refreshToken;
				dbModel.RefreshTokenExpiration = refreshTokenExpiration;
				if (statusId.HasValue)
					dbModel.StatusId = (int)statusId.Value;

				// Save to the DB.
				await _dbContext.SaveChangesAsync(cancelToken);
			}

			return dbModel;
		}

		public async Task<User?> DeleteUserAsync(int id, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			var dbModel = await _dbContext.Users
								.FirstOrDefaultAsync(a => a.Id == id, cancelToken).ConfigureAwait(false);
			if (dbModel != null)
			{
				// Delete the entity.
				_dbContext.Users.Remove(dbModel);

				// Save to the DB.
				await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
			}

			return dbModel;
		}

		public async Task<User?> BlockUserAsync(int id, bool isBlocked, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			var dbModel = await _dbContext.Users
								.FirstOrDefaultAsync(a => a.Id == id, cancelToken).ConfigureAwait(false);
			if (dbModel != null)
			{
				// Delete the entity.
				dbModel.StatusId = isBlocked ? (int)Common.UserStatus.Blocked : (int)Common.UserStatus.Active;

				// Save to the DB.
				await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
			}

			return dbModel;
		}

		public async Task<bool> UserHasDependenciesAsync(int id, CancellationToken cancelToken = default)
		{
			return false;
		}

		public async Task<User> CreateUserAsync(User user, CancellationToken cancelToken = default)
		{
			// Set required fields
			if (string.IsNullOrWhiteSpace(user.FullName))
			{
				user.FullName = $"{user.FirstName} {user.LastName}".Trim();
			}

			if (user.GuidId == Guid.Empty)
			{
				user.GuidId = Guid.NewGuid();
			}

			if (user.CreationTime == default)
			{
				user.CreationTime = DateTime.UtcNow;
			}

			if (user.StatusId == 0)
			{
				user.StatusId = (int)Common.UserStatus.Active;
			}

			if (user.RoleId == 0)
			{
				user.RoleId = (int)Common.UserRole.SiteAdmin;
			}

			// Add to DB
			_dbContext.Users.Add(user);
			await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);

			return user;
		}

		public async Task<User?> UpdateUserAsync(User user, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			User? dbModel = await _dbContext.Users
									.Where(a => a.Id == user.Id)
									.FirstOrDefaultAsync(cancelToken);
			if (dbModel == null)
				return null;

			// Update fields.
			if (user.FirstName.HasValue())
			{
				dbModel.FirstName = user.FirstName;
			}

			if (user.LastName != null)
			{
				dbModel.LastName = user.LastName;
			}

			// Update FullName if FirstName or LastName changed
			if (user.FirstName.HasValue() || user.LastName != null)
			{
				dbModel.FullName = $"{dbModel.FirstName} {dbModel.LastName}".Trim();
			}

			if (user.Email != null)
			{
				dbModel.Email = user.Email;
			}

		if (user.Phone != null)
		{
			dbModel.Phone = user.Phone;
		}

		if (user.Password != null)
		{
			dbModel.Password = user.Password;
		}

		if (user.AccountId.HasValue)
		{
			dbModel.AccountId = user.AccountId;
		}

			if (user.RoleId > 0)
			{
				dbModel.RoleId = user.RoleId;
			}

			if (user.StatusId > 0)
			{
				dbModel.StatusId = user.StatusId;
			}

			if (user.AvatarUrl != null)
			{
				dbModel.AvatarUrl = user.AvatarUrl;
			}

			// Update timestamp
			dbModel.UpdatedDate = DateTime.UtcNow;

			// Save to the DB.
			await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);

			return dbModel;
		}

		public async Task<User?> UpdateUserPasswordAsync(int userId, string passwordHash, CancellationToken cancelToken = default)
		{
			// Get the data from the DB.
			User? dbModel = await _dbContext.Users
									.Where(a => a.Id == userId)
									.FirstOrDefaultAsync(cancelToken);
			if (dbModel == null)
				return null;

			// Update password
			dbModel.Password = passwordHash;
			dbModel.UpdatedDate = DateTime.UtcNow;

			// Save to the DB.
			await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);

			return dbModel;
		}


		
	}
}

