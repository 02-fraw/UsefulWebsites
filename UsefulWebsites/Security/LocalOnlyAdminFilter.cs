using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace UsefulWebsites.Security;

public sealed class LocalOnlyAdminFilter : IAsyncAuthorizationFilter
{
    private readonly IWebHostEnvironment _environment;

    public LocalOnlyAdminFilter(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        IPAddress? remoteIpAddress = context.HttpContext.Connection.RemoteIpAddress;

        bool isLocalRequest =
            remoteIpAddress is not null &&
            IPAddress.IsLoopback(remoteIpAddress);

        if (!_environment.IsDevelopment() || !isLocalRequest)
        {
            context.Result = new NotFoundResult();
        }

        return Task.CompletedTask;
    }
}
