namespace Jellyfin.Plugin.SSO_Auth.Api;

/// <summary>
/// The one lock around every read-check-modify-save of the plugin configuration that touches
/// provider configurations or canonical links. Monitor locks are reentrant, so code that already
/// holds it may call helpers that take it again. Never hold it across an await.
/// </summary>
internal static class PluginConfigurationLock
{
    /// <summary>
    /// Gets the object to lock on.
    /// </summary>
    internal static object Sync { get; } = new();
}
