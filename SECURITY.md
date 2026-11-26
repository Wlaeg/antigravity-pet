# Security Policy

## Supported Versions

Security updates and bug fixes are actively provided for the latest release series:

| Version | Supported |
| :--- | :--- |
| `4.5.x` | :white_check_mark: Yes |
| `4.0.x – 4.4.x` | :warning: Upgrade to `4.5.x` recommended |
| `< 4.0.0` | :x: No |

---

## Security Architecture Notes

Antigravity Pet runs locally on your machine with the following boundaries:

- **Local Loopback Only**: The native pet's embedded HTTP server binds strictly to `127.0.0.1:47821` (loopback interface) and is never exposed to external network interfaces.
- **Zero Telemetry**: No analytics, tracking, or user data are transmitted externally.
- **Network Requests**: Outbound HTTPS requests are only made when you explicitly check for updates or browse/install packs from the Community Pet Workshop (`api.github.com` / `raw.githubusercontent.com` / `github.com`).
- **Custom Pet Packs**: Imported `.zip` / `.agypet` packs and images are validated locally before extraction and rendering.

---

## Reporting a Vulnerability / 报告安全漏洞

If you discover a security vulnerability in **Antigravity Pet** (for example, arbitrary file write during `.agypet` import, local command execution bypass, or unintended HTTP exposure):

1. **Please do NOT open a public GitHub Issue** with exploit details.
2. Report it privately using **[GitHub Private Vulnerability Reporting](https://github.com/Wlaeg/antigravity-pet/security/advisories/new)** (recommended).
3. Include as much detail as possible:
   - Affected version and OS (Windows / macOS)
   - Steps to reproduce or proof-of-concept
   - Potential impact and any suggested remediation

We will acknowledge receipt within **48 hours** and work to verify and release a fix promptly.
