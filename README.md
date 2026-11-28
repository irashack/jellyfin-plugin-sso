<h1 align="center">Jellyfin SSO Plugin</h1>

<p align="center">

<img alt="Banner" src="img/banner/banner.png"/>
<br/>
<br/>
<a href="https://github.com/irashack/jellyfin-plugin-sso">
<img alt="GPL 3.0 License" src="https://img.shields.io/github/license/irashack/jellyfin-plugin-sso.svg"/>
</a>
<a href="https://github.com/irashack/jellyfin-plugin-sso/actions/workflows/dotnet.yml">
<img alt="GitHub Actions Build Status" src="https://github.com/irashack/jellyfin-plugin-sso/actions/workflows/dotnet.yml/badge.svg"/>
</a>
<a href="https://github.com/irashack/jellyfin-plugin-sso/releases">
<img alt="Current Release" src="https://img.shields.io/github/release/irashack/jellyfin-plugin-sso.svg"/>
</a>
<a href="https://github.com/irashack/jellyfin-plugin-sso/releases.atom">
<img alt="Release RSS Feed" src="https://img.shields.io/badge/rss-releases-ffa500?logo=rss" />
</a>
</p>

This plugin lets users sign in to Jellyfin through an SSO provider (such as Google, Microsoft, or your own OpenID Connect or SAML provider). This enables one-click sign-in.

https://user-images.githubusercontent.com/17993169/149681516-f93b43f5-fa5c-4c1f-a909-e5414878a864.mp4

Existing users may link new SSO accounts, or remove existing links, using self-service at `/SSOViews/linking`.

## About this fork

