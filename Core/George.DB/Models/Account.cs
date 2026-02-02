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

    [StringLength(500)]
    public string? Website { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool IsKosherShop { get; set; }

    public bool AllowWeighted { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<Brand> Brands { get; set; } = new List<Brand>();

    [InverseProperty("Account")]
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

    [ForeignKey("ContentOwnerId")]
    [InverseProperty("Accounts")]
    public virtual ContentOwner? ContentOwner { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("AccountCreationUsers")]
    public virtual User? CreationUser { get; set; }

    [ForeignKey("ManagerId")]
    [InverseProperty("AccountManagers")]
    public virtual User? Manager { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<AccountMedia> AccountMedia { get; set; } = new List<AccountMedia>();

    [InverseProperty("Account")]
    public virtual ICollection<Site> Sites { get; set; } = new List<Site>();

    [ForeignKey("StatusId")]
    [InverseProperty("Accounts")]
    public virtual AccountStatus? StatusNavigation { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();

    [InverseProperty("Account")]
    public virtual ICollection<Tag> Tags { get; set; } = new List<Tag>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("AccountUpdateUsers")]
    public virtual User? UpdateUser { get; set; }

    [InverseProperty("Account")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();

    [ForeignKey("WizardStatusId")]
    [InverseProperty("Accounts")]
    public virtual WizardStatus? WizardStatus { get; set; }

    [ForeignKey("WizardTypeId")]
    [InverseProperty("Accounts")]
    public virtual WizardType? WizardType { get; set; }
}
