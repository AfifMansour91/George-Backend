using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Identity.Client;
using System.Text.Json;
using Attribute = George.DB.Attribute;
using SysRegex = System.Text.RegularExpressions;

namespace George.Services
{
    public class AutoMapperProfile : Profile
    {
        //**************************    Construction    **************************//
        public AutoMapperProfile()
        {
            //*************************    Common    *************************//
            CreateMap<Enum, string>().ConvertUsing(e => GetEnumValueDescription(e));
            CreateMap<string, int>().ConvertUsing(s => s.HasValue() && SysRegex.Regex.Match(s, "^[0-9]*$").Success ? Convert.ToInt32(s) : 0);
            CreateMap<string, int?>().ConvertUsing(s => s.HasValue() && SysRegex.Regex.Match(s, "^[0-9]*$").Success ? (int?)Convert.ToInt32(s) : null);
            CreateMap<string, decimal?>().ConvertUsing(s => s.HasValue() && SysRegex.Regex.Match(s, "^[0-9]*$").Success ? (decimal?)Convert.ToDecimal(s) : null);
            CreateMap<string, decimal>().ConvertUsing(s => s.HasValue() && SysRegex.Regex.Match(s, "^[0-9]*$").Success ? (decimal)Convert.ToDecimal(s) : 0);



            //*************************    DB    *************************//

            ////////////////////////// Account
            CreateMap<Account, AccountRes>()
                .ForMember(dest => dest.NotificationSettings, opt => opt.Ignore())
                .AfterMap((src, dest, context) =>
                {
                    dest.AccountName = src.Name;

                    // If you don’t have these columns yet, keep null/empty
                    dest.AccountDescription = src.Description;
                    dest.AccountAddress = src.Address;
                    dest.AccountCity = src.City;
                    dest.AccountState = src.State;
                    dest.AccountZip = src.Zip;
                    dest.AccountPhone = src.Phone;

                    dest.ManagerName = src.ManagerName;
                    dest.ManagerEmail = src?.ManagerEmail;

                    dest.Status = src.IsActive ? "Active" : "Inactive";

                    dest.WizardStatusNamePair = src.WizardStatus != null
                        ? new IdNamePair
                        {
                            Id = src.WizardStatus.Id,
                            Name = src.WizardStatus.Name
                        }
                        : null;
                    dest.WizardStatus = src.WizardStatus != null
                        ? src.WizardStatus.Name
                        : "Not Started";

                    dest.WizardTypeIdNamePair = src.WizardType != null
                        ? new IdNamePair
                        {
                            Id = src.WizardType.Id,
                            Name = src.WizardType.Name
                        }
                        : null;
                    dest.WizardType = src.WizardType != null && !string.IsNullOrEmpty(src.WizardType.Name)
                        ? src.WizardType.Name
                        : "all_sites";
                    dest.WizardStep = src?.WizardStep ?? 0;

                    dest.ContentOwner = src?.ContentOwner?.Name ?? "Company";

                    dest.CreatedDate = SpecifyUtc(src.CreationTime);
                    dest.UpdatedDate = src.UpdatedDate.HasValue ? SpecifyUtc(src.UpdatedDate.Value) : null;

                    dest.CreatedById = null;
                    dest.CreatedBy = null;
                    
                    dest.LogoUrl = src.LogoUrl;
                    dest.Website = src.Website;
                    dest.IsKosherShop = src.IsKosherShop;
                    dest.AllowWeighted = src.AllowWeighted;
                    dest.KioskEnabled = src.KioskEnabled;
                    if (src.KioskSettings != null)
                    {
                        dest.KioskSettings = context.Mapper.Map<KioskSettingsRes>(src.KioskSettings);
                        dest.KioskSettings.HomeVideoMediaId = src.KioskSettings.HomeVideoMediaId;
                        dest.KioskSettings.HomeVideoUrl = src.KioskSettings.HomeVideoMedia?.Url;
                        dest.KioskSettings.PosProductsType = src.KioskSettings.PosProductsType;
                        dest.KioskSettings.PosProductsCategoryId = src.KioskSettings.PosProductsCategoryId;
                        var orderedImages = src.KioskSettingsHomeImage?.OrderBy(i => i.SortOrder).ToList() ?? new List<George.DB.KioskSettingsHomeImage>();
                        dest.KioskSettings.HomeImageMediaIds = orderedImages.Select(x => x.MediaId).ToList();
                        dest.KioskSettings.HomeImageUrls = orderedImages.Select(x => x.Media?.Url).Where(u => !string.IsNullOrEmpty(u)).Cast<string>().ToList();
                    }
                    else
                    {
                        dest.KioskSettings = null;
                    }
                    dest.NotificationSettings = src.AccountNotificationSettings != null
                        ? MapNotificationSettingsToRes(src.AccountNotificationSettings)
                        : null;
                    dest.DefaultLowStockThresholdWeighted = src.DefaultLowStockThresholdWeighted;
                    dest.DefaultLowStockThresholdUnits = src.DefaultLowStockThresholdUnits;
                    dest.DefaultNewLabelDays = src.DefaultNewLabelDays > 0 ? src.DefaultNewLabelDays : 7;
                });
            CreateMap<KioskSettings, KioskSettingsRes>()
                .ForMember(dest => dest.HomeVideoUrl, opt => opt.Ignore())
                .ForMember(dest => dest.HomeImageMediaIds, opt => opt.Ignore())
                .ForMember(dest => dest.HomeImageUrls, opt => opt.Ignore());
            CreateMap<CreateAccountReq, Account>()
                .AfterMap((src, dest, context) =>
                {
                    dest.LogoUrl = src.LogoUrl;
                });
            CreateMap<UpdateAccountReq, Account>()
                .AfterMap((src, dest, context) =>
                {
                    // LogoUrl is handled explicitly in AccountService.UpdateAccountAsync
                    // to preserve existing value if not provided in request
                });

            /////////////////////////// Site
            CreateMap<Site, SiteRes>()
                .ForMember(dest => dest.KioskSettings, opt => opt.Ignore())
                .AfterMap((src, dest, context) =>
                {
                    dest.Id = src.Id;
                    // Map business types
                    if (src.BusinessType != null && src.BusinessType.Any())
                    {
                        dest.BusinessTypeIds = src.BusinessType.Select(bt => bt.Id).ToList();
                    }
                    else
                    {
                        dest.BusinessTypeIds = new List<int>();
                    }
                    
                    // Ensure status defaults to "active" if null
                    if (string.IsNullOrWhiteSpace(dest.Status))
                    {
                        dest.Status = src.IsActive ? "active" : "inactive";
                    }
                    
                    // Explicitly map WooCommerceEnabled to ensure it's included
                    dest.WooCommerceEnabled = src.WooCommerceEnabled;

                    // When account has kiosk enabled, include account kiosk settings (so GET Site/{id} returns showOutOfStockProducts, showOutOfStockAtBottom, etc.)
                    if (src.Account != null && src.Account.KioskEnabled && src.Account.KioskSettings != null)
                    {
                        dest.KioskSettings = context.Mapper.Map<KioskSettingsRes>(src.Account.KioskSettings);
                        dest.KioskSettings.HomeVideoMediaId = src.Account.KioskSettings.HomeVideoMediaId;
                        dest.KioskSettings.HomeVideoUrl = src.Account.KioskSettings.HomeVideoMedia?.Url;
                        dest.KioskSettings.PosProductsType = src.Account.KioskSettings.PosProductsType;
                        dest.KioskSettings.PosProductsCategoryId = src.Account.KioskSettings.PosProductsCategoryId;
                        var orderedImages = src.Account.KioskSettingsHomeImage?.OrderBy(i => i.SortOrder).ToList() ?? new List<George.DB.KioskSettingsHomeImage>();
                        dest.KioskSettings.HomeImageMediaIds = orderedImages.Select(x => x.MediaId).ToList();
                        dest.KioskSettings.HomeImageUrls = orderedImages.Select(x => x.Media?.Url).Where(u => !string.IsNullOrEmpty(u)).Cast<string>().ToList();
                    }
                    else
                    {
                        dest.KioskSettings = null;
                    }
                });
            CreateMap<CreateSiteReq, Site>()
                .AfterMap((src, dest, context) =>
                {
                    // Business types will be handled separately in storage
                });
            CreateMap<UpdateSiteReq, Site>()
                .AfterMap((src, dest, context) =>
                {
                    // Business types will be handled separately in storage
                });

            ////////////////////////// Order (Sprint 2)
            // Creation/updated timestamps: UTC + "Z" for real instants. Delivery/pickup are calendar dates — do not SpecifyUtc or a noon UTC instant becomes the wrong calendar day in the UI (e.g. .slice(0,10) → "yesterday").
            CreateMap<Order, OrderRes>()
                .ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.OrderItem ?? new List<OrderItem>()))
                .ForMember(dest => dest.BagsCount, opt => opt.MapFrom(src => src.BagsCount))
                .AfterMap((src, dest, context) =>
                {
                    dest.CreationTime = SpecifyUtc(src.CreationTime);
                    dest.UpdatedDate = src.UpdatedDate.HasValue ? SpecifyUtc(src.UpdatedDate.Value) : null;
                    dest.DeliveryDate = ToUnspecifiedCalendarDate(src.DeliveryDate);
                    dest.PickupDate = ToUnspecifiedCalendarDate(src.PickupDate);
                    dest.AccountName = src.Account?.Name;
                });
            CreateMap<OrderItem, OrderItemRes>();
            CreateMap<CreateOrderReq, Order>()
                .ForMember(d => d.PickupDate, o => o.MapFrom(s => s.PickupDate.HasValue ? s.PickupDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null))
                .ForMember(d => d.DeliveryDate, o => o.MapFrom(s => s.DeliveryDate.HasValue ? s.DeliveryDate.Value.ToDateTime(TimeOnly.MinValue) : (DateTime?)null));
            CreateMap<CreateOrderItemReq, OrderItem>();
            CreateMap<UpdateOrderReq, Order>()
                .ForAllMembers(opt => opt.Condition((src, dest, srcMember) => srcMember != null));

