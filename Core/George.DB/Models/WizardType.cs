using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("WizardType")]
[Index("Name", Name = "UQ__WizardTy__737584F6340E88E4", IsUnique = true)]
public partial class WizardType
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("WizardType")]
    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();
}
