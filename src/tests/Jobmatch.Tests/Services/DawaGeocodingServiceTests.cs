using System.Net;
using System.Text;
using Jobmatch.Features.Skillsets;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jobmatch.Tests.Services;

public sealed class DawaGeocodingServiceTests
{
    private const string MiniPayload =
        """[ { "id": "x", "x": 12.56553, "y": 55.67594, "betegnelse": "Rådhuspladsen 1, 1550 København V" } ]""";

    private static DawaGeocodingService Create(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new FakeHandler(respond)), NullLogger<DawaGeocodingService>.Instance);

    [Fact]
    public async Task GeocodeAsync_Maps_X_To_Longitude_And_Y_To_Latitude()
    {
        var svc = Create(_ => Json(MiniPayload));

        var result = await svc.GeocodeAsync("Rådhuspladsen 1");

        Assert.NotNull(result);
        Assert.Equal(55.67594, result!.Latitude);
        Assert.Equal(12.56553, result.Longitude);
        Assert.Equal("Rådhuspladsen 1, 1550 København V", result.ResolvedAddress);
    }

    [Fact]
    public async Task GeocodeAsync_Falls_Back_To_AdgangsAdresser_When_Adresser_Is_Empty()
    {
        var svc = Create(req =>
            req.RequestUri!.AbsolutePath.Contains("/adgangsadresser") ? Json(MiniPayload) : Json("[]"));

        var result = await svc.GeocodeAsync("Rådhuspladsen 1");

        Assert.NotNull(result);
        Assert.Equal(55.67594, result!.Latitude);
    }

    [Fact]
    public async Task GeocodeAsync_NotFound_On_Both_Endpoints_Is_Null()
    {
        var svc = Create(_ => Json("[]"));
        Assert.Null(await svc.GeocodeAsync("No Such Street 999"));
    }

    [Fact]
    public async Task GeocodeAsync_ServerError_Is_Null_Not_Thrown()
    {
        var svc = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        Assert.Null(await svc.GeocodeAsync("Rådhuspladsen 1"));
    }

    [Fact]
    public async Task GeocodeAsync_Timeout_Is_Null_Not_Thrown()
    {
        var svc = Create(_ => throw new TaskCanceledException("timed out"));
        Assert.Null(await svc.GeocodeAsync("Rådhuspladsen 1"));
    }

    [Fact]
    public async Task GeocodeAsync_Malformed_Payload_Is_Null_Not_Thrown()
    {
        var svc = Create(_ => Json("{ not json ["));
        Assert.Null(await svc.GeocodeAsync("Rådhuspladsen 1"));
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }
}
