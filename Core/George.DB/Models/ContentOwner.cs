using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("Name", Name = "UQ__ContentO__737584F6AA024CD0", IsUnique = true)]
public partial class ContentOwner
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("ContentOwner")]
    public virtual ICollection<Account> Account { get; set; } = new List<Account>();
}
