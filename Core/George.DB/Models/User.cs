using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("User")]
[Index("StatusId", Name = "IX_FK_User_UserStatus_StatusId")]
public partial class User
{
    [Key]
    public int Id { get; set; }

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

    public bool IsMaster { get; set; }

    public bool IsDeleted { get; set; }
}
