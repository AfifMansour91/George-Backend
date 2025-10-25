using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountEcomCredential")]
public partial class AccountEcomCredential
{
    [Key]
    public long Id { get; set; }

    public long AccountId { get; set; }

    public int EcomPlatformId { get; set; }

    [StringLength(300)]
    public string BaseUrl { get; set; } = null!;

    [StringLength(300)]
    public string ApiKey { get; set; } = null!;

    [StringLength(300)]
    public string ApiSecret { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountEcomCredentials")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("EcomPlatformId")]
    [InverseProperty("AccountEcomCredentials")]
    public virtual EcomPlatform EcomPlatform { get; set; } = null!;
}
