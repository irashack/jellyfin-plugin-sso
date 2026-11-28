using Jellyfin.Plugin.SSO_Auth.Api;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SSO_Auth;

/// <summary>
/// Registers the plugin's host-side services with Jellyfin's DI container. Jellyfin discovers
/// implementations in plugin assemblies through a parameterless constructor.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// Registers the plugin's services with the service collection.
    /// </summary>
    /// <param name="serviceCollection">The service collection.</param>
    /// <param name="applicationHost">The server application host.</param>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // Seals SSO-linked accounts without a stored password; the create arm only reaches new ones.
        serviceCollection.AddHostedService<PasswordlessLinkedAccountSweepService>();

        // A global MVC filter; it reads DisablePasswordLogin on every request, so turning the
        // setting on or off takes effect without a restart.
        serviceCollection.Configure<MvcOptions>(options => options.Filters.Add<PasswordLoginFilter>());
    }
}
