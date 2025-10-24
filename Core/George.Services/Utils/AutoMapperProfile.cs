using System.Text.Json;
using AutoMapper;
using George.Common;
using George.Data;
using George.DB;


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

			
			////////////////////////// LandUse
			CreateMap<LandUse, IdNamePair>();

			////////////////////////// Medium
			CreateMap<Medium, MediumRes>()
				.ForMember(dest => dest.FileUrl, opt => opt.MapFrom(src => src.FileUrl.HasValue() ? FileHelper.GetFileExternalPath(src.FileUrl) : default));
			CreateMap<MediumReq, Medium>()
				//.ForMember(dest => dest.FileUrl, opt => opt.MapFrom(src => src.FileKey.HasValue() ? FileHelper.GetFilePathFromKey(src.FileKey) : null))
				;
			CreateMap<CreateMediumReq, Medium>()
				.IncludeBase<MediumReq, Medium>();
			CreateMap<UpdateMediumReq, Medium>()
				.IncludeBase<MediumReq, Medium>();
			

			////////////////////////// Profile
			CreateMap<User, ProfileRes>()
				//.ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.AvatarUrl .HasValue() ? FileHelper.GetFileExternalPath(src.AvatarUrl) : default))
				.AfterMap((src, dest, context) => {

				});
			CreateMap<ProfileReq, User>()
				.AfterMap((src, dest, context) => {
				});


			////////////////////////// RegistryUnit
			CreateMap<RegistryUnit, RegistryUnitRes>()
				.ForMember(dest => dest.SubParcels, opt => opt.Ignore())
				.AfterMap((src, dest, context) => {
					if(src.RegistrationMethodId == null)
						dest.RegistrationMethodId = RegistrationMethod.None;
					if(src.SubParcels.HasValue())
					{
						dest.SubParcels = new();
						src.SubParcels.ForEach(a => dest.SubParcels.Add(a.SubParcel1));
					}
				});
			CreateMap<RegistryUnitReq, RegistryUnit>()
				.AfterMap((src, dest, context) => {
					if(src.SubParcels.HasValue())
						src.SubParcels.ForEach(a => dest.SubParcels.Add(new() { SubParcel1 = a }));
				});
			CreateMap<CreateRegistryUnitReq, RegistryUnit>()
				.IncludeBase<RegistryUnitReq, RegistryUnit>()
				.ForMember(dest => dest.RegistrationMethodId, opt => opt.MapFrom(src => src.RegistrationMethodId == RegistrationMethod.None ? null : src.RegistrationMethodId));
			CreateMap<UpdateRegistryUnitReq, RegistryUnit>()
				.IncludeBase<RegistryUnitReq, RegistryUnit>()
				.ForMember(dest => dest.RegistrationMethodId, opt => opt.MapFrom(src => src.RegistrationMethodId == RegistrationMethod.None ? null : src.RegistrationMethodId));


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
