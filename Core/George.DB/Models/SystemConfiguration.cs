using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Keyless]
[Table("SystemConfiguration")]
public partial class SystemConfiguration
{
    [StringLength(100)]
    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public string? Description { get; set; }
}
