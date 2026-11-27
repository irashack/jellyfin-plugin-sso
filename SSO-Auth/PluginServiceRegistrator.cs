using Jellyfin.Plugin.SSO_Auth.Api;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SSO_Auth;

/// <summary>
/// Registers the plugin's services with Jellyfin's service collection at start-up.
/// </summary>
public sealed class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <summary>
    /// Registers the plugin's services.
    /// </summary>
    /// <param name="serviceCollection">The service collection.</param>
    /// <param name="applicationHost">The server application host.</param>
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        // A global MVC filter; it reads DisablePasswordLogin on every request, so turning the
        // setting on or off takes effect without a restart.
        serviceCollection.Configure<MvcOptions>(options => options.Filters.Add<PasswordLoginFilter>());
    }
}
