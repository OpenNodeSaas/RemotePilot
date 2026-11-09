# RemotePilot

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

RemotePilot is a free, portable, offline-first remote operations client for Windows built on WPF (.NET).

- **Native RDP**: Generates `.rdp` files on the fly and connects through the system-provided `mstsc.exe` — no third-party proxy involved.
- **Built-in SSH terminal**: Powered by SSH.NET and VirtualTerminal.WPF, with SSH jump host support.
- **Asset management**: Grouping, search, and expiry reminders; supports bulk import from CSV / Excel.
- **Connection templates**: Reusable RDP parameter templates with quick resolution and split-screen (side-by-side / stacked) presets.
- **Offline-first**: All data stays local — no account, no network required.

## Requirements

- Windows 10 / 11 (x64)
- Release builds are self-contained and require no separate .NET runtime; unzip and run `RemotePilot.exe` directly.

## Build

Requires [.NET SDK 10](https://dotnet.microsoft.com/download).

```powershell
dotnet build src\RemotePilot\RemotePilot.csproj -c Release
dotnet test tests\RemotePilot.Tests
```

## Data & Security

- Asset data is stored in `%LOCALAPPDATA%\FastRdp\RemoteOps\assets.json`; to back up or migrate, just copy this file.
- Passwords are not saved by default. They are only stored when you enable "Remember password", and they are encrypted with Windows DPAPI (current user), meaning they can only be decrypted under the same Windows user.
- For backward compatibility, the data directory remains `%LOCALAPPDATA%\FastRdp\RemoteOps\` regardless of the product name.

## Third-Party Dependencies

| Component | License |
| --- | --- |
| [ExcelDataReader](https://github.com/ExcelDataReader/ExcelDataReader) | MIT |
| [SSH.NET](https://github.com/sshnet/SSH.NET) | MIT |
| [BouncyCastle.Cryptography](https://www.bouncycastle.org/) | MIT |
| Microsoft.Extensions.Logging.Abstractions | MIT |
| [VirtualTerminal.WPF](https://github.com/Rikitav/VirtualTerminal) | Apache 2.0 |
| [VirtualTerminal.SecureShell](https://github.com/Rikitav/VirtualTerminal) | Apache 2.0 |

All dependencies use permissive open-source licenses, with no commercial components, and are compatible with Apache 2.0.

## Code Signing

Release packages are currently unsigned. A free Windows code signing certificate is planned via the SignPath Foundation; see [CODE_SIGNING.md](CODE_SIGNING.md) for details.

## Sponsorship

The GitHub Sponsors application has not been submitted yet (this requires the maintainer to apply at github.com/sponsors and connect a Stripe payout account). The repository already includes `.github/FUNDING.yml` (`github: [OpenNodeSaas]`); once approved, a Sponsor button will appear on the repository page.

## License

This project is open-sourced under the [Apache License 2.0](LICENSE).

---

[中文说明 (Chinese)](README.md)
