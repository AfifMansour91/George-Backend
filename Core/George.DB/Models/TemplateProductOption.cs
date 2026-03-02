using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class TemplateProductOption
{
    [Key]
    public int Id { get; set; }

    public int TemplateProductId { get; set; }

    [StringLength(100)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProductOption")]
    public virtual TemplateProduct TemplateProduct { get; set; } = null!;

    [InverseProperty("TemplateProductOption")]
    public virtual ICollection<TemplateProductOptionValue> TemplateProductOptionValue { get; set; } = new List<TemplateProductOptionValue>();
}
