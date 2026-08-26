using Jobmatch.Api.Infrastructure;
using Jobmatch.Features.Cv;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jobmatch.Api.Features.Drafting;

public interface IDraftingHandler
{
    Task<IResult> Start(DraftRequest request);
    Task<IResult> Status();
    Task<IResult> GetCv();
    Task<IResult> UpdateCv(CvUpdateRequest request);
}

public sealed class DraftingHandler(
    DraftManager drafts,
    ICvDocumentStore cv,
    ILogger<DraftingHandler> logger) : HandlerBase(logger), IDraftingHandler
{
    public Task<IResult> Start(DraftRequest request) => ExecuteAsync(
        "draft application for listing {ListingId}",
        () =>
        {
            if (string.IsNullOrWhiteSpace(request.RunId) || string.IsNullOrWhiteSpace(request.ListingId))
                throw new InvalidRequestException("Both runId and listingId are required.");

            return Task.FromResult<IResult>(Results.Accepted(value: drafts.Start(request)));
        },
        request.ListingId);

    public Task<IResult> Status() => ExecuteAsync(
        "get draft status",
        () => Task.FromResult<IResult>(Results.Ok(drafts.Snapshot())));

    public Task<IResult> GetCv() => ExecuteAsync(
        "get cv",
        () => Task.FromResult<IResult>(Results.Ok(new CvResponse(cv.Find()))));

    public Task<IResult> UpdateCv(CvUpdateRequest request) => ExecuteAsync(
        "update cv",
        () =>
        {
            cv.Save(request.Text);
            return Task.FromResult<IResult>(Results.Ok(new CvResponse(cv.Find())));
        });
}
