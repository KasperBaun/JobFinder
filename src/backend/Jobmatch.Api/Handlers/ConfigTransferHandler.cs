using System.Globalization;
using Jobmatch.Api.Infrastructure;
using Jobmatch.Api.Models;
using Jobmatch.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Handlers;

public interface IConfigTransferHandler
{
    Task<IResult> Export();
    Task<IResult> Import(IFormFile? file);
}

public sealed class ConfigTransferHandler(IConfigTransferService transfer, ILogger<ConfigTransferHandler> logger)
    : HandlerBase(logger), IConfigTransferHandler
{
    public Task<IResult> Export() => ExecuteAsync(
        "export configuration",
        () =>
        {
            var bytes = transfer.Export();
            var fileName = $"jobfinder-export-{DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.zip";
            return Task.FromResult<IResult>(Results.File(bytes, "application/zip", fileName));
        });

    public Task<IResult> Import(IFormFile? file) => ExecuteAsync(
        "import configuration",
        () =>
        {
            if (file is null || file.Length == 0)
                throw new InvalidRequestException("No file was uploaded.");

            using var stream = file.OpenReadStream();
            var result = transfer.Import(stream);
            var dto = new ImportResponse(result.Restored, result.Skipped, result.Warnings);
            return Task.FromResult<IResult>(Results.Ok(dto));
        });
}
