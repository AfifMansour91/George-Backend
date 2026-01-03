using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("UserStatus")]
[Index("Name", Name = "UQ__UserStat__737584F64A33F02E", IsUnique = true)]
public partial class UserStatus
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("Status")]
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
