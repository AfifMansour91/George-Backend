using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("User")]
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
    [InverseProperty("Users")]
    public virtual Account? Account { get; set; }

    [InverseProperty("CreationUser")]
    public virtual ICollection<Account> AccountCreationUsers { get; set; } = new List<Account>();

    [InverseProperty("Manager")]
    public virtual ICollection<Account> AccountManagers { get; set; } = new List<Account>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Account> AccountUpdateUsers { get; set; } = new List<Account>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Attribute> AttributeCreationUsers { get; set; } = new List<Attribute>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Attribute> AttributeUpdateUsers { get; set; } = new List<Attribute>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Brand> BrandCreationUsers { get; set; } = new List<Brand>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Brand> BrandUpdateUsers { get; set; } = new List<Brand>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<BusinessType> BusinessTypeCreationUsers { get; set; } = new List<BusinessType>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<BusinessType> BusinessTypeUpdateUsers { get; set; } = new List<BusinessType>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Category> CategoryCreationUsers { get; set; } = new List<Category>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Category> CategoryUpdateUsers { get; set; } = new List<Category>();

    [ForeignKey("CreationUserId")]
    [InverseProperty("InverseCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("CreationUser")]
    public virtual ICollection<GlobalCategory> GlobalCategoryCreationUsers { get; set; } = new List<GlobalCategory>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<GlobalCategory> GlobalCategoryUpdateUsers { get; set; } = new List<GlobalCategory>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<User> InverseCreationUser { get; set; } = new List<User>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<User> InverseUpdateUser { get; set; } = new List<User>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Medium> MediumCreationUsers { get; set; } = new List<Medium>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Medium> MediumUpdateUsers { get; set; } = new List<Medium>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Product> ProductCreationUsers { get; set; } = new List<Product>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Product> ProductUpdateUsers { get; set; } = new List<Product>();

    [ForeignKey("RoleId")]
    [InverseProperty("Users")]
    public virtual Role Role { get; set; } = null!;

    [InverseProperty("CreationUser")]
    public virtual ICollection<Site> SiteCreationUsers { get; set; } = new List<Site>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Site> SiteUpdateUsers { get; set; } = new List<Site>();

    [ForeignKey("StatusId")]
    [InverseProperty("Users")]
    public virtual UserStatus Status { get; set; } = null!;

    [InverseProperty("CreationUser")]
    public virtual ICollection<Supplier> SupplierCreationUsers { get; set; } = new List<Supplier>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Supplier> SupplierUpdateUsers { get; set; } = new List<Supplier>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<Tag> TagCreationUsers { get; set; } = new List<Tag>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<Tag> TagUpdateUsers { get; set; } = new List<Tag>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<TemplateAttribute> TemplateAttributeCreationUsers { get; set; } = new List<TemplateAttribute>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<TemplateAttribute> TemplateAttributeUpdateUsers { get; set; } = new List<TemplateAttribute>();

    [InverseProperty("CreationUser")]
    public virtual ICollection<TemplateProduct> TemplateProductCreationUsers { get; set; } = new List<TemplateProduct>();

    [InverseProperty("UpdateUser")]
    public virtual ICollection<TemplateProduct> TemplateProductUpdateUsers { get; set; } = new List<TemplateProduct>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("InverseUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("Users")]
    public virtual ICollection<Site> Sites { get; set; } = new List<Site>();
}
