using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountUser")]
public partial class AccountUser
{
    [Key]
    public long Id { get; set; }

    public long AccountId { get; set; }

    public int UserId { get; set; }

    public int RoleId { get; set; }

    public bool IsActive { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountUsers")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("RoleId")]
    [InverseProperty("AccountUsers")]
    public virtual Role Role { get; set; } = null!;

    [ForeignKey("UserId")]
    [InverseProperty("AccountUsers")]
    public virtual User User { get; set; } = null!;
}
