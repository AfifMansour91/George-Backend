using System.ComponentModel;

namespace George.Common
{
	public enum SortField
	{
		None = 0,
		Id = 1,
		Name = 2,
		OwnershipType = 3,
		RegistrationMethod = 4,
		LandUse = 5,
		Block = 6,
		Parcel = 7,
		Address = 8,
		Area = 9, 
		IsFilled = 10

		//FirstName = 2,
		//LastName = 3,
		//Email = 4,
		//Phone = 5,
		//Role = 6,
		//Status = 7,
		////Function = 8,
		//CreationTime = 9,
		//UpdateTime = 10
	}
	

	public enum SortOrder
	{
		None = 0,
		Ascending = 1,
		Descending = 2
	}

	public struct CustomClaimType
	{
		public const string Authorized = "Authorized";
		public const string UserId = "UserId";
		public const string Username = "Username";
		public const string Role = "Role";
		public const string IsMaster = "IsMaster";
	}

	//*************************   SQL DB Enums    *************************//

	public enum OwnershipType
	{
		//None = 0,

		State = 1,
		LocalAuthority = 2,
		Private = 3,
		Mixed = 4,
		Other = 5
	}

	public enum RegistrationMethod
	{
		None = 0,

		AssessedBlock = 1,
		BookAndPage = 2,
	}


	// Values MUST match the DB's 'UserRole' table 'Id' column.
	public enum UserRole
	{
		None = 0,

		SysAdmin = 1,
		Admin = 2,
		User = 3,
	}

	public enum RolePermission
	{
		AccountView = 1,
		AccountViewSubscription = 2,
		AccountEdit = 3,
		AccountEditSubscription = 4,
		SiteView = 5,
		SiteEdit = 6,
		SiteEditLimited = 7,
		UserView = 8,
		UserEdit = 9,
		SystemSopView = 10,
		SystemSopEdit = 11,
		ShiftView = 12,
		ShiftEdit = 13,
		ShiftEditLimited = 14,
		AlertOperatorEdit = 15,
		AlertView = 16,
		AlertEdit = 17,
		AlertOwnership = 18,
		SystemTaskView = 19,
		SystemTaskEdit = 20,
	}

	public enum UserStatus
	{
		Active = 1,
		Inactive = 2,
		Blocked = 3,
		Pending = 4,
	}

	public static class UserRoleNames
	{
		public const string SYS_ADMIN ="SysAdmin";
		public const string ACCOUNT_ADMIN ="AccountAdmin";
		public const string ACCOUNT_USER ="AccountUser";
		public const string ACCOUNT_ALL = $"{ACCOUNT_ADMIN},{ACCOUNT_USER}";
	}


	//*************************    Software Enums    *************************//

	public enum StatusCode
	{
		Ok = 0,

		[Description("The requested operation cannot be performed.")]
		InvalidOperation = 901,

		[Description("The specified ID is invalid.")]
		InvalidId = 1001,
		[Description("Invalid token.")]
		InvalidToken = 1002,
		[Description("Invalid credentials.")]
		InvalidCredentials = 1003,
		[Description("The user is inactive.")]
		InactiveUser = 1004,
		[Description("The requested user or email was not found.")]
		UserNotFound = 1005,
		[Description("The user's email is not verified.")]
		UserEmailNotVerified = 1006,
		[Description("The specified email is already in use by another user.")]
		UserEmailAlreadyInUse = 1007,
		//[Description("The specified phone is already in use by another user.")]
		//UserPhoneAlreadyInUse = 1008,
		//[Description("The specified identification number is already in use by another user.")]
		//UserIdentificationNumberAlreadyInUse = 1009,
		[Description("Invalid password format.")]
		InvalidPasswordFormat = 1010,
		[Description("Failed to create new password")]
		FailedCreateNewPassword = 1011,
		//[Description("The user is the last and only SysAdmin and therefore cannot be deleted.")]
		//UserIsTheOnlySysAdmin = 1012,
		[Description("Expired token.")]
		ExpiredToken = 1013,
		[Description("Invalid verification code.")]
		InvalidOtp = 1015,
		[Description("The user is blocked.")]
		BlockedUser = 1016,
		//[Description("Invalid identification number.")]
		//InvalidIdentificationNumber = 1017,
		[Description("The specified name is already in use by another account.")]
		AccountNameAlreadyInUse = 1018,		
		[Description("The user already owns an account.")]
		UserAlreadyHasAccount = 1019,
		[Description("The user is locked out.")]
		UserLockedOut = 1020,

		[Description("The requested item was not found.")]
		ItemNotFound = 1101,
		[Description("The requested item is inactive and cannot be used.")]
		ItemInactive = 1102,
		//[Description("The specified item already exists.")] 
		//ItemAlreadyExist = 1102,
		//[Description("Some of the specified references does not exist.")] 
		//ReferencesNotExist = 1103,
		[Description("The specified item cannot be deleted since it has dependencies.")]
		ItemHasDependencies = 1104,
		[Description("Some of the DB data required for the operation was not found.")]
		FailedToGetDBData = 1105,

		
		[Description("The specified role cannot be changed.")]
		CannotChangeRole = 1120,


		

		[Description("An item with the same data already exist.")]
		DBDuplicateKeyViolation = 1201,
		[Description("One of the item's referenced IDs are invalid (Foreign Key Conflict).")]
		DBForeignKeyConflict = 1202,


		[Description("The HTTP call to external service has failed.")]
		ExternalHTTPFailed = 1301,

		[Description("The specified external ID is already in use by another data collector.")]
		ExternalIdAlreadyInUse = 1401,
		
		[Description("Failed to load file.")]
		FailedToLoadFile = 1501,
		[Description("Invalid file type.")]
		InvalidFileType = 1502,
		[Description("Bad Kml/Kmz file format.")]
		BadMapFileFormat = 1503,


		[Description("An error has occurred.")]
		GeneralError = 9995,
		[Description("The operation was cancelled.")]
		OperationCancelled = 9996,
		[Description("Some of the request's fields are invalid.")]
		InvalidRequest = 9997,
		[Description("Request has been made to an unauthorized data.")]
		UnauthorizedData = 9998,
		[Description("Unexpected error has occurred.")]
		UnknownError = 9999
	}

	
}
