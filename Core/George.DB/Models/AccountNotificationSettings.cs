using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>Sprint 2: Notification settings (התראות) per account. One row per account, no JSON.</summary>
[Table("AccountNotificationSettings")]
public partial class AccountNotificationSettings
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [ForeignKey(nameof(Account))]
    public int AccountId { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    // ----- New order -----
    public bool NewOrder_ManagerSoundEnabled { get; set; } = true;
    [StringLength(20)]
    public string? NewOrder_ManagerSoundKey { get; set; }
    public bool NewOrder_ManagerSoundTriggerWebsite { get; set; } = true;
    public bool NewOrder_ManagerSoundTriggerKiosk { get; set; } = true;
    public bool NewOrder_ManagerSoundTriggerWhatsapp { get; set; }
    public bool NewOrder_ManagerSoundTriggerPhone { get; set; }
    [StringLength(20)]
    public string? NewOrder_ManagerMessageChannel { get; set; }
    [StringLength(500)]
    public string? NewOrder_ManagerPhoneNumbers { get; set; }
    public string? NewOrder_ManagerMessageTemplate { get; set; }
    public bool NewOrder_ManagerReminderBeforeDeliveryEnabled { get; set; }
    public int NewOrder_ManagerReminderBeforeDeliveryMinutes { get; set; } = 60;
    public bool NewOrder_ManagerReminderNoTreatmentEnabled { get; set; }
    public int NewOrder_ManagerReminderNoTreatmentMinutes { get; set; } = 15;
    [StringLength(20)]
    public string? NewOrder_ManagerReminderNoTreatmentSoundKey { get; set; }
    [StringLength(20)]
    public string? NewOrder_CustomerChannel { get; set; }
    public string? NewOrder_CustomerMessageShipping { get; set; }
    public string? NewOrder_CustomerMessagePickup { get; set; }
    public string? NewOrder_CustomerMessageKiosk { get; set; }

    // ----- Order ready -----
    public bool OrderReady_ManagerNotifyEnabled { get; set; }
    [StringLength(20)]
    public string? OrderReady_CustomerChannel { get; set; }
    public string? OrderReady_CustomerMessageShipping { get; set; }
    public string? OrderReady_CustomerMessagePickup { get; set; }
    public string? OrderReady_CustomerMessageKiosk { get; set; }

    // ----- Order not picked up -----
    public bool OrderNotPickedUp_ManagerNotifyEnabled { get; set; }
    public bool OrderNotPickedUp_AutoReminderEnabled { get; set; }
    public int OrderNotPickedUp_MinutesAfterScheduledPickup { get; set; } = 30;
    public string? OrderNotPickedUp_CustomerMessageTemplate { get; set; }

    // ----- After delivery -----
    public bool AfterDelivery_Enabled { get; set; }
    [StringLength(20)]
    public string? AfterDelivery_TriggerType { get; set; }
    public int AfterDelivery_TriggerAfterValue { get; set; } = 1;
    [StringLength(20)]
    public string? AfterDelivery_TriggerAfterUnit { get; set; }
    public string? AfterDelivery_CustomerMessageTemplate { get; set; }

    public virtual Account Account { get; set; } = null!;
}
