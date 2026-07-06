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
│   ├── realmlist.wtf            # created temporarily only when Original starts
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

## Startup behavior

When the launcher starts, it performs one best-effort pass:

1. clears `1.12.1\WDB\` contents;
2. clears `1.14.2\_classic_era_\Cache\` contents;
3. starts `HermesProxy.exe` in the background.

It does **not** create, modify, or delete `1.12.1\realmlist.wtf` at ordinary launcher startup.

## Original / 1.12.1 launch behavior

Clicking **Original / 原始版** does this sequence:

1. resolves an IPv4 address for `ServerAddress`;
2. completely writes `1.12.1\realmlist.wtf` as:

   ```text
   set realmlist <IPv4>:32769
   ```

3. starts `1.12.1\WoW.exe`;
4. waits for the WoW GUI input queue to become ready, with a maximum wait of five seconds;
5. deletes `1.12.1\realmlist.wtf`.

The old 1.12 client reads the realmlist during early GUI startup. The temporary file therefore exists only for that launch. If `WoW.exe` exits early or startup fails, deletion is still attempted.

## Proxy command

The launcher starts the proxy as though it ran:

```powershell
.\HermesProxy.exe `
  --set ServerAddress=moshou.dynv6.net `
  --set ServerPort=32769 `
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
