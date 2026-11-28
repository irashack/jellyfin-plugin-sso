#nullable disable

using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.SSO_Auth.Api;
using Jellyfin.Plugin.SSO_Auth.Config;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Jellyfin.Plugin.SSO_Auth.Tests;

/// <summary>
/// Runs the filter in a real ASP.NET Core MVC pipeline, registered the way Jellyfin registers it
/// (<see cref="PluginServiceRegistrator"/>), against stand-ins for Jellyfin's UserController actions.
/// The tests in this class share <see cref="PasswordLoginFilter.ConfigurationSource"/>, and xUnit runs
/// a class's tests one at a time.
/// </summary>
public sealed class PasswordLoginFilterTests : IAsyncLifetime
{
    private readonly User _alice = new("alice", "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider", "Jellyfin.Server.Implementations.Users.DefaultPasswordResetProvider");
    private readonly User _carol = new("carol", "Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider", "Jellyfin.Server.Implementations.Users.DefaultPasswordResetProvider");
    private readonly IUserManager _userManager = Substitute.For<IUserManager>();
    private PluginConfiguration _config = new();
    private IHost _host;
    private HttpClient _client;

    public async ValueTask InitializeAsync()
    {
        _userManager.GetUserByName("alice").Returns(_alice);
        _userManager.GetUserByName("carol").Returns(_carol);
        _userManager.GetUserById(_alice.Id).Returns(_alice);
        _userManager.GetUserById(_carol.Id).Returns(_carol);
        PasswordLoginFilter.ConfigurationSource = () => _config;

        _host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_userManager);
                    // The registrator also adds the start-up sweep; with no plugin instance it does nothing.
                    services.AddSingleton(Substitute.For<ICryptoProvider>());
                    services.AddLogging();
                    services.AddControllers().AddApplicationPart(typeof(PasswordLoginFilterTests).Assembly);
                    new PluginServiceRegistrator().RegisterServices(services, null);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .StartAsync(TestContext.Current.CancellationToken);
        _client = _host.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        PasswordLoginFilter.ConfigurationSource = () => SSOPlugin.Instance?.Configuration;
        _client?.Dispose();
        if (_host is not null)
        {
            await _host.StopAsync(CancellationToken.None);
            _host.Dispose();
        }
    }

    private Task<HttpResponseMessage> ByName(string username) =>
        _client.PostAsJsonAsync("/Users/AuthenticateByName", new { Username = username, Pw = "secret" }, TestContext.Current.CancellationToken);

    private void LinkAlice()
    {
        var oid = new OidConfig();
        oid.CanonicalLinks = new SerializableDictionary<string, Guid> { ["alice"] = _alice.Id };
        _config.OidConfigs["example"] = oid;
    }

    [Fact]
    public async Task Off_PasswordSignInReachesJellyfin()
    {
        var response = await ByName("alice");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("signed-in:alice", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task On_PasswordSignInIsRefusedWith403BeforeJellyfin()
    {
        _config.DisablePasswordLogin = true;

        foreach (var name in new[] { "alice", "carol", "nobody" })
        {
            var response = await ByName(name);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(PasswordLoginGuard.RefusedMessage, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task On_ObsoleteAuthenticateByIdIsRefused()
    {
        _config.DisablePasswordLogin = true;

        var response = await _client.PostAsync($"/Users/{_alice.Id}/Authenticate?pw=secret", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task On_ObsoleteAuthenticateByIdOfListedUserIsAllowed()
    {
        _config.DisablePasswordLogin = true;
        _config.PasswordLoginAllowedUsers = new[] { "alice" };

        var response = await _client.PostAsync($"/Users/{_alice.Id}/Authenticate?pw=secret", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("signed-in-by-id:alice", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task On_ListedUserMaySignInWithPassword()
    {
        _config.DisablePasswordLogin = true;
        _config.PasswordLoginAllowedUsers = new[] { "Alice" };
        LinkAlice();

        Assert.Equal(HttpStatusCode.OK, (await ByName("alice")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ByName("carol")).StatusCode);
    }

    [Fact]
    public async Task On_AllowUnlinked_LetsOnlyUnlinkedExistingUsersThrough()
    {
        _config.DisablePasswordLogin = true;
        _config.AllowPasswordLoginForUnlinkedUsers = true;
        LinkAlice();

        Assert.Equal(HttpStatusCode.Forbidden, (await ByName("alice")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ByName("carol")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ByName("nobody")).StatusCode);
    }

    [Fact]
    public async Task On_QuickConnectAndOtherActionsAreUntouched()
    {
        _config.DisablePasswordLogin = true;

        var quickConnect = await _client.PostAsJsonAsync("/Users/AuthenticateWithQuickConnect", new { Secret = "abc" }, TestContext.Current.CancellationToken);
        var other = await _client.GetAsync("/Users/Public", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, quickConnect.StatusCode);
        Assert.Equal("quick-connect", await quickConnect.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task On_EmptyUsernameIsRefused()
    {
        _config.DisablePasswordLogin = true;

        var response = await _client.PostAsJsonAsync("/Users/AuthenticateByName", new { Username = string.Empty, Pw = string.Empty }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task TurningTheSettingOnTakesEffectWithoutARestart()
    {
        Assert.Equal(HttpStatusCode.OK, (await ByName("alice")).StatusCode);

        _config = new PluginConfiguration { DisablePasswordLogin = true };

        Assert.Equal(HttpStatusCode.Forbidden, (await ByName("alice")).StatusCode);
    }
}
