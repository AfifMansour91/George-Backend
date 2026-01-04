using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using George.Common;

namespace George.Services
{
	public class LocalFileStorage : IFileStorage
	{
		private readonly string _basePath;
		private readonly string _env;
		private readonly ILogger<LocalFileStorage> _logger;

		public LocalFileStorage(ILogger<LocalFileStorage> logger)
		{
			_basePath = SysConfig.Data.StorageLocalInternalBasePath ?? "./FileStorage";
			_env = SysConfig.Data.EnvironmentName.Trim('/').Trim('\\');
			_logger = logger;

			// Ensure base directory exists
			if (!Directory.Exists(_basePath))
			{
				Directory.CreateDirectory(_basePath);
				_logger.LogInformation($"Created storage directory: {_basePath}");
			}
		}

		public async Task<FileManagerRes> UploadFileAsync(IFormFile file, string? path, CancellationToken cancelToken = default)
		{
			FileManagerRes res = new();
			res.OriginalFileName = file.FileName;
			string filePath = string.Empty;
			string fullPath = string.Empty;

			try
			{
				// Create unique file path
				filePath = CreateUniqueFilePath(file, path);
				fullPath = Path.Combine(_basePath, filePath);

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
				res.FilePath = filePath; // Store relative path
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"UploadFileAsync() failed to upload file to local storage, fullpath = {fullPath}");
				res.Exception = ex;
			}

			return res;
		}

		public async Task<FileManagerRes> CopyFileAsync(string srcPath, string destPath, CancellationToken cancelToken = default)
		{
			FileManagerRes res = new();

			try
			{
				string srcFullPath = Path.Combine(_basePath, srcPath);
				string destFullPath = Path.Combine(_basePath, AddEnvToPath(destPath));

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
				_logger.LogError(ex, $"CopyFileAsync() failed to copy file, srcPath = {srcPath}, destPath = {destPath}");
				res.Exception = ex;
			}

			return res;
		}

		public async Task<FileManagerRes> MoveFileAsync(string srcPath, string destPath, CancellationToken cancelToken = default)
		{
			FileManagerRes res;

			// Copy the file
			res = await CopyFileAsync(srcPath, destPath, cancelToken);
			if (res.IsSuccessful)
			{
				// Delete the old file
				await DeleteFileAsync(srcPath);
			}

			return res;
		}

		public async Task<FileManagerRes> DeleteFileAsync(string path, CancellationToken cancelToken = default)
		{
			FileManagerRes res = new();

			try
			{
				string fullPath = Path.Combine(_basePath, path);
				if (File.Exists(fullPath))
				{
					await Task.Run(() => File.Delete(fullPath), cancelToken);
					res.IsSuccessful = true;
					res.FilePath = path;
				}
				else
				{
					_logger.LogWarning($"File not found for deletion: {fullPath}");
					res.IsSuccessful = true; // Consider it successful if file doesn't exist
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, $"DeleteFileAsync() failed to delete file, path = {path}");
				res.Exception = ex;
			}

			return res;
		}

		private string CreateUniqueFilePath(IFormFile file, string? path)
		{
			string res;

			var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLower();
			string fileName = Guid.NewGuid().ToString("N") + "." + extension;

			if (!string.IsNullOrEmpty(path))
			{
				path = path.Trim('/').Trim('\\');
				res = $"{_env}\\{path}\\{fileName}"; // Use backslash for Windows paths
			}
			else
			{
				res = $"{_env}\\{fileName}";
			}

			return res;
		}

		private string AddEnvToPath(string path)
		{
			path = path.Trim('/').Trim('\\').Replace('/', '\\'); // Normalize to Windows paths
			return $"{_env}\\{path}";
		}
	}
}

