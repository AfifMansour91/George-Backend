using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("EcomPlatform")]
public partial class EcomPlatform
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Code { get; set; } = null!;

    [StringLength(100)]
    public string Name { get; set; } = null!;

    [InverseProperty("EcomPlatform")]
    public virtual ICollection<AccountEcomCredential> AccountEcomCredentials { get; set; } = new List<AccountEcomCredential>();
}
