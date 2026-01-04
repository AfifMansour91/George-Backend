using Microsoft.AspNetCore.Http;

namespace George.Services
{
	public interface IFileStorage
	{
		Task<FileManagerRes> UploadFileAsync(IFormFile file, string? path, CancellationToken cancelToken = default);
		Task<FileManagerRes> CopyFileAsync(string srcPath, string destPath, CancellationToken cancelToken = default);
		Task<FileManagerRes> MoveFileAsync(string srcPath, string destPath, CancellationToken cancelToken = default);
		Task<FileManagerRes> DeleteFileAsync(string path, CancellationToken cancelToken = default);
	}
}

