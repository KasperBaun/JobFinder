using Microsoft.AspNetCore.Builder;

namespace Jobmatch.Api.Infrastructure;

public interface IEndpointRegistration
{
    void Register(WebApplication app);
}
