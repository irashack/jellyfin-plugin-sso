#nullable disable

using System;
using Jellyfin.Plugin.SSO_Auth.Api;
using Jellyfin.Plugin.SSO_Auth.Config;
using Xunit;

namespace Jellyfin.Plugin.SSO_Auth.Tests;

public class PasswordLoginGuardTests
{
    private static readonly Guid LinkedUser = Guid.NewGuid();
    private static readonly Guid SamlLinkedUser = Guid.NewGuid();
    private static readonly Guid UnlinkedUser = Guid.NewGuid();

    internal static PluginConfiguration Config(bool disable, bool allowUnlinked = false, params string[] allowed)
    {
        var config = new PluginConfiguration
        {
            DisablePasswordLogin = disable,
            AllowPasswordLoginForUnlinkedUsers = allowUnlinked,
            PasswordLoginAllowedUsers = allowed,
        };
        var oid = new OidConfig();
        oid.CanonicalLinks = new SerializableDictionary<string, Guid> { ["alice-sub"] = LinkedUser };
        config.OidConfigs["example"] = oid;
        var saml = new SamlConfig();
        saml.CanonicalLinks = new SerializableDictionary<string, Guid> { ["bob-nameid"] = SamlLinkedUser };
        config.SamlConfigs["corp"] = saml;
        return config;
    }

    [Fact]
    public void DefaultConfiguration_DoesNotRestrict()
    {
        var config = new PluginConfiguration();

        Assert.False(config.DisablePasswordLogin);
        Assert.False(config.AllowPasswordLoginForUnlinkedUsers);
        Assert.Empty(config.PasswordLoginAllowedUsers);
        Assert.Equal(PasswordLoginDecision.NotRestricted, PasswordLoginGuard.Evaluate(config, "alice", LinkedUser));
    }

    [Fact]
    public void NullConfiguration_DoesNotRestrict()
    {
        Assert.Equal(PasswordLoginDecision.NotRestricted, PasswordLoginGuard.Evaluate(null, "alice", LinkedUser));
    }

    [Fact]
    public void Disabled_RefusesLinkedUnlinkedAndUnknownUsers()
    {
        var config = Config(disable: true);

        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, "alice", LinkedUser));
        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, "carol", UnlinkedUser));
        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, "nobody", null));
        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, null, null));
        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, string.Empty, null));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("Admin")]
    public void Disabled_AllowsListedUser_CaseInsensitively(string username)
    {
        var config = Config(true, false, "  Admin ", string.Empty, null);

        Assert.Equal(PasswordLoginDecision.AllowedListedUser, PasswordLoginGuard.Evaluate(config, username, LinkedUser));
    }

    [Theory]
    [InlineData(" admin")]
    [InlineData("admin2")]
    [InlineData("adm")]
    public void Disabled_DoesNotAllowNearMissesOfAListedUser(string username)
    {
        var config = Config(true, false, "admin");

        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, username, null));
    }

    [Fact]
    public void Disabled_ListedUserIsAllowedEvenIfUnknown()
    {
        // Jellyfin answers an unknown user itself; the guard only decides whether to ask it.
        var config = Config(true, false, "breakglass");

        Assert.Equal(PasswordLoginDecision.AllowedListedUser, PasswordLoginGuard.Evaluate(config, "breakglass", null));
    }

    [Fact]
    public void AllowUnlinked_AllowsOnlyExistingUsersNoProviderLinks()
    {
        var config = Config(disable: true, allowUnlinked: true);

        Assert.Equal(PasswordLoginDecision.AllowedUnlinkedUser, PasswordLoginGuard.Evaluate(config, "carol", UnlinkedUser));
        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, "alice", LinkedUser));
        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, "bob", SamlLinkedUser));
        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, "nobody", null));
        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, "empty", Guid.Empty));
    }

    [Fact]
    public void AllowUnlinked_IsIgnoredWhenNotDisabled()
    {
        var config = Config(disable: false, allowUnlinked: true);

        Assert.Equal(PasswordLoginDecision.NotRestricted, PasswordLoginGuard.Evaluate(config, "alice", LinkedUser));
    }

    [Fact]
    public void IsLinked_ChecksOpenIdAndSamlProviders()
    {
        var config = Config(disable: true);
        config.OidConfigs["broken"] = null;

        Assert.True(PasswordLoginGuard.IsLinked(config, LinkedUser));
        Assert.True(PasswordLoginGuard.IsLinked(config, SamlLinkedUser));
        Assert.False(PasswordLoginGuard.IsLinked(config, UnlinkedUser));
    }

    [Fact]
    public void NullAllowedUsers_ListsNobody()
    {
        var config = Config(disable: true);
        config.PasswordLoginAllowedUsers = null;

        Assert.False(PasswordLoginGuard.IsListedUser(config, "admin"));
        Assert.Equal(PasswordLoginDecision.Refused, PasswordLoginGuard.Evaluate(config, "admin", null));
    }

    [Theory]
    [InlineData("Jellyfin.Api.Controllers.UserController", "AuthenticateUserByName", true)]
    [InlineData("Jellyfin.Api.Controllers.UserController", "AuthenticateUser", true)]
    [InlineData("Jellyfin.Api.Controllers.UserController", "AuthenticateWithQuickConnect", false)]
    [InlineData("Jellyfin.Api.Controllers.UserController", "UpdateUserPassword", false)]
    [InlineData("Jellyfin.Api.Controllers.UserController", "authenticateuserbyname", false)]
    [InlineData("Jellyfin.Api.Controllers.QuickConnectController", "AuthenticateUserByName", false)]
    [InlineData("Jellyfin.Plugin.SSO_Auth.Api.SSOController", "OidAuth", false)]
    [InlineData(null, null, false)]
    public void IsPasswordLoginAction_MatchesOnlyThePasswordSignInActions(string controller, string action, bool expected)
    {
        Assert.Equal(expected, PasswordLoginGuard.IsPasswordLoginAction(controller, action));
    }
}
