using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace WoWLauncherCommunity;

internal sealed class LauncherForm : Form
{
    // The only server values compiled into Launcher.exe.
    private const string ServerAddress = "moshou.dynv6.net";
    private const int ServerPort = 32769;

    private const string ProxyExeName = "HermesProxy.exe";
    private const string ProxyConfigName = "HermesProxy.config";
    private const string ClassicExeRelativePath = @"1.12.1\WoW.exe";
    private const string RemasteredExeRelativePath = @"1.14.2\_classic_era_\WowClassic_ForCustomServers.exe";
    private const string RealmlistRelativePath = @"1.12.1\realmlist.wtf";
    private const string ClassicWdbRelativePath = @"1.12.1\WDB";
    private const string RemasteredCacheRelativePath = @"1.14.2\_classic_era_\Cache";

    private readonly string _rootDirectory = AppContext.BaseDirectory;
    private readonly NotifyIcon _trayIcon;
    private Process? _ownedProxyProcess;
    private bool _exitRequested;

    public LauncherForm()
    {
        Text = "WoW Community";
        ClientSize = new Size(330, 150);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var launchRemasteredButton = new Button
        {
            Text = "重制版",
            Location = new Point(30, 45),
            Size = new Size(125, 55),
            Font = new Font(Font.FontFamily, 12, FontStyle.Regular)
        };
        launchRemasteredButton.Click += (_, _) => LaunchGame(RemasteredExeRelativePath);

        var launchClassicButton = new Button
        {
            Text = "复古版",
            Location = new Point(175, 45),
            Size = new Size(125, 55),
            Font = new Font(Font.FontFamily, 12, FontStyle.Regular)
        };
        launchClassicButton.Click += (_, _) => LaunchGame(ClassicExeRelativePath);

        Controls.Add(launchRemasteredButton);
        Controls.Add(launchClassicButton);

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("显示启动器", null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("退出并关闭代理", null, (_, _) => ExitFromTray());

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "WoW Community",
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        Load += (_, _) => RunStartupTasks();
        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                HideToTray();
            }
        };
        FormClosing += OnFormClosing;
    }

    private void RunStartupTasks()
    {
        var errors = new List<string>();

        RunStep(() => ClearDirectoryContents(Path.Combine(_rootDirectory, ClassicWdbRelativePath)), errors);
        RunStep(() => ClearDirectoryContents(Path.Combine(_rootDirectory, RemasteredCacheRelativePath)), errors);
        RunStep(WriteClassicRealmlist, errors);
        RunStep(StartProxy, errors);

        if (errors.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine + Environment.NewLine, errors),
                "WoW Community",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static void RunStep(Action action, List<string> errors)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            errors.Add(exception.Message);
        }
    }

    private void WriteClassicRealmlist()
    {
        var ipv4 = ResolveIpv4(ServerAddress);
        var realmlistPath = Path.Combine(_rootDirectory, RealmlistRelativePath);
        var realmlistDirectory = Path.GetDirectoryName(realmlistPath)!;

        Directory.CreateDirectory(realmlistDirectory);
        File.WriteAllText(
            realmlistPath,
            $"set realmlist {ipv4}:{ServerPort}{Environment.NewLine}",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string ResolveIpv4(string host)
    {
        var ipv4 = Dns.GetHostAddresses(host)
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork);

        return ipv4?.ToString()
            ?? throw new InvalidOperationException($"没有从 {host} 解析到 IPv4 A 记录。");
    }

    private void StartProxy()
    {
        if (_ownedProxyProcess is { HasExited: false })
        {
            return;
        }

        if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ProxyExeName))
            .Any(process => !process.HasExited))
        {
            return;
        }

        var proxyPath = Path.Combine(_rootDirectory, ProxyExeName);
        var proxyConfigPath = Path.Combine(_rootDirectory, ProxyConfigName);

        if (!File.Exists(proxyPath))
        {
            throw new FileNotFoundException($"找不到代理程序：{proxyPath}");
        }

        if (!File.Exists(proxyConfigPath))
        {
            throw new FileNotFoundException($"找不到代理配置：{proxyConfigPath}");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = proxyPath,
            WorkingDirectory = _rootDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // HermesProxy.config remains the required base configuration.
        // Only the fixed server address and realmd port are overridden here.
        startInfo.ArgumentList.Add("--no-version-check");
        startInfo.ArgumentList.Add("--set");
        startInfo.ArgumentList.Add($"ServerAddress={ServerAddress}");
        startInfo.ArgumentList.Add("--set");
        startInfo.ArgumentList.Add($"ServerPort={ServerPort}");

        _ownedProxyProcess = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 HermesProxy.exe。");
    }

    private void LaunchGame(string relativeExePath)
    {
        var gamePath = Path.Combine(_rootDirectory, relativeExePath);

        if (!File.Exists(gamePath))
        {
            MessageBox.Show(
                $"找不到游戏程序：{gamePath}",
                "WoW Community",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = gamePath,
            WorkingDirectory = Path.GetDirectoryName(gamePath)!,
            UseShellExecute = true
        });
    }

    private static void ClearDirectoryContents(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(filePath, FileAttributes.Normal);
            File.Delete(filePath);
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(directoryPath)
            .OrderByDescending(path => path.Length))
        {
            Directory.Delete(childDirectory, recursive: true);
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_exitRequested || eventArgs.CloseReason == CloseReason.WindowsShutDown)
        {
            _trayIcon.Visible = false;
            StopOwnedProxy();
            return;
        }

        eventArgs.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
    }

    private void RestoreFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitFromTray()
    {
        _exitRequested = true;
        Close();
    }

    private void StopOwnedProxy()
    {
        try
        {
            if (_ownedProxyProcess is { HasExited: false })
            {
                _ownedProxyProcess.Kill(entireProcessTree: true);
                _ownedProxyProcess.WaitForExit(3_000);
            }
        }
        catch
        {
            // Windows is already closing or the process exited between checks.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
