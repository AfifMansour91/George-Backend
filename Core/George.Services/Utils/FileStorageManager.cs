using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.HttpSys;
using Microsoft.Extensions.Logging;
//using SharpCompress.Common;
using George.Common;

namespace George.Services
{
	public class FileManagerRes
	{
		public bool IsSuccessful { get; set; }
		public string? OriginalFileName { get; set; }
		public string? FilePath { get; set; }
		public Exception? Exception { get; set; }

	}

	public class FileStorageManager
	{
		//*********************  Data members/Constants  *********************//
		private AmazonS3Client _awsClient;
		private readonly string _bucket;
		private readonly string _env;
		private readonly ILogger<FileStorageManager> _logger;

		//**************************    Construction    **************************//
		public FileStorageManager(ILogger<FileStorageManager> logger)
		{
			_bucket = SysConfig.Data.AWSBucket;
			_env = SysConfig.Data.EnvironmentName.Trim('/').Trim('\\');

			_logger = logger;

			_awsClient = new AmazonS3Client(SysConfig.Data.AWSAccessKey,
											SysConfig.Data.AWSKeySecret,
											Amazon.RegionEndpoint.EUCentral1);

			//_awsClient.GetBucketVersioningAsync(_bucket).Wait();

		}


		//*************************    Public Methods    *************************//

		public async Task<FileManagerRes> UploadFileAsync(IFormFile file, string? path, CancellationToken cancelToken = default)
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
					PutObjectResponse response = await _awsClient.PutObjectAsync(request, cancelToken);
					if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
					{
						res.IsSuccessful = true;
						res.FilePath = request.Key;
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError($"UploadFileAsync() failed to upload IFormFile file to S3, fullpath = {filePath}", ex);
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
			FileManagerRes res = new();

			try
			{
				// Upload the file to S3.

				// Set the request.
				var request = new CopyObjectRequest {
					SourceBucket = _bucket,
					SourceKey = srcPath,
					DestinationBucket = _bucket,
					DestinationKey = AddEnvToPath(destPath),
					CannedACL = S3CannedACL.PublicRead
				};

				// Send the request
				CopyObjectResponse response = await _awsClient.CopyObjectAsync(request, cancelToken);
				if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
				{
					res.IsSuccessful = true;
					res.FilePath = request.DestinationKey;
				}
			}
			catch (Exception ex)
			{
				_logger.LogError($"CopyFileAsync() failed to copy file in S3, srcPath = {srcPath}, destPath = {destPath}", ex);
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

			var request = new DeleteObjectRequest {
				BucketName = _bucket,
				Key = path
			};

			try
			{
				var response = await _awsClient.DeleteObjectAsync(request, cancelToken);
				if (response.HttpStatusCode == System.Net.HttpStatusCode.NoContent)
				{
					res.IsSuccessful = true;
					res.FilePath = path;
				}
			}
			catch (DeleteObjectsException ex)
			{
				_logger.LogError($"DeleteFileAsync() failed, when try to remove file from S3, path = {path}", ex);
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
