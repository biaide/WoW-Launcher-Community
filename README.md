# WoW Launcher Community

A small .NET 10 WinForms launcher for one fixed CMaNGOS Classic server.

## Runtime layout

Place the published `Launcher.exe` into this root directory:

```text
wowcommunity/
├── Launcher.exe
├── HermesProxy.exe              # JimsProxy build renamed to HermesProxy.exe
├── HermesProxy.config           # original default file; do not edit it
├── CSV/                         # matching CSV folder from the same JimsProxy build
├── 1.12.1/
│   ├── WoW.exe
│   ├── realmlist.wtf
│   └── WDB/
└── 1.14.2/
    └── _classic_era_/
        ├── WowClassic_ForCustomServers.exe
        └── Cache/
```

## Fixed values compiled into Launcher.exe

```csharp
private const string ServerAddress = "moshou.dynv6.net";
private const int ServerPort = 32769;
private const int ClientBuild = 42597;
private const int ServerBuild = 5875;
```

At startup, the launcher performs one best-effort pass:

1. clears `1.12.1\WDB\` contents;
2. clears `1.14.2\_classic_era_\Cache\` contents;
3. resolves an IPv4 address for `ServerAddress` and writes `1.12.1\realmlist.wtf`;
4. starts `HermesProxy.exe` in the background.

It intentionally does not show setup-error popups for DNS, proxy, cache, or game-file problems. HermesProxy remains responsible for its own configuration and CSV errors.

The proxy command is equivalent to:

```powershell
.\HermesProxy.exe `
  --set ServerAddress=your.server.com `
  --set ServerPort=3724 `
  --set ClientBuild=42597 `
  --set ServerBuild=5875
```

`HermesProxy.config` must exist because HermesProxy itself requires it, but the launcher does not read, alter, validate, or generate it.

## Single-instance behavior

Only one launcher instance runs per Windows user session.

- Closing or minimizing the main window hides it in the notification area.
- Launching `Launcher.exe` again does not start a second instance; it restores the existing launcher window to the foreground.
- The notification-area menu follows the Windows UI language.
- Selecting **Exit program / 退出程序** closes the launcher and the HermesProxy process that it started.

## Build

Install the .NET 10 SDK, then run:

```bat
publish.cmd
```

Copy `publish\Launcher.exe` into the runtime root.
