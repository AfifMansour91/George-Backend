using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace George.Api.Core
{
    public class UploadAttribute : Attribute
    {
    }

	/// <summary>
	/// Filter to enable handling file upload in swagger
	/// </summary>
	public class FileUploadFilter : IOperationFilter
	{
		public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var isFileUploadOperation =
             context.MethodInfo.CustomAttributes.Any(a => a.AttributeType == typeof(UploadAttribute));
            if (!isFileUploadOperation) return;

            var uploadFileMediaType = new OpenApiMediaType()
            {
                Schema = new OpenApiSchema()
                {
                    Type = "object",
                    Properties =
                    {
                        ["uploadedFile"] = new OpenApiSchema()
                        {
                            Description = "Upload File",
                            Type = "file",
                            Format = "binary"
                        }
                    },
                    Required = new HashSet<string>()
                    {
                        "uploadedFile"
                    }
                }
            };

            operation.RequestBody = new OpenApiRequestBody
            {
                Content =
                {
                    ["multipart/form-data"] = uploadFileMediaType
                }
            };
        }
	}
}