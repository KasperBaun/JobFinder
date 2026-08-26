using Jobmatch.Api.Features.Drafting;
using Jobmatch.Features.Drafting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Api.Features.Drafting;

/// <summary>
/// One draft runs at a time, because a second would load a second model into memory. What matters is
/// which of the two callers that limit applies to: the same listing asked for twice is one request
/// and observes the run, a different listing is a different request and has to be told no.
/// </summary>
public sealed class DraftManagerTests
{
    [Fact]
    public void Start_SameListingWhileRunning_ObservesTheRunInFlight()
    {
        using var fixture = new InFlightDraft();

        var again = fixture.Manager.Start(new DraftRequest("run-1", "listing-a"));

        Assert.Equal(DraftState.Drafting, again.State);
        Assert.Equal("listing-a", again.ListingId);
        Assert.Equal(fixture.StartedAt, again.StartedAt);
    }

    // Returning the running draft's progress for a listing nobody is drafting reports work that will
    // never happen: the caller sees 202 and a status that settles as Completed, for the other listing.
    [Fact]
    public void Start_DifferentListingWhileRunning_IsRefused()
    {
        using var fixture = new InFlightDraft();

        var refused = Assert.Throws<ConflictException>(
            () => fixture.Manager.Start(new DraftRequest("run-1", "listing-b")));

        Assert.Contains("listing-a", refused.Message);
    }

    [Fact]
    public void Start_SameListingInADifferentRun_IsRefused()
    {
        using var fixture = new InFlightDraft();

        Assert.Throws<ConflictException>(
            () => fixture.Manager.Start(new DraftRequest("run-2", "listing-a")));
    }

    [Fact]
    public async Task Start_AfterTheRunFinishes_IsAccepted()
    {
        using var fixture = new InFlightDraft();
        await fixture.FinishAsync();

        var next = fixture.Manager.Start(new DraftRequest("run-1", "listing-b"));

        Assert.Equal(DraftState.Drafting, next.State);
        Assert.Equal("listing-b", next.ListingId);
    }

    /// <summary>A manager holding one draft that has genuinely entered the service and is parked there.</summary>
    private sealed class InFlightDraft : IDisposable
    {
        private readonly BlockingDraftService _service = new();
        private readonly ServiceProvider _provider;

        public InFlightDraft()
        {
            var services = new ServiceCollection();
            services.AddScoped<IApplicationDraftService>(_ => _service);
            _provider = services.BuildServiceProvider();

            Manager = new DraftManager(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<DraftManager>.Instance);

            StartedAt = Manager.Start(new DraftRequest("run-1", "listing-a")).StartedAt;
            _service.Entered.Task.GetAwaiter().GetResult();
        }

        public DraftManager Manager { get; }

        public DateTimeOffset? StartedAt { get; }

        public async Task FinishAsync()
        {
            _service.Release();
            for (var attempt = 0; attempt < 100; attempt++)
            {
                if (Manager.Snapshot().State != DraftState.Drafting) return;
                await Task.Delay(20);
            }

            throw new Xunit.Sdk.XunitException("Draft never left the Drafting state.");
        }

        public void Dispose()
        {
            _service.Release();
            _provider.Dispose();
        }
    }

    private sealed class BlockingDraftService : IApplicationDraftService
    {
        private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Release() => _gate.TrySetResult();

        public async Task<DraftedDocuments> DraftAsync(string runId, string listingId, CancellationToken ct = default)
        {
            Entered.TrySetResult();
            await _gate.Task.WaitAsync(ct);
            return new DraftedDocuments(
                listingId,
                runId,
                new ApplicationDraft("Backend Developer", "Acme A/S", "resume", "letter"),
                "resume.docx",
                "letter.docx",
                DateTimeOffset.UnixEpoch);
        }
    }
}
