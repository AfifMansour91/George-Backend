using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("Name", Name = "UQ__AccountS__737584F61AD40735", IsUnique = true)]
public partial class AccountStatus
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("StatusNavigation")]
    public virtual ICollection<Account> Account { get; set; } = new List<Account>();
}
