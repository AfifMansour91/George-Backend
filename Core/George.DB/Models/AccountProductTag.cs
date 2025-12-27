using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountProductTag")]
[Index("AccountProductId", "TagId", Name = "IX_AccountProductTag_Unique", IsUnique = true)]
public partial class AccountProductTag
{
    [Key]
    public long Id { get; set; }

    public long AccountProductId { get; set; }

    public long TagId { get; set; }

    [ForeignKey("AccountProductId")]
    [InverseProperty("AccountProductTags")]
    public virtual AccountProduct AccountProduct { get; set; } = null!;

    [ForeignKey("TagId")]
    [InverseProperty("AccountProductTags")]
    public virtual Tag Tag { get; set; } = null!;
}