The plugin was written by [9p4](https://github.com/9p4/jellyfin-plugin-sso), whose repository is archived. [K0lin/jellyfin-plugin-sso](https://github.com/K0lin/jellyfin-plugin-sso) continues it, and this repository, irashack/jellyfin-plugin-sso, is K0lin's 5.1.1 with the changes below. Until 5.0.0.3002 this fork was based on [Buco7854/jellyfin-plugin-sso](https://github.com/Buco7854/jellyfin-plugin-sso) instead; the Buco7854 fixes that K0lin's fork lacks were carried over. Everything not listed here is K0lin's.

This fork's own changes:

1. **`requireLinkedAccount`** (OpenID providers; **Require Linked Account** on the configuration page; default `false`): only an identity already linked to a Jellyfin user can sign in. An unlinked sign-in gets HTTP 403 and a message pointing to `/SSOViews/linking`, instead of a new Jellyfin user or a match by username. See [Account linking](#account-linking).
2. **Live TV permissions are left alone when `enableAuthorization` is off.** K0lin's fork (like 9p4's) writes the provider's Live TV defaults onto the user at every sign-in even with authorization off, overriding Live TV access an administrator set per user. Here, as for every other permission, they are written only with authorization on.
3. **Admin link API**, `POST|DELETE|GET /sso/OID/AdminLink/{provider}/{jellyfinUserId}`, for linking identities to existing users with an administrator API key, plus one lock around every write of provider configurations and links. See [Admin link API](#admin-link-api).
4. **`disablePasswordLogin`** (**Disable password sign-in** on the configuration page; default `false`): refuses Jellyfin's own username and password sign-in at the API, so the server is SSO-only, with a list of break-glass users who may still use a password. Quick Connect and API keys keep working. See [Disabling password sign-in](#disabling-password-sign-in).

Carried over from Buco7854's fork:

- **Account linking runs as a server-side transaction.** The linking page asks the server to start a link for the signed-in user (`POST /sso/OID/StartLink/{provider}` or `/sso/SAML/StartLink/{provider}`); the identity provider's answer completes it once, within 10 minutes, for that user only. The old `?isLinking=true` start and the unauthenticated `linking` relay state are refused. SAML link responses must answer the request that started them.
- **A linked identity is never silently moved** to another Jellyfin user: linking an identity that is already linked to someone else answers 409.
- OpenID requests use Jellyfin's own HTTP client, with clear timeout and connection errors in the log.
- The package keeps the name `SSO-Auth`, the same as the plugin's runtime name, so Jellyfin removes superseded versions on upgrade (K0lin's package is named "SSO Authentication").

Where both bases had a behavior, this fork keeps one:

- **`preserveAdminPermissions`**: both have it with the same meaning and default (`false`): with authorization on, admin status follows the provider's admin roles on every sign-in unless this is on.
- **Accounts without a password**: K0lin's sealing replaces Buco7854's random password at creation. A user the plugin creates gets an unguessable password before anything else (if that fails, the user is deleted again and the sign-in fails); every SSO-linked user that has no stored password is given one at start-up and at sign-in. A password an administrator set is never touched. See [Accounts without a password](#accounts-without-a-password).

And one change of behavior in the shared code: `POST /sso/SAML/Auth/{provider}` now checks the provider's `roles`, as `SAML/post` already did, so a signed SAML response for a user without an allowed role can't be exchanged for a session by posting it there directly.

The plugin's GUID (`505ce9d1-d916-42fa-86ca-673ef241d7df`), name and every configuration key are unchanged, so an installed copy of K0lin's, Buco7854's or 9p4's plugin is replaced in place and keeps its settings and links.

## Current State

This is alpha software. **This version requires Jellyfin 12.0 or later** (.NET 10). The sign-in flow is available in the web UI and in clients that support [Quick Connect](https://jellyfin.org/docs/general/server/quick-connect).

**This README reflects the branch it is on. Switch tags to view version-specific documentation.**

## Reporting bugs and security issues

Report bugs and feature requests in this repository's [issues](https://github.com/irashack/jellyfin-plugin-sso/issues/new/choose), and security vulnerabilities privately through [private vulnerability reporting](https://github.com/irashack/jellyfin-plugin-sso/security/advisories/new), never in a public issue. See [SECURITY.md](SECURITY.md).

## Tested Providers

[Find provider specific documentation in providers.md](providers.md)

- Authelia
- authentik
- Keycloak
  - OIDC & SAML
- Pocket ID
- Kanidm
- Google OpenID: works, but usernames are all numeric

## Supported Protocols

- [OpenID Connect](https://openid.net/developers/how-connect-works/)
- [SAML](https://www.cloudflare.com/learning/access-management/what-is-saml/)

## Installing

Add the package repository [https://raw.githubusercontent.com/irashack/jellyfin-plugin-sso/manifest-release/manifest.json](https://raw.githubusercontent.com/irashack/jellyfin-plugin-sso/manifest-release/manifest.json) to your Jellyfin plugin repositories, then install **SSO-Auth** from the catalog and restart Jellyfin. Each version's zip, with MD5 and SHA-256 checksums, is also on the [releases page](https://github.com/irashack/jellyfin-plugin-sso/releases). This fork publishes formal releases only, no nightly builds.

Versions: `5.1.1.3003` is K0lin's 5.1.1 plus this fork's release number (3001 and 3002 were the releases on Buco7854's 5.0.0.3). Jellyfin sees it as newer than either upstream's 5.1.1 or 5.0.0.x.

### Testing a local build

Install the .NET 10 SDK and `zip`, then run this from the repository root:

```bash
./scripts/build-test-package.sh
```

The script creates `artifacts/jellyfin-plugin-sso_<version>_jellyfin12.zip`, with `<version>` taken from `build.yaml`. To test it:

1. Stop Jellyfin and back up its data directory.
2. Move the currently installed SSO plugin directory out of Jellyfin's `plugins` directory.
3. Create a new directory named `SSO-Auth_<version>` under `plugins` and extract the zip into it.
4. Start Jellyfin and confirm the log contains `Loaded plugin: SSO-Auth <version>`.
5. Test an existing-user login, a new-user login, role mapping, and OIDC/SAML account linking at `/SSOViews/linking`, as applicable to your setup.

Common plugin locations are `/config/plugins` in the official container and `/var/lib/jellyfin/plugins` in Linux package installations. Do not test against your only copy of production data; Jellyfin 12 database migrations are not reversible without a backup.

## Configuration

Open **Dashboard → Plugins → SSO-Auth**. The page is available in English, French and Serbian, chosen from the browser's language. It manages:

- **Disable password sign-in**: the server-wide settings described in [Disabling password sign-in](#disabling-password-sign-in), with their own Save button.
- **OpenID Connect providers**: load, delete, add or update a provider. Every key listed under [OpenID configuration keys](#openid-configuration-keys) except `canonicalLinks` has a field.

SAML providers are configured through the API only (see [SAML](#saml-configuration-keys)). Settings take effect as soon as they are saved; only installing or updating the plugin needs a Jellyfin restart.

The whole configuration can also be read and written with Jellyfin's plugin configuration endpoint, `GET|POST /Plugins/505ce9d1-d916-42fa-86ca-673ef241d7df/Configuration`, which uses the PascalCase names (`DisablePasswordLogin`, `OidConfigs`, …). It is stored in `plugins/configurations/SSO-Auth.xml` under Jellyfin's configuration directory, client secrets and links included.

### Permissions at sign-in

With `enableAuthorization` on, every sign-in through a provider sets the user's administrator flag (from `adminRoles`, see `preserveAdminPermissions`), libraries (`enableAllFolders`, `enabledFolders`, folder roles), Live TV access and management, and, if configured, the content download permission. These are written through Jellyfin's user policy, so they persist across restarts.

With `enableAuthorization` off, the plugin never changes a user's permissions. A user the plugin creates then starts with no libraries (Jellyfin would otherwise give a new user every library), and an administrator grants access in Jellyfin.

### Accounts without a password

A Jellyfin user with no stored password accepts an empty password on the ordinary login form. To keep SSO users from being reachable that way, the plugin gives every SSO-linked user that has no stored password an unguessable password that nothing knows: when it creates a user, when such a user signs in through SSO, and in a sweep of all linked users each time Jellyfin starts. Each sweep that seals anything logs one `[SSO Audit]` warning with a count. A password an administrator set is never changed, and the user's authentication provider is left as it was. To give such a user a password sign-in back, set a password in the dashboard.

## Account linking

A Jellyfin user and an identity at a provider are linked by the identity's link key: for OpenID, the value of the claim named in `defaultUsernameClaim` (`preferred_username` when unset), or of `sub` when the provider doesn't return that claim; for SAML, the `NameID`.

- **Signing in.** Without `requireLinkedAccount`, an identity that isn't linked yet signs in as the Jellyfin user whose username equals its link key, or a new user with that name is created; either way the link is then saved. With `requireLinkedAccount`, only an identity that is already linked signs in; nothing is matched by name or created.
- **Self-service.** A signed-in user opens `/SSOViews/linking`, chooses **Link account** beside a provider and authenticates there; the link is saved when the provider redirects back. The same page lists the user's links and removes selected ones once delete mode is on. Keep another way to sign in before removing a link. To show the page in Jellyfin's menu, use [custom menu links](https://jellyfin.org/docs/general/clients/web-config.html#custom-menu-links).
- **Administrators** can link and unlink OpenID identities with the [admin link API](#admin-link-api), or edit `CanonicalLinks` in the configuration.

## Disabling password sign-in

With **Disable Password Sign-in** on (`disablePasswordLogin`; default `false`), Jellyfin's username and password sign-in is refused, so users can only sign in through this plugin's providers or Quick Connect. It is enforced on the server, not by hiding the login form: the plugin registers an MVC filter that answers Jellyfin's two password sign-in routes, `POST /Users/AuthenticateByName` and the obsolete `POST /Users/{userId}/Authenticate`, with HTTP 403 and a short message before Jellyfin checks the password. Refused attempts are logged as warnings with the username and client address, and they don't count toward Jellyfin's per-user lockout, because Jellyfin never sees them. The setting is read on every request, so saving it takes effect without a restart.

Settings (configuration page, **Disable password sign-in**, saved with their own button):

- `disablePasswordLogin`: boolean, default `false`. Off, the plugin changes nothing about password sign-in.
- `passwordLoginAllowedUsers`: list of Jellyfin usernames, default empty. These users may still sign in with a password: the break-glass path. Compared case-insensitively, as Jellyfin compares usernames. Keep at least one administrator here; the page asks for confirmation when you disable password sign-in with an empty list.
- `allowPasswordLoginForUnlinkedUsers`: boolean, default `false`. Also lets an existing user that no OpenID or SAML provider links to sign in with a password, for example local accounts for people without an identity at your provider. Users created by SSO sign-in are always linked, so they stay SSO-only.

What keeps working, and what doesn't:

- **SSO sign-in** through this plugin (web UI, and apps that show the web UI) is unaffected: it starts sessions without a password.
- **Quick Connect** is unaffected. It is how TV and phone apps whose login screen only has a username and password form sign in: the app shows a code, and a user who is already signed in (through SSO in a browser) approves it. Quick Connect must be enabled in Jellyfin's dashboard (General → Quick Connect); turn it off there if you don't want it.
- **API keys** (`Authorization: MediaBrowser Token="…"` or `?ApiKey=…`) are unaffected; they never use the password routes. So are **sessions that already exist**: turning the setting on doesn't sign anyone out. To end sessions that were started with a password, sign those devices out in the dashboard (Devices).
- **Apps signing in with a password** get HTTP 403. Jellyfin's web UI shows its "not authorized" message; other apps show a generic sign-in error. Services that sign in as a user with a password need an API key instead, or their user on `passwordLoginAllowedUsers`.
- **Password changes and resets** are not blocked, but a new password doesn't let a refused user sign in.
- **Other authentication providers** (for example the LDAP plugin) are reached through the same password routes, so they are refused too, except for listed and (with `allowPasswordLoginForUnlinkedUsers`) unlinked users.

Which status a client gets tells it something: a listed user (or an unlinked one, with that option) gets Jellyfin's usual 401 for a wrong password, where everyone else gets 403. Someone probing the server can therefore learn which usernames are on the break-glass list, but still needs their passwords.

Locked out? An administrator API key can still change the setting (`POST /Plugins/505ce9d1-d916-42fa-86ca-673ef241d7df/Configuration` with the whole configuration). Or stop Jellyfin, set `<DisablePasswordLogin>false</DisablePasswordLogin>` in `plugins/configurations/SSO-Auth.xml` under Jellyfin's configuration directory, and start it again.

You may also want to hide the password form in the web UI, with the [login button](#creating-a-login-button-on-the-main-page) in its place. Hiding it is cosmetic; the setting above is what refuses the sign-in.

## Examples

### Creating A Login Button On The Main Page

In the Jellyfin administration UI, under "General", there is a "Branding" section. In that section, add the following code in the "Login disclaimer" block (replacing `PROVIDER_NAME`):

```html
<form action="/sso/OID/start/PROVIDER_NAME">
  <button class="raised block emby-button button-submit">
    Sign in with SSO
  </button>
</form>
```

Then, add the following code in the "Custom CSS code" section:

```css
a.raised.emby-button {
  padding: 0.9em 1em;
  color: inherit !important;
}

.disclaimerContainer {
  display: block;
}
```

![screenshot of the configuration page with the same code](img/custom-button.png)

For more information, refer to [issue #16](https://github.com/9p4/jellyfin-plugin-sso/issues/16).

### SAML

Example for adding a SAML configuration with the API using [curl](https://curl.se/):

`curl -v -X POST -H "Content-Type: application/json" -d '{"samlEndpoint": "https://keycloak.example.com/realms/test/protocol/saml", "samlClientId": "jellyfin-saml", "samlCertificate": "Very long base64 encoded string here", "enabled": true, "enableAuthorization": true, "enableAllFolders": false, "enabledFolders": [], "adminRoles": ["jellyfin-admin"], "roles": ["allowed-to-use-jellyfin"], "enableFolderRoles": true, "folderRoleMapping": [{"role": "allowed-to-watch-movies", "folders": ["cc7df17e2f3509a4b5fc1d1ff0a6c4d0", "f137a2dd21bbc1b99aa5c0f6bf02a805"]}]}' "https://myjellyfin.example.com/sso/SAML/Add/PROVIDER_NAME?ApiKey=API_KEY_HERE"`

The posted JSON replaces the provider's whole configuration, so send every setting you want.

The SAML provider must have the following configuration (shown for Keycloak):

- Sign Documents on
- Sign Assertions off
- Client Signature Required off
- Redirect URI: `https://myjellyfin.example.com/sso/SAML/post/PROVIDER_NAME`
- Base URL: `https://myjellyfin.example.com`
- Master SAML processing URL: `https://myjellyfin.example.com/sso/SAML/start/PROVIDER_NAME`

Replace `PROVIDER_NAME` with the chosen provider name.

### OpenID

Example for adding an OpenID configuration with the API using [curl](https://curl.se/):

`curl -v -X POST -H "Content-Type: application/json" -d '{"oidEndpoint": "https://keycloak.example.com/realms/test", "oidClientId": "jellyfin-oid", "oidSecret": "short secret here", "enabled": true, "enableAuthorization": true, "enableAllFolders": false, "enabledFolders": [], "adminRoles": ["jellyfin-admin"], "roles": ["allowed-to-use-jellyfin"], "enableFolderRoles": true, "folderRoleMapping": [{"role": "allowed-to-watch-movies", "folders": ["cc7df17e2f3509a4b5fc1d1ff0a6c4d0", "f137a2dd21bbc1b99aa5c0f6bf02a805"]}], "roleClaim": "realm_access.roles", "oidScopes": []}' "https://myjellyfin.example.com/sso/OID/Add/PROVIDER_NAME?ApiKey=API_KEY_HERE"`

The OpenID provider must have the following configuration (shown for Keycloak):

- Access Type: Confidential
- Standard Flow Enabled
- Redirect URI: `https://myjellyfin.example.com/sso/OID/redirect/PROVIDER_NAME`
- Base URL: `https://myjellyfin.example.com`

Replace `PROVIDER_NAME` with the chosen provider name.

## API Endpoints

The API is all done from a base URL of `/sso/`. JSON keys are case-insensitive; this README writes them in camelCase. Routes marked _admin_ need an administrator session or API key (`Authorization: MediaBrowser Token="API_KEY"` or `?ApiKey=API_KEY`); _signed in_ means any Jellyfin session, acting on its own user unless it is an administrator's.

### SAML

#### Flow

- GET `SAML/start/PROVIDER_NAME`: starts sign-in with the provider (redirects to it). `SAML/p/PROVIDER_NAME` is the old path for the same thing.
- POST `SAML/post/PROVIDER_NAME` (old path `SAML/p/PROVIDER_NAME`): the assertion consumer service. It accepts the provider's form post and returns HTML and JavaScript that complete the sign-in, or completes a link started by `SAML/StartLink`.
- POST `SAML/Auth/PROVIDER_NAME`: called by that page to receive Jellyfin credentials. JSON body:
  - `deviceId`, `deviceName`, `appName`, `appVersion`: strings describing the client.
  - `data`: string. The signed SAML response. Its signature and the provider's `roles` are checked.
- POST `SAML/StartLink/PROVIDER_NAME` (_signed in_): starts linking a SAML identity to the signed-in user and answers the provider URL to open, as text.

#### SAML configuration keys

- POST `SAML/Add/PROVIDER_NAME` (_admin_): adds or replaces the configuration of a SAML provider. JSON body (every key optional; booleans default to `false`, lists to empty, strings to unset):
  - `samlEndpoint`: string. The SAML endpoint.
  - `samlClientId`: string. The SAML client ID.
  - `samlCertificate`: string. The base64 encoded SAML certificate.
  - `enabled`: boolean. Whether the provider is enabled.
  - `enableAuthorization`: boolean. Whether the plugin sets the user's permissions at every sign-in. See [Permissions at sign-in](#permissions-at-sign-in).
  - `enableAllFolders`: boolean. Gives users access to all libraries.
  - `enabledFolders`: array of strings. Library IDs users get when `enableAllFolders` is false and `enableFolderRoles` is off. To get the IDs, GET `/Library/MediaFolders` with an API key and read each `Id`.
  - `enableContentDownloading`: boolean or `null`, default `null`. Allows (`true`) or forbids (`false`) content downloads; `null` leaves the permission as it is. Applied only when `enableAuthorization` is on.
  - `applyContentDownloadPermissionOnEveryLogin`: boolean. With `false`, `enableContentDownloading` is applied only to users the plugin creates; with `true`, at every sign-in.
  - `roles`: array of strings. Checked against the response's `Role` attributes; a user with any of them may sign in. Empty allows everyone.
  - `adminRoles`: array of strings. A user with any of these `Role` attributes becomes an administrator. Empty grants no one admin.
  - `preserveAdminPermissions`: boolean. When true, sign-in only ever grants administrator from `adminRoles` and never revokes it from a user who has it. With `false`, admin status follows `adminRoles` on every sign-in.
  - `enableFolderRoles`: boolean. Use `folderRoleMapping` for library access instead of `enabledFolders`.
  - `folderRoleMapping`: array of objects `{"role": string, "folders": array of library IDs}`. A user with the role gets those libraries when `enableFolderRoles` is on.
  - `enableLiveTvRoles`: boolean. Use `liveTvRoles` and `liveTvManagementRoles`.
  - `liveTvRoles`: array of strings. Roles that grant Live TV access when `enableLiveTvRoles` is on.
  - `liveTvManagementRoles`: array of strings. Roles that grant Live TV management when `enableLiveTvRoles` is on.
  - `enableLiveTv`: boolean. Live TV access by default, also when `enableLiveTvRoles` is on.
  - `enableLiveTvManagement`: boolean. Live TV management by default, also when `enableLiveTvRoles` is on.
  - `defaultProvider`: string. Authentication provider assigned to the user after an SSO sign-in, for example `Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider` so the user can also sign in with a password later. Unset leaves it as it is. See `Unregister`.
  - `schemeOverride`: string, `http` or `https`. Scheme for the URLs the plugin generates, if it picks the wrong one behind a proxy.
  - `portOverride`: integer. Port for the URLs the plugin generates.
  - `canonicalLinks`: object mapping link keys (`NameID`s) to Jellyfin user IDs. Maintained by linking; a posted configuration replaces it.

  All Live TV settings are applied only when `enableAuthorization` is on.

- GET `SAML/Del/PROVIDER_NAME` (_admin_): removes a SAML provider.
- GET `SAML/Get` (_admin_): lists the SAML configurations.
- GET `SAML/GetNames`: lists the SAML provider names.

### OpenID

#### Flow

- GET `OID/start/PROVIDER_NAME`: starts sign-in with the provider (redirects to it). `OID/p/PROVIDER_NAME` is the old path for the same thing.
- GET `OID/redirect/PROVIDER_NAME` (old path `OID/r/PROVIDER_NAME`): the OpenID callback. It returns HTML and JavaScript that complete the sign-in, or completes a link started by `OID/StartLink`.
- POST `OID/Auth/PROVIDER_NAME`: called by that page to receive Jellyfin credentials. JSON body:
  - `deviceId`, `deviceName`, `appName`, `appVersion`: strings describing the client.
  - `data`: string. The OpenID state. It is single-use and expires after 10 minutes.
- POST `OID/StartLink/PROVIDER_NAME` (_signed in_): starts linking an OpenID identity to the signed-in user and answers the provider URL to open, as text.

#### OpenID configuration keys

- POST `OID/Add/PROVIDER_NAME` (_admin_): adds or replaces the configuration of an OpenID provider. JSON body (every key optional; booleans default to `false`, lists to empty, strings to unset):
  - `oidEndpoint`: string. The provider's issuer URL; it must serve `.well-known/openid-configuration`.
  - `oidClientId`: string. The OpenID client ID.
  - `oidSecret`: string. The OpenID client secret.
  - `enabled`: boolean. Whether the provider is enabled.
  - `enableAuthorization`: boolean. Whether the plugin sets the user's permissions at every sign-in. See [Permissions at sign-in](#permissions-at-sign-in).
  - `enableAllFolders`: boolean. Gives users access to all libraries.
  - `enabledFolders`: array of strings. Library IDs users get when `enableAllFolders` is false and `enableFolderRoles` is off.
  - `enableContentDownloading`: boolean or `null`, default `null`. Allows (`true`) or forbids (`false`) content downloads; `null` leaves the permission as it is. Applied only when `enableAuthorization` is on.
  - `applyContentDownloadPermissionOnEveryLogin`: boolean. With `false`, `enableContentDownloading` is applied only to users the plugin creates; with `true`, at every sign-in.
  - `roles`: array of strings. Checked against the claim named in `roleClaim`; a user with any of them may sign in. Empty allows everyone.
  - `adminRoles`: array of strings. A user with any of these roles becomes an administrator. Empty grants no one admin.
  - `preserveAdminPermissions`: boolean. When true, sign-in only ever grants administrator from `adminRoles` and never revokes it from a user who has it. With `false`, admin status follows `adminRoles` on every sign-in.
  - `enableFolderRoles`: boolean. Use `folderRoleMapping` for library access instead of `enabledFolders`.
  - `folderRoleMapping`: array of objects `{"role": string, "folders": array of library IDs}`.
  - `enableLiveTvRoles`, `liveTvRoles`, `liveTvManagementRoles`, `enableLiveTv`, `enableLiveTvManagement`: as for SAML, applied only when `enableAuthorization` is on.
  - `roleClaim`: string. The claim holding the roles. The first segment is the claim type; further segments walk into its JSON value, for example `realm_access.roles` for Keycloak. Write `\.` for a literal dot. The value must be a string or a list of strings. Role matching is exact and case-sensitive.
  - `oidScopes`: array of strings. Scopes to request in addition to `openid profile`. Some providers need an extra scope (for example `groups`) to send the role claim.
  - `overrideDefaultScopes`: boolean. Request `openid` plus `oidScopes` only, without `profile`.
  - `oidAuthorizationParameters`: string holding a JSON object of extra parameters for the authorization request, for example `{"prompt": "login"}` or a `claims` object. Values must be strings, numbers or booleans (`claims` must be an object); at most 16 entries. Parameters the flow sets itself (`client_id`, `redirect_uri`, `response_type`, `scope`, `state`, `nonce`, `code_challenge`, `code_challenge_method`, `resource`, `request`, `request_uri`) can't be overridden. An invalid value makes sign-in with this provider answer 400 and logs why.
  - `defaultUsernameClaim`: string. The claim used as the username of users the plugin creates, and as the link key. Default `preferred_username`; `sub` is used when the claim is missing. Set it to `sub` for links that survive a username change at the provider.
  - `requireLinkedAccount`: boolean. When true, only an identity already linked to a Jellyfin user can sign in; an unlinked sign-in is refused with HTTP 403 and a plain-text message pointing to `/SSOViews/linking`, and no user is created or matched by name. Linking itself is unaffected. See [Account linking](#account-linking).
  - `defaultProvider`: string. Authentication provider assigned to the user after an SSO sign-in. Unset leaves it as it is. See `Unregister`.
  - `avatarUrlFormat`: string. URL of the user's avatar, with claims written as `@{claim_type}`. Unset leaves avatars alone.
  - `disableHttps`: boolean (insecure). Allow a discovery endpoint without HTTPS.
  - `disablePushedAuthorization`: boolean (insecure). Don't use pushed authorization requests; may be needed for Authelia.
  - `doNotValidateEndpoints`: boolean (insecure). Don't require the provider's endpoints to be on the issuer's host; may be needed for Google.
  - `doNotValidateIssuerName`: boolean (insecure). Don't check the issuer name in discovery.
  - `doNotLoadProfile`: boolean. Don't call the UserInfo endpoint; may be needed for Cloudflare.
  - `schemeOverride`: string, `http` or `https`. Scheme for the URLs the plugin generates.
  - `portOverride`: integer. Port for the URLs the plugin generates.
  - `canonicalLinks`: object mapping link keys to Jellyfin user IDs. Maintained by linking; a posted configuration replaces it.
- GET `OID/Del/PROVIDER_NAME` (_admin_): removes an OpenID provider.
- GET `OID/Get` (_admin_): lists the OpenID configurations.
- GET `OID/GetNames`: lists the OpenID provider names.
- GET `OID/States` (_admin_): lists OpenID flows in progress.

### Links

- GET `oid/links/JELLYFIN_USER_ID` and `saml/links/JELLYFIN_USER_ID` (_signed in_): the user's link keys per provider.
- DELETE `MODE/Link/PROVIDER_NAME/JELLYFIN_USER_ID/LINK_KEY` (_signed in_; `MODE` is `oid` or `saml`): removes one link of the user.
- POST `MODE/Link/PROVIDER_NAME/JELLYFIN_USER_ID` (_signed in_): links the identity of a completed sign-in (`data` as for `Auth`) to the user. The linking page uses `StartLink` instead.

### Admin link API

For automation (for example an onboarding service) that holds an administrator API key: links an OpenID identity to an existing Jellyfin user without that user signing in first. The self-service routes can't do this, because they act on the signed-in session's user. All three routes are _admin_ (401 without credentials, 403 for a non-administrator), apply to OpenID providers only, and answer 404 when `PROVIDER_NAME` is not a configured OpenID provider. Responses never contain a link key, a client secret or any other configuration, and their log lines name only the Jellyfin user ID and the outcome.

The body field is called `sub` because setting `defaultUsernameClaim` to `sub` is the usual choice for stable links; with any other claim, send that claim's value (see [Account linking](#account-linking)).

- POST `OID/AdminLink/PROVIDER_NAME/JELLYFIN_USER_ID`: links a key to the user. Send `Content-Type: application/json` (415 otherwise) and the body `{"sub": "<link key>"}`. Status codes:
  - 204: linked.
  - 400: `sub` missing, empty, whitespace only, or longer than 255 characters, or the body is not valid JSON.
  - 403: the Jellyfin user is an administrator.
  - 404: unknown provider or unknown Jellyfin user.
  - 409: that key is already linked in this provider, to any user, including this one. The route is deliberately not idempotent.
- DELETE `OID/AdminLink/PROVIDER_NAME/JELLYFIN_USER_ID`: removes every link in the provider whose target is that user (for offboarding). Answers 200 with `{"removed": <count>}`, where the count is 0 when there was nothing to remove; 403 when the user is an administrator. The Jellyfin user need not still exist, so links to a deleted user can be cleaned up.
- GET `OID/AdminLink/PROVIDER_NAME/JELLYFIN_USER_ID`: answers 200 with `{"linked": true}` or `{"linked": false}` and nothing else.

Locking: one process-wide lock covers every read-check-modify-save of provider configurations and links done by this plugin: these three routes, the self-service link and unlink routes, the link written at sign-in and when a user is created, the OID/SAML `Add` and `Del` routes, and the start-up sweep's read of the links. Link writers save a modified copy of the links rather than editing the live dictionary, so a concurrent sign-in never reads a half-updated one. Two writers stay outside it: `OID/Add` replaces a whole provider configuration, links included, with the posted body, and saving the configuration page (or posting to `/Plugins/…/Configuration`) writes back the whole configuration it loaded, which can undo a link written in between.

### Misc

- POST `Unregister/USERNAME` (_admin_): "unregisters" a user from SSO by setting their authentication provider. Post the provider as a JSON string; to return to Jellyfin's password provider: `curl -X POST -H "Content-Type: application/json" -d '"Jellyfin.Server.Implementations.Users.DefaultAuthenticationProvider"' "https://myjellyfin.example.com/sso/Unregister/USERNAME?ApiKey=API_KEY"`. Answers 404 for an unknown user.

## Limitations

Without `requireLinkedAccount`, signing in with an SSO identity whose link key equals the username of an existing Jellyfin user signs in as that user, and with `enableAuthorization` on, overwrites that user's permissions. Use caution with administrator accounts, and keep a second administrator account.

With `enableAuthorization` on, a sign-in that matches no `adminRoles` entry revokes admin unless `preserveAdminPermissions` is on. Check your role mapping before signing in with an administrator account.

There is no logout callback: signing out of Jellyfin doesn't sign you out of the SSO provider.

Sign-in through SSO works in the web UI and in apps that show it. Other apps sign in with Quick Connect, approved from a browser session.

# Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). This fork has GitHub issues turned off; questions and bugs about the code it shares with K0lin's fork belong in [K0lin's tracker](https://github.com/K0lin/jellyfin-plugin-sso/issues).

## Dependencies

This project uses Nix flakes to manage development dependencies. Run `nix develop` to use the same toolchain versions. `npm ci` installs Prettier, which formats the JavaScript, HTML, CSS and Markdown (`npm run format:check`).

## Building and testing

This is built with .NET 10. `dotnet build` builds the plugin and `dotnet test` runs the tests in `SSO-Auth.Tests` (xunit v3): role evaluation, scopes and authorization parameters, content downloads, password sealing, the password sign-in filter in an ASP.NET Core test server, and translation completeness. `./scripts/build-test-package.sh` builds an installable zip, see [Testing a local build](#testing-a-local-build), and `./scripts/validate-release-metadata.sh` checks that the version, name and GUID agree across `build.yaml`, the project, the plugin class and `apiClient.js`.

Translations live in `SSO-Auth/Config/i18n`; `en-us.json` is the reference, and every other locale must have all of its keys.

### VSCode Workflow

An example `.vscode` configuration may be found at [strazto/jellyfin-plugin-sso-vscode](https://github.com/strazto/jellyfin-plugin-sso-vscode).

## Releasing

Releases are built with [JPRM](https://github.com/oddstr13/jellyfin-plugin-repository-manager) by CI. To release, bump the version in `build.yaml`, `SSO-Auth/SSO-Auth.csproj` and `SSO-Auth/Views/apiClient.js` (the validation script checks they agree), add a `changelog` entry in `build.yaml`, merge to `main`, and publish a GitHub release named `v<version>` on `main`. The release runs **Publish Release**, which attaches the zip with its MD5 and SHA-256 and regenerates `manifest.json` on the `manifest-release` branch from all releases. Nightly builds are not published.

To publish releases from your own fork, change the repository URLs in `build.yaml` and the manifest URL under [Installing](#installing).

## Credits and Thanks

[9p4](https://github.com/9p4) wrote this plugin. [K0lin](https://github.com/K0lin) maintains the fork this one is based on, including its redesigned UI, translations, tests and security fixes, and [Buco7854](https://github.com/Buco7854) the fork this one was based on before, including the secure account-linking flow. Thanks to [tradicije](https://github.com/tradicije) for designing the branding, and to the translators of the French and Serbian locales. The plugin is licensed under the GPL 3.0, like its upstreams.

Much thanks to the [Jellyfin LDAP plugin](https://github.com/jellyfin/jellyfin-plugin-ldapauth) for offering a base for the plugin.

The SAML side uses the [AspNet SAML](https://github.com/jitbit/AspNetSaml/) library (patched to work with Base64 on non-Windows machines), and the OpenID side the [Duende IdentityModel OIDC Client](https://github.com/DuendeSoftware/foss) library.

## Something funny about the origins of this plugin

9p4 had [requested this functionality a few years back](https://github.com/jellyfin/jellyfin/issues/2012). What goes around comes around.
