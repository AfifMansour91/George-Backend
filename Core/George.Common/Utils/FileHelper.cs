using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace George.Common
{
	public static class FileHelper
	{
		private const string FILE_KEY_DELIMITER = "::"; // This string cannot be part of a path or file name.
		private const string REGISTRYUNITS_FOLDER = "RegistryUnits";
		private const string SYSTEM_FOLDER = "System";
		private const string TEMP_FOLDER = "Temp";
		private const string USERS_FOLDER = "Users";

		public static string GetFileInternalPath(string? filePath)
		{
			if( !filePath.HasValue() )
				return string.Empty;

			filePath = filePath!.Trim('/').Trim('\\');
			string baseUrl = SysConfig.Data.StorageInternalBasePath.Trim('/').Trim('\\');

			return $"{baseUrl}/{filePath}";
		}

		public static string GetFileExternalPath(string? filePath)
		{
			if( !filePath.HasValue() )
				return string.Empty;

			filePath = filePath!.Trim('/').Trim('\\');
			string baseUrl = SysConfig.Data.StorageExternalBasePath.Trim('/').Trim('\\');

			string res = $"{baseUrl}/{filePath}";

			return res;
		}

		public static string GetSystemFolderPath()
		{
			return $"{SYSTEM_FOLDER}";
		}

		public static string GetTempFolderPath()
		{
			return $"{TEMP_FOLDER}";
		}

		public static string GetUserFolderPath(int userId)
		{
			return $"{USERS_FOLDER}/{userId}";
		}

		public static string GetRegistryUnitFolderPath(int registryUnitId)
		{
			return $"{REGISTRYUNITS_FOLDER}/{registryUnitId}";
		}



		public static string? GetFileName(string path)
		{
			string? res = default;

			if (!string.IsNullOrEmpty(path))
				res = path!.Split('/').LastOrDefault();

			return res;
		}

		public static string? EncryptFileKey(string originalFileName, string filePath)
		{
			string data = originalFileName + FILE_KEY_DELIMITER + filePath;

			return Cryptography.EncryptString(data);
		}

		/// <summary>
		/// Decrypt a Tuple where Item1 is the original file name add Item2 is the new full file path (including name).
		/// </summary>
		/// <returns>
		/// Tuple, Item1 - the original file name, Item2 - the new full file path (including name).
		/// </returns>
		public static Tuple<string, string>? DecryptFileKey(string data)
		{
			string fileData = Cryptography.DecryptString(data);

			string[] dataFields =  fileData!.Split(FILE_KEY_DELIMITER);
			if (dataFields.Length < 2)
				return null;

			return new Tuple<string, string>(dataFields[0], dataFields[1]);
		}

		public static string AddFileNameToPath(string path, string fileName)
		{
			string res;

			if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(fileName))
				throw new GeorgeInvalidArgumentException("path or fileName strings are empty.");

			if (path.EndsWith('/'))
				res = path + fileName;
			else
				res = path + "/" + fileName;

			return res;
		}

		public static string? GetFilePathFromKey(string? fileKey)
		{
			string? res = null;

			Tuple<string, string>? fileData = GetFileDataFromKey(fileKey);
			if(fileData != null)
				res = fileData.Item2;

			return res;
		}

		public static string? GetOriginalFileNameFromKey(string? fileKey)
		{
			string? res = null;

			Tuple<string, string>? fileData = GetFileDataFromKey(fileKey);
			if(fileData != null)
				res = fileData.Item1;

			return res;
		}

		/// <summary>
		/// Decrypt a Tuple where Item1 is the file name add Item2 is the full file path (including name).
		/// </summary>
		/// <returns>
		/// Tuple, Item1 - the original file name, Item2 - the full file path (including name).
		/// </returns>
		public static Tuple<string, string>? GetFileDataFromKey(string? fileKey)
		{
			Tuple<string, string>? res = null;

			// Set the file path to the model.
			if (fileKey.HasValue())
			{
				res = FileHelper.DecryptFileKey(fileKey!);
				if (res == null || !res.Item1.HasValue() || !res.Item2.HasValue())
					throw new GeorgeInvalidArgumentException($"The file key is invalid.");
			}

			return res;
		}

		//public string Encrypt(string filePath)
		//{
		//	return PasswordHandler.;
		//}

		//public string Decrypt(string filePath) 
		//{
		//}
	}
}
