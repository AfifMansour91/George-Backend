using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("WizardStatus")]
[Index("Name", Name = "UQ__WizardSt__737584F6288990B3", IsUnique = true)]
public partial class WizardStatus
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("WizardStatus")]
    public virtual ICollection<Account> Accounts { get; set; } = new List<Account>();
}
