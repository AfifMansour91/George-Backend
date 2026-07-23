using George.Services.Response;

namespace George.Services;

/// <summary>Entity → response mapping for notification settings, shared by AutoMapperProfile and AccountService.</summary>
public static class NotificationSettingsMapper
{
    public static NotificationSettingsRes ToRes(George.DB.AccountNotificationSettings e)
    {
        return new NotificationSettingsRes
        {
            NewOrder = new NewOrderSettingsRes
            {
                ManagerSoundEnabled = e.NewOrderManagerSoundEnabled,
                ManagerSoundKey = e.NewOrderManagerSoundKey,
                ManagerSoundTriggerSources = new NewOrderSoundTriggerSourcesRes
                {
                    Website = e.NewOrderManagerSoundTriggerWebsite,
                    Kiosk = e.NewOrderManagerSoundTriggerKiosk,
                    Whatsapp = e.NewOrderManagerSoundTriggerWhatsapp,
                    Phone = e.NewOrderManagerSoundTriggerPhone
                },
                ManagerMessageChannel = e.NewOrderManagerMessageChannel,
                ManagerPhoneNumbers = e.NewOrderManagerPhoneNumbers,
                ManagerMessageTemplate = e.NewOrderManagerMessageTemplate,
                ManagerReminderBeforeDeliveryEnabled = e.NewOrderManagerReminderBeforeDeliveryEnabled,
                ManagerReminderBeforeDeliveryMinutes = e.NewOrderManagerReminderBeforeDeliveryMinutes,
                ManagerReminderNoTreatmentEnabled = e.NewOrderManagerReminderNoTreatmentEnabled,
                ManagerReminderNoTreatmentMinutes = e.NewOrderManagerReminderNoTreatmentMinutes,
                ManagerReminderNoTreatmentSoundKey = e.NewOrderManagerReminderNoTreatmentSoundKey,
                CustomerChannel = e.NewOrderCustomerChannel,
                CustomerMessageShipping = e.NewOrderCustomerMessageShipping,
                CustomerMessagePickup = e.NewOrderCustomerMessagePickup,
                CustomerMessageKiosk = e.NewOrderCustomerMessageKiosk,
                CustomerSmsOnPhoneOrderEnabled = e.NewOrderCustomerSmsOnPhoneOrderEnabled,
                CustomerMessagePhoneOrder = e.NewOrderCustomerMessagePhoneOrder
            },
            OrderReady = new OrderReadySettingsRes
            {
                ManagerNotifyEnabled = e.OrderReadyManagerNotifyEnabled,
                CustomerChannel = e.OrderReadyCustomerChannel,
                CustomerMessageShipping = e.OrderReadyCustomerMessageShipping,
                CustomerMessagePickup = e.OrderReadyCustomerMessagePickup,
                CustomerMessageKiosk = e.OrderReadyCustomerMessageKiosk
            },
            OrderNotPickedUp = new OrderNotPickedUpSettingsRes
            {
                ManagerNotifyEnabled = e.OrderNotPickedUpManagerNotifyEnabled,
                AutoReminderEnabled = e.OrderNotPickedUpAutoReminderEnabled,
                MinutesAfterScheduledPickup = e.OrderNotPickedUpMinutesAfterScheduledPickup,
                CustomerMessageTemplate = e.OrderNotPickedUpCustomerMessageTemplate
            },
            AfterDelivery = new AfterDeliverySettingsRes
            {
                Enabled = e.AfterDeliveryEnabled,
                TriggerType = e.AfterDeliveryTriggerType,
                TriggerAfterValue = e.AfterDeliveryTriggerAfterValue,
                TriggerAfterUnit = e.AfterDeliveryTriggerAfterUnit,
                CustomerMessageTemplate = e.AfterDeliveryCustomerMessageTemplate
            },
            Payments = new PaymentNotificationSettingsRes
            {
                CustomerMessageInvoice = e.PaymentCustomerMessageInvoice,
                CustomerMessageRefund = e.PaymentCustomerMessageRefund,
                CustomerMessagePaymentLink = e.PaymentCustomerMessagePaymentLink,
                SendInvoiceSmsAfterCapture = e.PaymentSendInvoiceSmsAfterCapture,
            }
        };
    }
}
