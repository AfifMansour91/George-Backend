using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class BusinessTypeCategory
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    public int BusinessTypeId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("BusinessTypeCategory")]
    public virtual BusinessType BusinessType { get; set; } = null!;
}
