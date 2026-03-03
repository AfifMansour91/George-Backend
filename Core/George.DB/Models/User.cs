using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class User
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

    public int RoleId { get; set; }

    public int? AccountId { get; set; }

    public int StatusId { get; set; }

    [StringLength(50)]
    public string FirstName { get; set; } = null!;

    [StringLength(50)]
    public string LastName { get; set; } = null!;

    [StringLength(101)]
    public string FullName { get; set; } = null!;

    [StringLength(250)]
    public string? Email { get; set; }

    public bool IsEmailVerified { get; set; }

    [StringLength(250)]
    public string? Password { get; set; }

    [StringLength(50)]
    public string? Otp { get; set; }

    [Precision(0)]
    public DateTime? LastLoginDate { get; set; }

    public int LockoutFailCount { get; set; }

    [Precision(0)]
    public DateTime? LockoutExpiration { get; set; }

    [StringLength(250)]
    public string? RefreshToken { get; set; }

    [Precision(0)]
    public DateTime? RefreshTokenExpiration { get; set; }

    [StringLength(50)]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? AvatarUrl { get; set; }

    public string? Notes { get; set; }

    [Precision(0)]
    public DateTime? OtpExpiration { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("User")]
    public virtual Account? Account { get; set; }

    [InverseProperty("CreationUser")]
    public virtual ICollection<Account> AccountCreationUser { get; set; } = new List<Account>();

    [InverseProperty("Manager")]
    public virtual ICollection<Account> AccountManager { get; set; } = new List<Account>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<AccountMedia> AccountMedia { get; set; } = new List<AccountMedia>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Account> AccountUpdateUser { get; set; } = new List<Account>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Attribute> AttributeCreationUser { get; set; } = new List<Attribute>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Attribute> AttributeUpdateUser { get; set; } = new List<Attribute>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Brand> BrandCreationUser { get; set; } = new List<Brand>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Brand> BrandUpdateUser { get; set; } = new List<Brand>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<BusinessType> BusinessTypeCreationUser { get; set; } = new List<BusinessType>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<BusinessType> BusinessTypeUpdateUser { get; set; } = new List<BusinessType>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Category> CategoryCreationUser { get; set; } = new List<Category>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Category> CategoryUpdateUser { get; set; } = new List<Category>();

    [ForeignKey("CreationUserId")]
    [InverseProperty("InverseCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("CreationUser")]
    public virtual ICollection<GlobalCategory> GlobalCategoryCreationUser { get; set; } = new List<GlobalCategory>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<GlobalCategory> GlobalCategoryUpdateUser { get; set; } = new List<GlobalCategory>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<User> InverseCreationUser { get; set; } = new List<User>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<User> InverseUpdateUser { get; set; } = new List<User>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Media> MediaCreationUser { get; set; } = new List<Media>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Media> MediaUpdateUser { get; set; } = new List<Media>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Product> ProductCreationUser { get; set; } = new List<Product>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Product> ProductUpdateUser { get; set; } = new List<Product>();

    [ForeignKey("RoleId")]
    [InverseProperty("User")]
    public virtual Role Role { get; set; } = null!;

    [InverseProperty("CreationUser")]
    public virtual ICollection<Site> SiteCreationUser { get; set; } = new List<Site>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Site> SiteUpdateUser { get; set; } = new List<Site>();

    [ForeignKey("StatusId")]
    [InverseProperty("User")]
    public virtual UserStatus Status { get; set; } = null!;

    [InverseProperty("CreationUser")]
    public virtual ICollection<Supplier> SupplierCreationUser { get; set; } = new List<Supplier>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Supplier> SupplierUpdateUser { get; set; } = new List<Supplier>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Tag> TagCreationUser { get; set; } = new List<Tag>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Tag> TagUpdateUser { get; set; } = new List<Tag>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<TemplateAttribute> TemplateAttributeCreationUser { get; set; } = new List<TemplateAttribute>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<TemplateAttribute> TemplateAttributeUpdateUser { get; set; } = new List<TemplateAttribute>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<TemplateProduct> TemplateProductCreationUser { get; set; } = new List<TemplateProduct>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<TemplateProduct> TemplateProductUpdateUser { get; set; } = new List<TemplateProduct>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("InverseUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [InverseProperty("User")]
    public virtual UserPreference? UserPreference { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("User")]
    public virtual ICollection<Site> Site { get; set; } = new List<Site>();
}
