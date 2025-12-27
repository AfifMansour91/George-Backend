using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("Account")]
public partial class Account
{
    [Key]
    public long Id { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool IsKosherShop { get; set; }

    public bool AllowWeighted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    [StringLength(250)]
    public string? StoreDomain { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(300)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? Zip { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(200)]
    public string? ManagerName { get; set; }

    [StringLength(250)]
    public string? ManagerEmail { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [StringLength(20)]
    public string WizardStatus { get; set; } = null!;

    [StringLength(20)]
    public string? WizardType { get; set; }

    public int WizardStep { get; set; }

    [StringLength(20)]
    public string ContentOwner { get; set; } = null!;

    [StringLength(50)]
    public string? Base44Id { get; set; }

    [StringLength(250)]
    public string? Base44CreatedBy { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<AccountBusinessType> AccountBusinessTypes { get; set; } = new List<AccountBusinessType>();

    [InverseProperty("Account")]
    public virtual ICollection<AccountCategory> AccountCategories { get; set; } = new List<AccountCategory>();

    [InverseProperty("Account")]
    public virtual ICollection<AccountEcomCredential> AccountEcomCredentials { get; set; } = new List<AccountEcomCredential>();

    [InverseProperty("Account")]
    public virtual ICollection<AccountProductImportStaging> AccountProductImportStagings { get; set; } = new List<AccountProductImportStaging>();

    [InverseProperty("Account")]
    public virtual ICollection<AccountProduct> AccountProducts { get; set; } = new List<AccountProduct>();

    [InverseProperty("Account")]
    public virtual ICollection<AccountUser> AccountUsers { get; set; } = new List<AccountUser>();

    [InverseProperty("Account")]
    public virtual ICollection<Site> Sites { get; set; } = new List<Site>();

    [InverseProperty("Account")]
    public virtual ICollection<SyncJob> SyncJobs { get; set; } = new List<SyncJob>();

    [InverseProperty("Account")]
    public virtual ICollection<WizardSession> WizardSessions { get; set; } = new List<WizardSession>();
}
