using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("SiteBusinessType")]
public partial class SiteBusinessType
{
    [Key]
    public long Id { get; set; }

    public long SiteId { get; set; }

    public int BusinessTypeId { get; set; }

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("SiteBusinessTypes")]
    public virtual BusinessType BusinessType { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("SiteBusinessTypes")]
    public virtual Site Site { get; set; } = null!;
}
