using System;
using System.Security.Cryptography;
using System.Text;

namespace George.Common
{
	public class Cryptography
	{
		//***********************  Data members/Constants  ***********************//
		private const string LOWER_CASE = "abcdefghijklmnopqursuvwxyz";
		private const string UPPER_CASE = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		private const string NUMBERS = "123456789";
		private const string SPECIALS = @"!@$%^&*()#";

		public const int SALT_SIZE = 24;
		public const int HASH_SIZE = 24;
		public const int PBKDF2_ITT = 500;


		//*************************    Public Methods    *************************//
		#region Public

		public static string EncryptString(string plainText)
		{
			return EncryptString(SysConfig.EncryptionKey, plainText);
		}

		public static string EncryptString(string key, string plainText)
		{
			byte[] iv = new byte[16];
			byte[] array;

			using (Aes aes = Aes.Create())
			{
				aes.Key = Encoding.UTF8.GetBytes(key);
				aes.IV = iv;

				ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

				using (MemoryStream memoryStream = new MemoryStream())
				{
					using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, encryptor, CryptoStreamMode.Write))
					{
						using (StreamWriter streamWriter = new StreamWriter((Stream)cryptoStream))
						{
							streamWriter.Write(plainText);
						}

						array = memoryStream.ToArray();
					}
				}
			}

			return Convert.ToBase64String(array);
		}

		public static string DecryptString(string cipherText)
		{
			return DecryptString(SysConfig.EncryptionKey, cipherText);
		}

		public static string DecryptString(string key, string cipherText)
		{
			byte[] iv = new byte[16];
			byte[] buffer = Convert.FromBase64String(cipherText);

			using (Aes aes = Aes.Create())
			{
				aes.Key = Encoding.UTF8.GetBytes(key);
				aes.IV = iv;
				ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

				using (MemoryStream memoryStream = new MemoryStream(buffer))
				{
					using (CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, decryptor, CryptoStreamMode.Read))
					{
						using (StreamReader streamReader = new StreamReader((Stream)cryptoStream))
						{
							return streamReader.ReadToEnd();
						}
					}
				}
			}
		}

		public static string GeneratePassword(int length = 10, bool useLowercase = true, bool useUppercase = true,
																				bool useNumbers = true, bool useSpecial = true)
		{
			char[] password = new char[length];
			string charSet = ""; // Initialise to blank.
			System.Random _random = new Random();

			// Build up the character set to choose from.
			if (useLowercase)
				charSet += LOWER_CASE;

			if (useUppercase)
				charSet += UPPER_CASE;

			if (useNumbers)
				charSet += NUMBERS;

			if (useSpecial)
				charSet += SPECIALS;

			for (int i = 0 ; i < length ; i++)
				password[i] = charSet[_random.Next(charSet.Length - 1)];

			return String.Join(null, password);
		}

		public static string GeneratePasswordHash(string password)
		{
			try
			{
				// Generate random salt.
				var csprng = new RNGCryptoServiceProvider();
				var salt = new byte[SALT_SIZE];
				csprng.GetBytes(salt);

				// Generate the password hash.
				var hash = PBKDF2(password, salt, PBKDF2_ITT, HASH_SIZE);
				return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(hash);
			}
			catch (Exception ex)
			{
				throw;
			}
		}

		public static bool VerifyPasswordHash(string password, string dbHash)
		{
			try
			{
				if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(dbHash))
					return false;

				char[] delimiter = { ':' };
				var split = dbHash.Split(delimiter);

				var salt = Convert.FromBase64String(split[0]);
				var hash = Convert.FromBase64String(split[1]);

				var hashToValidate = PBKDF2(password, salt, PBKDF2_ITT, hash.Length);

				return SlowEquals(hash, hashToValidate);
			}
			catch (FormatException)
			{
				return false;
			}
			catch (Exception ex)
			{
				throw ex;
			}
		}

		#endregion


		//*************************    Private Methods    ************************//
		#region Private

		private static byte[] PBKDF2(string password, byte[] salt, int pBKDF2_ITT, int outputBytes)
		{
			try
			{
				var pbkdf2 = new Rfc2898DeriveBytes(password, salt);
				pbkdf2.IterationCount = pBKDF2_ITT;
				return pbkdf2.GetBytes(outputBytes);
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static bool SlowEquals(byte[] dbHash, byte[] passHash)
		{
			try
			{
				var diff = (uint)dbHash.Length ^ (uint)passHash.Length;
				for (var i = 0 ; i < dbHash.Length && i < passHash.Length ; i++)
					diff |= dbHash[i] ^ (uint)passHash[i];

				return diff == 0;
			}
			catch (Exception)
			{
				return false;
			}
		}

		#endregion
	}
}