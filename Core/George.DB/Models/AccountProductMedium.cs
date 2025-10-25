using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class AccountProductMedium
{
    [Key]
    public long Id { get; set; }

    public long AccountProductId { get; set; }

    [StringLength(1000)]
    public string Url { get; set; } = null!;

    [StringLength(300)]
    public string? AltText { get; set; }

    public int SortOrder { get; set; }

    public bool IsPrimary { get; set; }

    [ForeignKey("AccountProductId")]
    [InverseProperty("AccountProductMedia")]
    public virtual AccountProduct AccountProduct { get; set; } = null!;
}
