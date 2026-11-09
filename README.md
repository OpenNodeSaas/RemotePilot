# RemotePilot

[![License](https://img.shields.io/badge/license-Apache%202.0-blue.svg)](LICENSE)

RemotePilot 是一款面向 Windows 运维人员的免费、绿色、离线优先的远程运维客户端，基于 WPF（.NET）构建。

- **RDP 直连**：动态生成 `.rdp` 文件，通过系统自带 `mstsc.exe` 连接，不经过任何第三方代理。
- **内置 SSH 终端**：基于 SSH.NET 与 VirtualTerminal.WPF，支持 SSH 跳板机。
- **资产管理**：分组、搜索、到期提醒；支持 CSV / Excel 批量导入。
- **连接方案模板**：可复用的 RDP 参数模板，支持分辨率与分屏（左右 / 上下一分二）快捷设置。
- **离线优先**：所有数据保存在本地，无需账号，无需联网。

## 运行要求

- Windows 10 / 11（x64）
- 发布包为 self-contained，无需另装 .NET 运行时；解压后直接运行 `RemotePilot.exe`

## 构建

需要 [.NET SDK 10](https://dotnet.microsoft.com/download)。

```powershell
dotnet build src\RemotePilot\RemotePilot.csproj -c Release
dotnet test tests\RemotePilot.Tests
```

## 数据与安全

- 资产数据保存在 `%LOCALAPPDATA%\FastRdp\RemoteOps\assets.json`，备份或迁移只需复制该文件。
- 默认不保存密码。仅当你勾选"记住密码"时才保存，且使用 Windows DPAPI（当前用户）加密，只能在同一 Windows 用户下解密。
- 为兼容历史版本，数据目录仍沿用 `%LOCALAPPDATA%\FastRdp\RemoteOps\`，与程序名无关。

## 第三方依赖

| 组件 | 许可证 |
| --- | --- |
| [ExcelDataReader](https://github.com/ExcelDataReader/ExcelDataReader) | MIT |
| [SSH.NET](https://github.com/sshnet/SSH.NET) | MIT |
| [BouncyCastle.Cryptography](https://www.bouncycastle.org/) | MIT |
| Microsoft.Extensions.Logging.Abstractions | MIT |
| [VirtualTerminal.WPF](https://github.com/Rikitav/VirtualTerminal) | Apache 2.0 |
| [VirtualTerminal.SecureShell](https://github.com/Rikitav/VirtualTerminal) | Apache 2.0 |

全部为宽松开源许可证，无商业组件，与 Apache 2.0 兼容。

## 代码签名

目前发布包未签名。计划通过 SignPath Foundation 申请免费 Windows 代码签名，详情见 [CODE_SIGNING.md](CODE_SIGNING.md)。

## 赞助

GitHub Sponsors 申请尚未提交（需维护者在 github.com/sponsors 申请并接通 Stripe 收款）。仓库已配置 `.github/FUNDING.yml`（`github: [OpenNodeSaas]`），通过后仓库页面将显示 Sponsor 按钮。

## 许可证

本项目以 [Apache License 2.0](LICENSE) 开源。

---

powered by [www.idazi.xyz](https://www.idazi.xyz)
