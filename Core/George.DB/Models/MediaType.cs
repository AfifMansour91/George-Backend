using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("MediaType")]
[Index("Name", Name = "UQ__MediaTyp__737584F6DE362F8B", IsUnique = true)]
public partial class MediaType
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("Type")]
    public virtual ICollection<Medium> Media { get; set; } = new List<Medium>();
}
