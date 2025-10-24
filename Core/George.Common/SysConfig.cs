
namespace George.Common
{
	public static class SysConfig
	{
		public class Parameter
		{
			public const string EnvironmentName = "EnvironmentName";
			public const string DefaultPageSize = "DefaultPageSize";
			public const string RefreshDataPageSize = "RefreshDataPageSize";
			public const string RefreshDataWaitTimeInMillisec = "RefreshDataWaitTimeInMillisec";
			public const string RefreshDataWaitTimeLongInMillisec = "RefreshDataWaitTimeLongInMillisec";
			public const string StorageExternalBasePath = "StorageExternalBasePath";
			public const string StorageInternalBasePath = "StorageInternalBasePath";
			public const string StorageLocalExternalBasePath = "StorageLocalExternalBasePath";
			public const string StorageLocalInternalBasePath = "StorageLocalInternalBasePath";
			public const string TempFolder = "TempFolder";
			//public const string WebAppUrl = "WebAppUrl";
			public const string LockoutExpirationInMin = "LockoutExpirationInMin";
			public const string MaxFailCountBeforeLockout = "MaxFailCountBeforeLockout";
			public const string AWSBucket = "AWSBucket";
			public const string AWSAccessKey = "AWSAccessKey";
			public const string AWSKeySecret = "AWSKeySecret";
		}

		public class ParameterValue
		{
			public string Key { get; set; } = null!;
			public string? Value { get; set; }
		}

		//*********************  Data members/Constants  *********************//
		public const int INVALID_ID = -1;
		public const int CONTROL_CENTER_ID = 1;
		public const int PLACEHOLDER_ID = -999999;
		private static ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
		private static SysConfigData _data = new SysConfigData(); // TODO: Set to null and init on startup.

		//*************************    Properties    *************************//


		public static string EncryptionKey { get; set; } = @"gsxr750157a4e4584bbce2gh2315a555";

        public static bool IsInitialized
		{
			get
			{
				return (SysConfig.Data != null);
			}
		}

		public static SysConfigData Data
		{
			get
			{
				_rwLock.EnterReadLock();
				try
				{
					return _data;
				}
				finally
				{
					_rwLock.ExitReadLock();
				}
			}
			set
			{
				_rwLock.EnterWriteLock();
				try
				{
					_data = value;
				}
				finally
				{
					_rwLock.ExitWriteLock();
				}
			}
		}
	}
}
