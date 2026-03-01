namespace George.Services.Response;

/// <summary>Sprint 2: Notification settings (התראות). Matches frontend NotificationSettings shape (camelCase in JSON).</summary>
public class NotificationSettingsRes
{
    public NewOrderSettingsRes? NewOrder { get; set; }
    public OrderReadySettingsRes? OrderReady { get; set; }
    public OrderNotPickedUpSettingsRes? OrderNotPickedUp { get; set; }
    public AfterDeliverySettingsRes? AfterDelivery { get; set; }
}

public class NewOrderSoundTriggerSourcesRes
{
    public bool? Website { get; set; }
    public bool? Kiosk { get; set; }
    public bool? Whatsapp { get; set; }
    public bool? Phone { get; set; }
}

public class NewOrderSettingsRes
{
    public bool? ManagerSoundEnabled { get; set; }
    public string? ManagerSoundKey { get; set; }
    public NewOrderSoundTriggerSourcesRes? ManagerSoundTriggerSources { get; set; }
    public string? ManagerMessageChannel { get; set; }
    public string? ManagerPhoneNumbers { get; set; }
    public string? ManagerMessageTemplate { get; set; }
    public bool? ManagerReminderBeforeDeliveryEnabled { get; set; }
    public int? ManagerReminderBeforeDeliveryMinutes { get; set; }
    public bool? ManagerReminderNoTreatmentEnabled { get; set; }
    public int? ManagerReminderNoTreatmentMinutes { get; set; }
    public string? ManagerReminderNoTreatmentSoundKey { get; set; }
    public string? CustomerChannel { get; set; }
    public string? CustomerMessageShipping { get; set; }
    public string? CustomerMessagePickup { get; set; }
    public string? CustomerMessageKiosk { get; set; }
    public bool? CustomerSmsOnPhoneOrderEnabled { get; set; }
    public string? CustomerMessagePhoneOrder { get; set; }
}

public class OrderReadySettingsRes
{
    public bool? ManagerNotifyEnabled { get; set; }
    public string? CustomerChannel { get; set; }
    public string? CustomerMessageShipping { get; set; }
    public string? CustomerMessagePickup { get; set; }
    public string? CustomerMessageKiosk { get; set; }
}

public class OrderNotPickedUpSettingsRes
{
    public bool? ManagerNotifyEnabled { get; set; }
    public bool? AutoReminderEnabled { get; set; }
    public int? MinutesAfterScheduledPickup { get; set; }
    public string? CustomerMessageTemplate { get; set; }
}

public class AfterDeliverySettingsRes
{
    public bool? Enabled { get; set; }
    public string? TriggerType { get; set; }
    public int? TriggerAfterValue { get; set; }
    public string? TriggerAfterUnit { get; set; }
    public string? CustomerMessageTemplate { get; set; }
}
