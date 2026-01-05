using Amazon.S3;
using Amazon.S3.Model;
//using SharpCompress.Common;
using George.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace George.Services
{
	public class FileManagerRes
	{
		public bool IsSuccessful { get; set; }
		public string? OriginalFileName { get; set; }
		public string? FilePath { get; set; }
		public Exception? Exception { get; set; }

	}

	public class FileStorageManager : IFileStorage
	{
		//*********************  Data members/Constants  *********************//
		private AmazonS3Client? _awsClient;
		private readonly string _bucket;
		private readonly string _env;
		private readonly ILogger<FileStorageManager> _logger;
		private readonly bool _useLocalStorage;

		//**************************    Construction    **************************//
		public FileStorageManager(ILogger<FileStorageManager> logger)
		{
			// Safely access SysConfig.Data with null checks
			var configData = SysConfig.Data;
			_bucket = configData?.AWSBucket ?? string.Empty;
			_env = (configData?.EnvironmentName ?? "PROD").Trim('/').Trim('\\');
			_logger = logger;

			// Check if local storage should be used
			// Priority: 1. UseLocalStorage flag from appsettings, 2. Check if AWS credentials are configured
			if (configData?.UseLocalStorage == true)
			{
				_useLocalStorage = true;
			}
			else
			{
				// If AWS credentials are not configured, use local storage
				_useLocalStorage = string.IsNullOrEmpty(configData?.AWSBucket) ||
								  string.IsNullOrEmpty(configData?.AWSAccessKey) ||
								  string.IsNullOrEmpty(configData?.AWSKeySecret);
			}

			if (!_useLocalStorage)
			{
				try
				{
					_awsClient = new AmazonS3Client(configData?.AWSAccessKey,
													configData?.AWSKeySecret,
													Amazon.RegionEndpoint.EUCentral1);
					_logger.LogInformation("Using S3 file storage");
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Failed to initialize S3 client, falling back to local storage");
					_useLocalStorage = true;
				}
			}

			if (_useLocalStorage)
			{
				_logger.LogInformation("Using local file storage");
				// Ensure local storage directory exists
				string basePath = configData?.StorageLocalInternalBasePath ?? "./FileStorage";
				if (!Directory.Exists(basePath))
				{
					Directory.CreateDirectory(basePath);
					_logger.LogInformation($"Created storage directory: {basePath}");
				}
			}
		}


		//*************************    Public Methods    *************************//

		public async Task<FileManagerRes> UploadFileAsync(IFormFile file, string? path, CancellationToken cancelToken = default)
		{
			if (_useLocalStorage)
			{
				return await UploadFileLocalAsync(file, path, cancelToken);
			}

			return await UploadFileS3Async(file, path, cancelToken);
		}

		private async Task<FileManagerRes> UploadFileLocalAsync(IFormFile file, string? path, CancellationToken cancelToken = default)
		{
			FileManagerRes res = new();
			res.OriginalFileName = file.FileName;
			string filePath = string.Empty;
			string fullPath = string.Empty;

			try
			{
				// Get local storage base path
				string basePath = SysConfig.Data?.StorageLocalInternalBasePath ?? "./FileStorage";

				// Create unique file path
				filePath = CreateUniqueFilePath(file, path);
				fullPath = Path.Combine(basePath, filePath.Replace('/', Path.DirectorySeparatorChar));

				// Ensure directory exists
				var directory = Path.GetDirectoryName(fullPath);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				// Save file to disk
				using (var stream = new FileStream(fullPath, FileMode.Create))
				{
					await file.CopyToAsync(stream, cancelToken);
				}

				res.IsSuccessful = true;
				res.FilePath = filePath;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"UploadFileLocalAsync() failed to upload file to local storage, fullpath = {fullPath}");
				res.Exception = ex;
			}

			return res;
		}

		private async Task<FileManagerRes> UploadFileS3Async(IFormFile file, string? path, CancellationToken cancelToken = default)
		{
			FileManagerRes res = new();

			res.OriginalFileName = file.FileName;
			string filePath = string.Empty;
			try
			{
				// Get the file and convert it to a byte array.
				byte[] fileBytes = new Byte[file.Length];
				file.OpenReadStream().Read(fileBytes, 0, (int)file.Length);

				// create unique file name for prevent the mess
				filePath = CreateUniqueFilePath(file, path);

				// Upload the file to S3.
				using (var stream = new MemoryStream(fileBytes))
				{
					// Set the request.
					PutObjectRequest request = new PutObjectRequest {
						BucketName = _bucket,
						Key = filePath,
						InputStream = stream,
						ContentType = file.ContentType//,
						//CannedACL = S3CannedACL.PublicRead
					};

					// Send the request
					if (_awsClient != null)
					{
						PutObjectResponse response = await _awsClient.PutObjectAsync(request, cancelToken);
						if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
						{
							res.IsSuccessful = true;
							res.FilePath = request.Key;
						}
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"UploadFileS3Async() failed to upload IFormFile file to S3, fullpath = {filePath}");
				res.Exception = ex;
			}

			return res;
		}

		//public async Task<FileManagerRes> UploadAndResizeFileAsync(IFormFile file, string? path, CancellationToken cancelToken = default)
		//{
		//	FileManagerRes res = new();

		//	string filePath = string.Empty;
		//	try
		//	{
		//		// Get the file and convert it to a byte array.
		//		byte[] fileBytes = new Byte[file.Length];
		//		file.OpenReadStream().Read(fileBytes, 0, (int)file.Length);

		//		// create unique file name for prevent the mess
		//		filePath = CreateUniqueFilePath(file, path);

		//		// Upload the file to S3.
		//		using (var stream = new MemoryStream(fileBytes))
		//		{
		//			// Resized the image.
		//			using (var resizedStream = ImageUtils.ResizeImage(stream, SysConfig.Data.MinImageWidthForResize, 
		//						SysConfig.Data.ImageResizeQuality, SysConfig.Data.MinImageWidthForResize))
		//			{
		//				// Set the request.
		//				PutObjectRequest request = new PutObjectRequest {
		//					BucketName = _bucket,
		//					Key = filePath,
		//					InputStream = resizedStream != null ? resizedStream : stream,
		//					ContentType = file.ContentType,
		//					CannedACL = S3CannedACL.PublicRead
		//				};

		//				// Send the request
		//				PutObjectResponse response = await _awsClient.PutObjectAsync(request, cancelToken);
		//				if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
		//				{
		//					res.IsSuccessful = true;
		//					res.FilePath = request.Key;
		//				}
		//			}
		//		}
		//	}
		//	catch (Exception ex)
		//	{
		//		_logger.LogError($"UploadFileAsync() failed to upload IFormFile file to S3, fullpath = {filePath}", ex);
		//	}

		//	return res;
		//}

		public async Task<FileManagerRes> CopyFileAsync(string srcPath, string destPath, CancellationToken cancelToken = default)
		{
			if (_useLocalStorage)
			{
				return await CopyFileLocalAsync(srcPath, destPath, cancelToken);
			}

			return await CopyFileS3Async(srcPath, destPath, cancelToken);
		}

		private async Task<FileManagerRes> CopyFileLocalAsync(string srcPath, string destPath, CancellationToken cancelToken = default)
		{
			FileManagerRes res = new();

			try
			{
				string basePath = SysConfig.Data?.StorageLocalInternalBasePath ?? "./FileStorage";
				string srcFullPath = Path.Combine(basePath, srcPath.Replace('/', Path.DirectorySeparatorChar));
				string destFullPath = Path.Combine(basePath, AddEnvToPath(destPath).Replace('/', Path.DirectorySeparatorChar));

				// Ensure destination directory exists
				var directory = Path.GetDirectoryName(destFullPath);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}

				if (File.Exists(srcFullPath))
				{
					await Task.Run(() => File.Copy(srcFullPath, destFullPath, overwrite: true), cancelToken);
					res.IsSuccessful = true;
					res.FilePath = AddEnvToPath(destPath);
				}
				else
				{
					_logger.LogWarning($"Source file not found: {srcFullPath}");
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"CopyFileLocalAsync() failed to copy file, srcPath = {srcPath}, destPath = {destPath}");
				res.Exception = ex;
			}

			return res;
		}

		private async Task<FileManagerRes> CopyFileS3Async(string srcPath, string destPath, CancellationToken cancelToken = default)
		{
			FileManagerRes res = new();

			try
			{
				// Set the request.
				var request = new CopyObjectRequest {
					SourceBucket = _bucket,
					SourceKey = srcPath,
					DestinationBucket = _bucket,
					DestinationKey = AddEnvToPath(destPath),
					CannedACL = S3CannedACL.PublicRead
				};

				// Send the request
				if (_awsClient != null)
				{
					CopyObjectResponse response = await _awsClient.CopyObjectAsync(request, cancelToken);
					if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
					{
						res.IsSuccessful = true;
						res.FilePath = request.DestinationKey;
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"CopyFileS3Async() failed to copy file in S3, srcPath = {srcPath}, destPath = {destPath}");
				res.Exception = ex;
			}

			return res;
		}

		public async Task<FileManagerRes> MoveFileAsync(string srcPath, string destPath, CancellationToken cancelToken = default)
		{
			FileManagerRes res;

			// Copy the file.
			res = await CopyFileAsync(srcPath, destPath, cancelToken);
			if(res.IsSuccessful) 
			{
				// Delete the old file.
				await DeleteFileAsync(srcPath);
			}

			return res;
		}

	public async Task<FileManagerRes> DeleteFileAsync(string path, CancellationToken cancelToken = default)
	{
		FileManagerRes res = new();

		if (_useLocalStorage)
		{
			return await DeleteFileLocalAsync(path, cancelToken);
		}

		var request = new DeleteObjectRequest {
			BucketName = _bucket,
			Key = path
		};

		try
		{
			if (_awsClient != null)
			{
				var response = await _awsClient.DeleteObjectAsync(request, cancelToken);
				if (response.HttpStatusCode == System.Net.HttpStatusCode.NoContent)
				{
					res.IsSuccessful = true;
					res.FilePath = path;
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, $"DeleteFileAsync() failed, when try to remove file from S3, path = {path}");
		}

		return res;
	}

	private async Task<FileManagerRes> DeleteFileLocalAsync(string path, CancellationToken cancelToken = default)
	{
		FileManagerRes res = new();

		try
		{
			string basePath = SysConfig.Data?.StorageLocalInternalBasePath ?? "./FileStorage";
			string fullPath = Path.Combine(basePath, path.Replace('/', Path.DirectorySeparatorChar));

			if (File.Exists(fullPath))
			{
				await Task.Run(() => File.Delete(fullPath), cancelToken);
				res.IsSuccessful = true;
				res.FilePath = path;
			}
			else
			{
				_logger.LogWarning($"File not found for deletion: {fullPath}");
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, $"DeleteFileLocalAsync() failed to delete file, path = {path}");
			res.Exception = ex;
		}

		return res;
	}


		//*************************    Private/Protected Methods    *************************//
		private string CreateUniqueFilePath(IFormFile file, string? path)
		{
			string res;

			var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLower();

			string fileName = Guid.NewGuid().ToString("N") + "." + extension;
			if (!string.IsNullOrEmpty(path))
			{
				path = path.Trim('/').Trim('\\');
				res = $"{_env}/{path}/{fileName}";
			}
			else
			{
				res = $"{_env}/{fileName}";
			}

			return res;
		}

		private string AddEnvToPath(string path)
		{
			path = path.Trim('/').Trim('\\');

			return $"{_env}/{path}";
		}

		//private string GetKeyByUrl(string url)
		//{
		//	return _bucket + url.Split(_bucket)[1];
		//}

		//private async Task<string> EnsureBucketExistsAsync(string bucketName)
		//{
		//	var bucket = $"{_rootBucketName}/{_env}/{bucketName}".ToLower();
		//	var exists = await _awsClient.DoesS3BucketExistAsync(bucket).ConfigureAwait(false);
		//	if (exists)
		//		return bucket;

		//	// we need to do separate request for checking if bucket exists, because method below throws exception if it is already exists
		//	await _awsClient.EnsureBucketExistsAsync(bucket).ConfigureAwait(false);
		//	return bucket;
		//}
	}
}
