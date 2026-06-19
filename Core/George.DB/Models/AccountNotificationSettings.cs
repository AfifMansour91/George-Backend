using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("AccountId", Name = "UQ_AccountNotificationSettings_AccountId", IsUnique = true)]
public partial class AccountNotificationSettings
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    [Column("NewOrder_ManagerSoundEnabled")]
    public bool NewOrderManagerSoundEnabled { get; set; }

    [Column("NewOrder_ManagerSoundKey")]
    [StringLength(20)]
    public string? NewOrderManagerSoundKey { get; set; }

    [Column("NewOrder_ManagerSoundTriggerWebsite")]
    public bool NewOrderManagerSoundTriggerWebsite { get; set; }

    [Column("NewOrder_ManagerSoundTriggerKiosk")]
    public bool NewOrderManagerSoundTriggerKiosk { get; set; }

    [Column("NewOrder_ManagerSoundTriggerWhatsapp")]
    public bool NewOrderManagerSoundTriggerWhatsapp { get; set; }

    [Column("NewOrder_ManagerSoundTriggerPhone")]
    public bool NewOrderManagerSoundTriggerPhone { get; set; }

    [Column("NewOrder_ManagerMessageChannel")]
    [StringLength(20)]
    public string? NewOrderManagerMessageChannel { get; set; }

    [Column("NewOrder_ManagerPhoneNumbers")]
    [StringLength(500)]
    public string? NewOrderManagerPhoneNumbers { get; set; }

    [Column("NewOrder_ManagerMessageTemplate")]
    public string? NewOrderManagerMessageTemplate { get; set; }

    [Column("NewOrder_ManagerReminderBeforeDeliveryEnabled")]
    public bool NewOrderManagerReminderBeforeDeliveryEnabled { get; set; }

    [Column("NewOrder_ManagerReminderBeforeDeliveryMinutes")]
    public int NewOrderManagerReminderBeforeDeliveryMinutes { get; set; }

    [Column("NewOrder_ManagerReminderNoTreatmentEnabled")]
    public bool NewOrderManagerReminderNoTreatmentEnabled { get; set; }

    [Column("NewOrder_ManagerReminderNoTreatmentMinutes")]
    public int NewOrderManagerReminderNoTreatmentMinutes { get; set; }

    [Column("NewOrder_ManagerReminderNoTreatmentSoundKey")]
    [StringLength(20)]
    public string? NewOrderManagerReminderNoTreatmentSoundKey { get; set; }

    [Column("NewOrder_CustomerChannel")]
    [StringLength(20)]
    public string? NewOrderCustomerChannel { get; set; }

    [Column("NewOrder_CustomerMessageShipping")]
    public string? NewOrderCustomerMessageShipping { get; set; }

    [Column("NewOrder_CustomerMessagePickup")]
    public string? NewOrderCustomerMessagePickup { get; set; }

    [Column("NewOrder_CustomerMessageKiosk")]
    public string? NewOrderCustomerMessageKiosk { get; set; }

    [Column("OrderReady_ManagerNotifyEnabled")]
    public bool OrderReadyManagerNotifyEnabled { get; set; }

    [Column("OrderReady_CustomerChannel")]
    [StringLength(20)]
    public string? OrderReadyCustomerChannel { get; set; }

    [Column("OrderReady_CustomerMessageShipping")]
    public string? OrderReadyCustomerMessageShipping { get; set; }

    [Column("OrderReady_CustomerMessagePickup")]
    public string? OrderReadyCustomerMessagePickup { get; set; }

    [Column("OrderReady_CustomerMessageKiosk")]
    public string? OrderReadyCustomerMessageKiosk { get; set; }

    [Column("OrderNotPickedUp_ManagerNotifyEnabled")]
    public bool OrderNotPickedUpManagerNotifyEnabled { get; set; }

    [Column("OrderNotPickedUp_AutoReminderEnabled")]
    public bool OrderNotPickedUpAutoReminderEnabled { get; set; }

    [Column("OrderNotPickedUp_MinutesAfterScheduledPickup")]
    public int OrderNotPickedUpMinutesAfterScheduledPickup { get; set; }

    [Column("OrderNotPickedUp_CustomerMessageTemplate")]
    public string? OrderNotPickedUpCustomerMessageTemplate { get; set; }

    [Column("AfterDelivery_Enabled")]
    public bool AfterDeliveryEnabled { get; set; }

    [Column("AfterDelivery_TriggerType")]
    [StringLength(20)]
    public string? AfterDeliveryTriggerType { get; set; }

    [Column("AfterDelivery_TriggerAfterValue")]
    public int AfterDeliveryTriggerAfterValue { get; set; }

    [Column("AfterDelivery_TriggerAfterUnit")]
    [StringLength(20)]
    public string? AfterDeliveryTriggerAfterUnit { get; set; }

    [Column("AfterDelivery_CustomerMessageTemplate")]
    public string? AfterDeliveryCustomerMessageTemplate { get; set; }

    [Column("NewOrder_CustomerSmsOnPhoneOrderEnabled")]
    public bool NewOrderCustomerSmsOnPhoneOrderEnabled { get; set; }

    [Column("NewOrder_CustomerMessagePhoneOrder")]
    public string? NewOrderCustomerMessagePhoneOrder { get; set; }

    [Column("Payment_CustomerMessageInvoice")]
    public string? PaymentCustomerMessageInvoice { get; set; }

    [Column("Payment_CustomerMessageRefund")]
    public string? PaymentCustomerMessageRefund { get; set; }

    [Column("Payment_CustomerMessagePaymentLink")]
    public string? PaymentCustomerMessagePaymentLink { get; set; }

    /// <summary>When true, send invoice link SMS automatically after successful payment capture.</summary>
    [Column("Payment_SendInvoiceSmsAfterCapture")]
    public bool PaymentSendInvoiceSmsAfterCapture { get; set; } = true;

    [ForeignKey("AccountId")]
    [InverseProperty("AccountNotificationSettings")]
    public virtual Account Account { get; set; } = null!;
}
