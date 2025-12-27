using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountProductSite")]
public partial class AccountProductSite
{
    [Key]
    public long Id { get; set; }

    public long AccountProductId { get; set; }

    public long SiteId { get; set; }

    public bool IsEnabled { get; set; }

    [ForeignKey("AccountProductId")]
    [InverseProperty("AccountProductSites")]
    public virtual AccountProduct AccountProduct { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("AccountProductSites")]
    public virtual Site Site { get; set; } = null!;
}
