using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.SSO_Auth.Config;
using MediaBrowser.Controller.Library;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.SSO_Auth.Api;

/// <summary>
/// The outcome of checking a password sign-in against <see cref="PluginConfiguration.DisablePasswordLogin"/>.
/// </summary>
public enum PasswordLoginDecision
{
    /// <summary>
    /// Password sign-in is not disabled; Jellyfin handles the request as usual.
    /// </summary>
    NotRestricted,

    /// <summary>
    /// The username is on <see cref="PluginConfiguration.PasswordLoginAllowedUsers"/>.
    /// </summary>
    AllowedListedUser,

    /// <summary>
    /// The user exists, no SSO provider links to it, and
    /// <see cref="PluginConfiguration.AllowPasswordLoginForUnlinkedUsers"/> is on.
    /// </summary>
    AllowedUnlinkedUser,

    /// <summary>
    /// The request is refused before Jellyfin checks the password.
    /// </summary>
    Refused,
}

/// <summary>
/// Decides whether a username and password sign-in may reach Jellyfin.
/// </summary>
public static class PasswordLoginGuard
{
    /// <summary>
    /// The message returned with HTTP 403 when a password sign-in is refused.
    /// </summary>
    public const string RefusedMessage =
        "Password sign-in is disabled on this server. Sign in with single sign-on, or with Quick Connect from an app.";

    /// <summary>
    /// The full name of Jellyfin's controller that holds the password sign-in actions.
    /// </summary>
    public const string UserControllerTypeName = "Jellyfin.Api.Controllers.UserController";

    /// <summary>
    /// Returns whether an MVC action is one of Jellyfin's password sign-in actions:
    /// <c>AuthenticateUserByName</c> (POST /Users/AuthenticateByName) or the obsolete
    /// <c>AuthenticateUser</c> (POST /Users/{userId}/Authenticate).
    /// </summary>
    /// <param name="controllerTypeName">The controller type's full name.</param>
    /// <param name="actionName">The action name.</param>
    /// <returns>Whether the action signs a user in with a password.</returns>
    public static bool IsPasswordLoginAction(string controllerTypeName, string actionName)
    {
        return string.Equals(controllerTypeName, UserControllerTypeName, StringComparison.Ordinal)
            && (string.Equals(actionName, "AuthenticateUserByName", StringComparison.Ordinal)
                || string.Equals(actionName, "AuthenticateUser", StringComparison.Ordinal));
    }

    /// <summary>
    /// Decides whether a password sign-in may go ahead.
    /// </summary>
    /// <param name="config">The plugin configuration; null allows the sign-in.</param>
    /// <param name="username">The username as sent by the client; may be null.</param>
    /// <param name="userId">The id of the Jellyfin user with that name, or null if there is none.</param>
    /// <returns>The decision.</returns>
    public static PasswordLoginDecision Evaluate(PluginConfiguration config, string username, Guid? userId)
    {
        if (config is null || !config.DisablePasswordLogin)
        {
            return PasswordLoginDecision.NotRestricted;
        }

        if (IsListedUser(config, username))
        {
            return PasswordLoginDecision.AllowedListedUser;
        }

        if (config.AllowPasswordLoginForUnlinkedUsers
            && userId is Guid id
            && id != Guid.Empty
            && !IsLinked(config, id))
        {
            return PasswordLoginDecision.AllowedUnlinkedUser;
        }

        return PasswordLoginDecision.Refused;
    }

