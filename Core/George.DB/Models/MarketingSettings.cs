using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>Account-level marketing guard rules (spec §5.2-5.3, §10.3). No row = the defaults below.</summary>
[Index("AccountId", Name = "UQ_MarketingSettings_AccountId", IsUnique = true)]
public partial class MarketingSettings
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime UpdateTime { get; set; }

    /// <summary>"HH:mm" Israel time.</summary>
    [StringLength(5)]
    public string SendWindowStart { get; set; } = "09:00";

    [StringLength(5)]
    public string SendWindowEnd { get; set; } = "20:00";

    public bool BlockShabbatAndHolidays { get; set; } = true;

    public int FrequencyCapCount { get; set; } = 2;

    public int FrequencyCapDays { get; set; } = 7;

    public int AttributionWindowHours { get; set; } = 72;

    public bool SkipOrderedToday { get; set; } = true;
}
