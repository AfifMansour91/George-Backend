using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("BusinessType")]
public partial class BusinessType
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = null!;

    [StringLength(100)]
    public string Name { get; set; } = null!;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(100)]
    public string? Icon { get; set; }

    public bool? IsDeleted { get; set; }

    public bool? IsActive { get; set; }

    [InverseProperty("BusinessType")]
    public virtual ICollection<AccountBusinessType> AccountBusinessTypes { get; set; } = new List<AccountBusinessType>();

    [InverseProperty("BusinessType")]
    public virtual ICollection<CategoryBusinessType> CategoryBusinessTypes { get; set; } = new List<CategoryBusinessType>();

    [InverseProperty("BusinessType")]
    public virtual ICollection<Medium> Media { get; set; } = new List<Medium>();

    [InverseProperty("BusinessType")]
    public virtual ICollection<ProductTemplateBusinessType> ProductTemplateBusinessTypes { get; set; } = new List<ProductTemplateBusinessType>();

    [InverseProperty("BusinessType")]
    public virtual ICollection<SiteBusinessType> SiteBusinessTypes { get; set; } = new List<SiteBusinessType>();
}
