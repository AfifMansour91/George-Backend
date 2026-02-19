using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace George.DB;

/// <summary>
/// Stores per-user UI preferences (e.g. product list view/filters) as JSON.
/// One row per user.
/// </summary>
[Table("UserPreference")]
public class UserPreference
{
	[Key]
	public int UserId { get; set; }

	/// <summary>
	/// JSON object: keys like "myProducts_viewPrefs", "globalCatalog_viewPrefs", values are the preference objects.
	/// </summary>
	public string? PreferencesJson { get; set; }
}
