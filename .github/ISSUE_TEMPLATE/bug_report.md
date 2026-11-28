---
name: Bug report
about: Something in the plugin doesn't work as documented
title: ""
labels: bug
assignees: ""
---

<!-- Security vulnerabilities: please don't file them here. Use private vulnerability reporting: https://github.com/irashack/jellyfin-plugin-sso/security/advisories/new -->

**Describe the bug**
What happened, and what you expected to happen instead.

**To reproduce**
Steps that show the problem:

1. ...
2. ...

**Versions**

- Jellyfin version: [e.g. 12.0.1]
- Plugin version: [e.g. 5.1.1.3003, from Jellyfin's plugin page]
- Provider type: [OpenID Connect or SAML]
- Identity provider: [e.g. Pocket ID, Authelia, Keycloak]
- Client: [e.g. web UI in Firefox, Quick Connect from the Android app]

**Configuration**
The provider's configuration, with secrets (client secret, certificates) removed. You can copy it from the plugin's configuration XML; wrap it in three backticks.

**Logs**
The Jellyfin server log lines from the failed sign-in, with tokens and personal details removed.

**Additional context**
Anything else that helps, such as whether the plugin was built from source.
