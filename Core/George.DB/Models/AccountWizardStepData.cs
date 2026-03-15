using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("AccountId", "SiteId", "StepNumber", Name = "UQ_AccountWizardStepData_AccountId_SiteId_StepNumber", IsUnique = true)]
public partial class AccountWizardStepData
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int? SiteId { get; set; }

    public int StepNumber { get; set; }

    public string? DataJson { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountWizardStepData")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("AccountWizardStepData")]
    public virtual Site? Site { get; set; }
}