            ////////////////////////// BusinessType
            CreateMap<BusinessType, BusinessTypeRes>()
                .AfterMap((src, dest, context) =>
                {
                });
            CreateMap<CreateBusinessTypeReq, BusinessType>()
                .AfterMap((src, dest, context) =>
                {
                });
            CreateMap<UpdateBusinessTypeReq, BusinessType>()
                .AfterMap((src, dest, context) =>
                {
                });

            ////////////////////////// Attribute
            CreateMap<Attribute, AttributeRes>()
                .AfterMap((src, dest, context) =>
                {
                    // Values will be mapped in the service layer
                });
            CreateMap<CreateAttributeReq, Attribute>()
                .AfterMap((src, dest, context) =>
                {
                    // Values will be handled separately in storage
                });
            CreateMap<UpdateAttributeReq, Attribute>()
                .AfterMap((src, dest, context) =>
                {
                    // Values will be handled separately in storage
                });

            ////////////////////////// Category
            CreateMap<Category, CategoryRes>()
                .AfterMap((src, dest, context) =>
                {
                    // Sites will be mapped in the service layer
                });
            CreateMap<CreateCategoryReq, Category>()
                .AfterMap((src, dest, context) =>
                {
                    // Sites will be handled separately in storage
                });
            CreateMap<UpdateCategoryReq, Category>()
                .AfterMap((src, dest, context) =>
                {
                    // Sites will be handled separately in storage
                });

