using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TBM.API.Swagger
{
    public class FileUploadOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var hasFileUpload = context.MethodInfo
                .GetParameters()
                .Any(p => p.ParameterType == typeof(IFormFile));

            if (!hasFileUpload)
                return;

            // Keep real route/query parameters and remove only the synthetic file parameter.
            operation.Parameters = (operation.Parameters ?? Enumerable.Empty<OpenApiParameter>())
                .Where(p => !string.Equals(p.Name, "file", StringComparison.OrdinalIgnoreCase))
                .ToList();

            operation.RequestBody = new OpenApiRequestBody
            {
                Content =
                {
                    ["multipart/form-data"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchema
                        {
                            Type = "object",
                            Properties =
                            {
                                ["file"] = new OpenApiSchema
                                {
                                    Type = "string",
                                    Format = "binary"
                                }
                            },
                            Required = new HashSet<string> { "file" }
                        }
                    }
                }
            };
        }
    }
}
