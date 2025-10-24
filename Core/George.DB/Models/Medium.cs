using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("Medium")]
public partial class Medium
{
    [Key]
    public int Id { get; set; }

    public int RegistryUnitId { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [StringLength(1000)]
    public string? Url { get; set; }

    [StringLength(1000)]
    public string? FileUrl { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [ForeignKey("RegistryUnitId")]
    [InverseProperty("Media")]
    public virtual RegistryUnit RegistryUnit { get; set; } = null!;
}
