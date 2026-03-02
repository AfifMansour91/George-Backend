using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using George.DB;

namespace George.Data
{
	public class UserPreferenceStorage : StorageBase
	{
		public UserPreferenceStorage(GeorgeDBContext dbContext, ILogger<UserPreferenceStorage> logger)
			: base(dbContext, logger)
		{
		}

		public async Task<string?> GetPreferencesJsonAsync(int userId, CancellationToken cancelToken = default)
		{
			var row = await _dbContext.UserPreference
				.AsNoTracking()
				.Where(p => p.UserId == userId)
				.Select(p => p.PreferencesJson)
				.FirstOrDefaultAsync(cancelToken);
			return row;
		}

		public async Task SavePreferencesJsonAsync(int userId, string? preferencesJson, CancellationToken cancelToken = default)
		{
			var existing = await _dbContext.UserPreference
				.FirstOrDefaultAsync(p => p.UserId == userId, cancelToken);

			if (existing != null)
			{
				existing.PreferencesJson = preferencesJson;
			}
			else
			{
				_dbContext.UserPreference.Add(new UserPreference
				{
					UserId = userId,
					PreferencesJson = preferencesJson
				});
			}

			await _dbContext.SaveChangesAsync(cancelToken);
		}
	}
}
