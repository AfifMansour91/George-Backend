using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountUser")]
[Index("AccountId", "SiteId", Name = "IX_AccountUser_Account_Site")]
public partial class AccountUser
{
    [Key]
    public long Id { get; set; }

    public long AccountId { get; set; }

    public int UserId { get; set; }

    public int RoleId { get; set; }

    public bool IsActive { get; set; }

    public long? SiteId { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountUsers")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("RoleId")]
    [InverseProperty("AccountUsers")]
    public virtual Role Role { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("AccountUsers")]
    public virtual Site? Site { get; set; }

    [ForeignKey("UserId")]
    [InverseProperty("AccountUsers")]
    public virtual User User { get; set; } = null!;
}
