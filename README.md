# WoW Launcher Community

A small Windows launcher for one fixed CMaNGOS Classic server.

## Source target

- .NET 10 / WinForms
- Output: self-contained `Launcher.exe` for Windows x64
- No .NET runtime is needed on the target game PC after `publish.cmd` completes.

## Runtime layout

Place the published `Launcher.exe` into the same root directory as the following files and folders:

```text
wowcommunity/
├── Launcher.exe
├── HermesProxy.exe              # JimsProxy build renamed to HermesProxy.exe
├── HermesProxy.config           # required by HermesProxy.exe
├── CSV/                         # required by JimsProxy/HermesProxy
├── 1.12.1/
│   ├── WoW.exe
│   ├── realmlist.wtf
│   └── WDB/
└── 1.14.2/
    └── _classic_era_/
        ├── WowClassic_ForCustomServers.exe
        └── Cache/
```

`AccountData/`, `Logs/`, and other HermesProxy output folders are created in this same root folder.

## Fixed settings

`LauncherForm.cs` contains the only two server constants:

```csharp
private const string ServerAddress = "moshou.dynv6.net";
private const int ServerPort = 32769;
```

At launcher startup it:

1. clears `1.12.1\WDB\` contents;
2. clears `1.14.2\_classic_era_\Cache\` contents;
3. resolves only an IPv4 A record for `ServerAddress` and writes `1.12.1\realmlist.wtf` as `set realmlist <IPv4>:32769`;
4. starts `HermesProxy.exe` in the background with `--no-version-check`, `--set ServerAddress=...`, and `--set ServerPort=...`;
5. leaves the proxy running when the window is minimized or closed to the notification area.

The only real exit command is in the notification-area menu. It closes the launcher and the proxy process started by this launcher.

## HermesProxy.config baseline

The proxy requires `HermesProxy.config` in the root working directory. Before distribution, set these values in that deployed file:

```xml
<add key="ClientBuild" value="42597" />
<add key="ServerBuild" value="5875" />
<add key="BNetPort" value="1119" />
```

The launcher intentionally does not write or replace `HermesProxy.config`. It overrides only `ServerAddress` and `ServerPort` at runtime.

## Build

Install the .NET 10 SDK on the development machine, then run:

```bat
publish.cmd
```

Copy the contents of `publish\` to the runtime root as `Launcher.exe`.
