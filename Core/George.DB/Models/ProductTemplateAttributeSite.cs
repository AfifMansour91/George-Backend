using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("ProductTemplateAttributeSite")]
public partial class ProductTemplateAttributeSite
{
    [Key]
    public long Id { get; set; }

    public long ProductTemplateAttributeId { get; set; }

    public long SiteId { get; set; }

    [ForeignKey("ProductTemplateAttributeId")]
    [InverseProperty("ProductTemplateAttributeSites")]
    public virtual ProductTemplateAttribute ProductTemplateAttribute { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("ProductTemplateAttributeSites")]
    public virtual Site Site { get; set; } = null!;
}