            ////////////////////////// Brand
            // BrandService manually maps Brand -> BrandRes (needs SiteIds + ProductCount post-process),
            // so only req -> entity mappings are registered here.
            CreateMap<CreateBrandReq, Brand>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.CreationTime, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedDate, opt => opt.Ignore())
                .ForMember(dest => dest.CreationUserId, opt => opt.Ignore())
                .ForMember(dest => dest.UpdateUserId, opt => opt.Ignore())
                .ForMember(dest => dest.Site, opt => opt.Ignore())
                .ForMember(dest => dest.ProductBrand, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.TemplateProduct, opt => opt.Ignore())
                .ForMember(dest => dest.InverseParentBrand, opt => opt.Ignore())
                .ForMember(dest => dest.ParentBrand, opt => opt.Ignore())
                .ForMember(dest => dest.SourceGlobalBrand, opt => opt.Ignore())
                .ForMember(dest => dest.Account, opt => opt.Ignore())
                .ForMember(dest => dest.CreationUser, opt => opt.Ignore())
                .ForMember(dest => dest.UpdateUser, opt => opt.Ignore());

            CreateMap<UpdateBrandReq, Brand>()
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.CreationTime, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedDate, opt => opt.Ignore())
                .ForMember(dest => dest.CreationUserId, opt => opt.Ignore())
                .ForMember(dest => dest.UpdateUserId, opt => opt.Ignore())
                .ForMember(dest => dest.Site, opt => opt.Ignore())
                .ForMember(dest => dest.ProductBrand, opt => opt.Ignore())
                .ForMember(dest => dest.Product, opt => opt.Ignore())
                .ForMember(dest => dest.TemplateProduct, opt => opt.Ignore())
                .ForMember(dest => dest.InverseParentBrand, opt => opt.Ignore())
                .ForMember(dest => dest.ParentBrand, opt => opt.Ignore())
                .ForMember(dest => dest.SourceGlobalBrand, opt => opt.Ignore())
                .ForMember(dest => dest.Account, opt => opt.Ignore())
                .ForMember(dest => dest.CreationUser, opt => opt.Ignore())
                .ForMember(dest => dest.UpdateUser, opt => opt.Ignore());


            ////////////////////////// Profile
            CreateMap<User, ProfileRes>()
                //.ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.AvatarUrl .HasValue() ? FileHelper.GetFileExternalPath(src.AvatarUrl) : default))
                .AfterMap((src, dest, context) =>
                {

                });
            CreateMap<ProfileReq, User>()
                .AfterMap((src, dest, context) =>
                {
                });


            ////////////////////////// Upload
            CreateMap<FileManagerRes, UploadRes>()
                .ForMember(dest => dest.FileKey, opt => opt.MapFrom(src => src.FilePath.HasValue() ? FileHelper.EncryptFileKey(src.OriginalFileName, src.FilePath) : default))
                .ForMember(dest => dest.FileUrl, opt => opt.MapFrom(src => src.FilePath.HasValue() ? FileHelper.GetFileExternalPath(src.FilePath) : default));


			////////////////////////// User
			CreateMap<User, InnerUserRes>();
			CreateMap<User, UserRes>()
				.ForMember(dest => dest.SiteIds, opt => opt.MapFrom(src => src.Site != null ? src.Site.Select(s => s.Id).ToList() : new List<int>()));

            ////////////////////////// Promotion
            CreateMap<Promotion, PromotionRes>()
                .AfterMap((src, dest, _) =>
                {
                    dest.CreationTime = SpecifyUtc(src.CreationTime);
                    dest.UpdatedDate = src.UpdatedDate.HasValue ? SpecifyUtc(src.UpdatedDate.Value) : null;
                    if (src.ScheduleStartDateUtc.HasValue)
                        dest.ScheduleStartDateUtc = SpecifyUtc(src.ScheduleStartDateUtc.Value);
                    if (src.ScheduleEndDateUtc.HasValue)
                        dest.ScheduleEndDateUtc = SpecifyUtc(src.ScheduleEndDateUtc.Value);
                });
            CreateMap<CreatePromotionReq, Promotion>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.GuidId, opt => opt.Ignore())
                .ForMember(dest => dest.CreationTime, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedDate, opt => opt.Ignore())
                .ForMember(dest => dest.CreationUserId, opt => opt.Ignore())
                .ForMember(dest => dest.UpdateUserId, opt => opt.Ignore())
                .ForMember(dest => dest.IsDeleted, opt => opt.Ignore())
                .ForMember(dest => dest.Site, opt => opt.Ignore());

        }



        //*************************    Private Methods    *************************//
        private static string GetEnumValueDescription(Enum value)
        {
            return value.GetDescription();
        }

        /// <summary>Ensure DateTime is marked as UTC so JSON serialization emits "Z" and clients display correct local time. Use for any response DTO DateTime (e.g. Order, Account, Customer).</summary>
        private static DateTime SpecifyUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc) return value;
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static DateTime? ToUnspecifiedCalendarDate(DateTime? value) =>
            value.HasValue ? DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Unspecified) : null;


        private JsonDocument? ParseJson(string settings)
        {
            return JsonDocument.Parse(settings);
        }

        private void MapUserToInnerUserRes<T>(User user, T dest, ResolutionContext context) where T : InnerUserRes
        {
            if (user != null)
            {
                dest.FirstName = user.FirstName;
                dest.LastName = user.LastName;
                //dest.IdentificationNumber = user.IdentificationNumber;
                //dest.Email = user.Email;
                //dest.Phone = user.Phone;
                //dest.LanguageId = user.LanguageId;

            }
        }

        private static NotificationSettingsRes MapNotificationSettingsToRes(George.DB.AccountNotificationSettings e)
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
                }
            };
        }
    }
}