    /// <summary>
    /// Returns whether a username is on <see cref="PluginConfiguration.PasswordLoginAllowedUsers"/>,
    /// compared the way Jellyfin looks users up by name (upper-cased, invariant culture).
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="username">The username as sent by the client.</param>
    /// <returns>Whether the username is listed.</returns>
    public static bool IsListedUser(PluginConfiguration config, string username)
    {
        if (config?.PasswordLoginAllowedUsers is null || string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var normalized = username.ToUpperInvariant();
        return config.PasswordLoginAllowedUsers
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .Any(entry => string.Equals(entry.Trim().ToUpperInvariant(), normalized, StringComparison.Ordinal));
    }

    /// <summary>
    /// Returns whether any OpenID or SAML provider links an identity to a Jellyfin user.
    /// </summary>
    /// <param name="config">The plugin configuration.</param>
    /// <param name="userId">The Jellyfin user id.</param>
    /// <returns>Whether the user is linked.</returns>
    public static bool IsLinked(PluginConfiguration config, Guid userId)
    {
        var oidLinked = config.OidConfigs?.Values
            .Where(provider => provider is not null)
            .Any(provider => provider.CanonicalLinks.Values.Contains(userId)) ?? false;
        var samlLinked = config.SamlConfigs?.Values
            .Where(provider => provider is not null)
            .Any(provider => provider.CanonicalLinks.Values.Contains(userId)) ?? false;
        return oidLinked || samlLinked;
    }
}

/// <summary>
/// Refuses Jellyfin's username and password sign-in while
/// <see cref="PluginConfiguration.DisablePasswordLogin"/> is on, before the password is checked.
/// Registered for every MVC action by <see cref="PluginServiceRegistrator"/>; it does nothing
/// for any action other than the two password sign-in actions.
/// </summary>
public sealed class PasswordLoginFilter : IAsyncActionFilter
{
    private readonly IUserManager _userManager;
    private readonly ILogger<PasswordLoginFilter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordLoginFilter"/> class.
    /// </summary>
    /// <param name="userManager">Instance of the <see cref="IUserManager"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{PasswordLoginFilter}"/> interface.</param>
    public PasswordLoginFilter(IUserManager userManager, ILogger<PasswordLoginFilter> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets or sets where the filter reads the plugin configuration from. Tests replace it.
    /// </summary>
    internal static Func<PluginConfiguration> ConfigurationSource { get; set; } =
        () => SSOPlugin.Instance?.Configuration;

    /// <inheritdoc />
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var config = ConfigurationSource();
        if (config is null
            || !config.DisablePasswordLogin
            || context.ActionDescriptor is not ControllerActionDescriptor action
            || !PasswordLoginGuard.IsPasswordLoginAction(action.ControllerTypeInfo.FullName, action.ActionName))
        {
            await next().ConfigureAwait(false);
            return;
        }

        var user = ResolveUser(context, out var username);
        var decision = PasswordLoginGuard.Evaluate(config, username, user?.Id);
        var remoteIp = context.HttpContext.Connection.RemoteIpAddress?.ToString();

        switch (decision)
        {
            case PasswordLoginDecision.AllowedListedUser:
                _logger.LogInformation(
                    "Password sign-in for {Username} from {RemoteIp} allowed: the user is on PasswordLoginAllowedUsers.",
                    username,
                    remoteIp);
                break;
            case PasswordLoginDecision.AllowedUnlinkedUser:
                _logger.LogInformation(
                    "Password sign-in for {Username} from {RemoteIp} allowed: no SSO provider links the user.",
                    username,
                    remoteIp);
                break;
            case PasswordLoginDecision.Refused:
                _logger.LogWarning(
                    "Password sign-in for {Username} from {RemoteIp} refused: DisablePasswordLogin is on.",
                    username,
                    remoteIp);
                context.Result = new ContentResult
                {
                    StatusCode = StatusCodes.Status403Forbidden,
                    Content = PasswordLoginGuard.RefusedMessage,
                    ContentType = "text/plain; charset=utf-8",
                };
                return;
        }

        await next().ConfigureAwait(false);
    }

    private User ResolveUser(ActionExecutingContext context, out string username)
    {
        username = null;

        // AuthenticateUserByName binds its body to "request" (Jellyfin.Api's AuthenticateUserByName,
        // which this plugin doesn't reference, so its Username is read by name).
        if (context.ActionArguments.TryGetValue("request", out var request) && request is not null)
        {
            username = request.GetType().GetProperty("Username")?.GetValue(request) as string;
            return string.IsNullOrWhiteSpace(username) ? null : _userManager.GetUserByName(username);
        }

        // The obsolete AuthenticateUser takes the user id from the route.
        if (context.ActionArguments.TryGetValue("userId", out var routeId) && routeId is Guid userId && userId != Guid.Empty)
        {
            var user = _userManager.GetUserById(userId);
            username = user?.Username;
            return user;
        }

        return null;
    }
}
