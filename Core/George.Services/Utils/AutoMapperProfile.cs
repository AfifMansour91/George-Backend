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
                    dest.WizardStatus = src.Status == null
                        ? "Not Started"
                        : (src.Status == "Completed" ? "Completed" : "In Progress");

                    dest.WizardTypeIdNamePair = src.WizardType != null
                        ? new IdNamePair
                        {
                            Id = src.WizardType.Id,
                            Name = src.WizardType.Name
                        }
                        : null;
                    dest.WizardType = "all_sites"; // until you store it
                    dest.WizardStep = src?.WizardStep ?? 0;

                    dest.ContentOwner = src?.ContentOwner?.Name ?? "Company";

                    dest.CreatedDate = src.CreationTime;
                    dest.UpdatedDate = src.UpdatedDate;

                    dest.CreatedById = null;
                    dest.CreatedBy = null;

                });
            CreateMap<CreateAccountReq, Account>()
                .AfterMap((src, dest, context) =>
                {
                });
            CreateMap<UpdateAccountReq, Account>()
                .AfterMap((src, dest, context) =>
                {
                });

            /////////////////////////// Site
            CreateMap<Site, SiteRes>()
                .AfterMap((src, dest, context) =>
                {
                    dest.Id = src.Id;
                    // Map business types
                    if (src.BusinessTypes != null && src.BusinessTypes.Any())
                    {
                        dest.BusinessTypeIds = src.BusinessTypes.Select(bt => bt.Id).ToList();
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
            CreateMap<User, UserRes>();

        }



        //*************************    Private Methods    *************************//
        private static string GetEnumValueDescription(Enum value)
        {
            return value.GetDescription();
        }


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

    }
}
