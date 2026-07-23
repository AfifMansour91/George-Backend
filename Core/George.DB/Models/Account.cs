using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class Account
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    public Guid GuidId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(200)]
    public string? City { get; set; }

    [StringLength(200)]
    public string? State { get; set; }

    [StringLength(50)]
    public string? Zip { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    public int? ManagerId { get; set; }

    [StringLength(200)]
    public string? ManagerName { get; set; }

    [StringLength(250)]
    public string ManagerEmail { get; set; } = null!;

    public int? StatusId { get; set; }

    public int? WizardStatusId { get; set; }

    public int? WizardTypeId { get; set; }

    public int? WizardStep { get; set; }

    public int? ContentOwnerId { get; set; }

    [StringLength(1000)]
    public string? LogoUrl { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool IsKosherShop { get; set; }

    public bool AllowWeighted { get; set; }

    [StringLength(500)]
    public string? Website { get; set; }

    public bool KioskEnabled { get; set; }

    /// <summary>Default low-stock threshold for weighted-style (kg) products in this account.</summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? DefaultLowStockThresholdWeighted { get; set; }

    /// <summary>Default low-stock threshold for unit-count products in this account.</summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? DefaultLowStockThresholdUnits { get; set; }

    /// <summary>Default duration in days for the &quot;חדש&quot; label end date when enabled (placeholder in product form).</summary>
    public int DefaultNewLabelDays { get; set; } = 7;

    [InverseProperty("Account")]
    public virtual ICollection<AccountMedia> AccountMedia { get; set; } = new List<AccountMedia>();

    [InverseProperty("Account")]
    public virtual ICollection<AccountNotificationSettings> AccountNotificationSettings { get; set; } = new List<AccountNotificationSettings>();

    [InverseProperty("Account")]
    public virtual ICollection<AccountWizardStepData> AccountWizardStepData { get; set; } = new List<AccountWizardStepData>();

    [InverseProperty("Account")]
    public virtual ICollection<Brand> Brand { get; set; } = new List<Brand>();

    [InverseProperty("Account")]
    public virtual ICollection<Category> Category { get; set; } = new List<Category>();

    [ForeignKey("ContentOwnerId")]
    [InverseProperty("Account")]
    public virtual ContentOwner? ContentOwner { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("AccountCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("Account")]
    public virtual KioskSettings? KioskSettings { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<KioskSettingsHomeImage> KioskSettingsHomeImage { get; set; } = new List<KioskSettingsHomeImage>();

    [ForeignKey("ManagerId")]
    [InverseProperty("AccountManager")]
    public virtual User? Manager { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<Media> Media { get; set; } = new List<Media>();

    [InverseProperty("Account")]
    public virtual ICollection<Order> Order { get; set; } = new List<Order>();

    [InverseProperty("Account")]
    public virtual ICollection<Site> Site { get; set; } = new List<Site>();

    [ForeignKey("StatusId")]
    [InverseProperty("Account")]
    public virtual AccountStatus? StatusNavigation { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<Supplier> Supplier { get; set; } = new List<Supplier>();

    [InverseProperty("Account")]
    public virtual ICollection<Tag> Tag { get; set; } = new List<Tag>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("AccountUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<User> User { get; set; } = new List<User>();

    [ForeignKey("WizardStatusId")]
    [InverseProperty("Account")]
    public virtual WizardStatus? WizardStatus { get; set; }

    [ForeignKey("WizardTypeId")]
    [InverseProperty("Account")]
    public virtual WizardType? WizardType { get; set; }
}
