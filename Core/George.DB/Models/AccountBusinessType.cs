using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountBusinessType")]
[Index("AccountId", Name = "IX_AccountBusinessType_AccountId")]
public partial class AccountBusinessType
{
    [Key]
    public long Id { get; set; }

    public long AccountId { get; set; }

    public int BusinessTypeId { get; set; }

    public bool IsSelected { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountBusinessTypes")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("AccountBusinessTypes")]
    public virtual BusinessType BusinessType { get; set; } = null!;
}
