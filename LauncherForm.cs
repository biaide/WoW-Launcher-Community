using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace WoWLauncherCommunity;

internal sealed class LauncherForm : Form
{
    // Fixed values compiled into Launcher.exe. HermesProxy.config is not edited.
    private const string ServerAddress = "moshou.dynv6.net";
    private const int ServerPort = 32769;
    private const int ClientBuild = 42597;
    private const int ServerBuild = 5875;

    private const string ProxyExeName = "HermesProxy.exe";
    private const string ClassicExeRelativePath = @"1.12.1\WoW.exe";
    private const string RemasteredExeRelativePath = @"1.14.2\_classic_era_\WowClassic_ForCustomServers.exe";
    private const string RealmlistRelativePath = @"1.12.1\realmlist.wtf";
    private const string ClassicWdbRelativePath = @"1.12.1\WDB";
    private const string RemasteredCacheRelativePath = @"1.14.2\_classic_era_\Cache";

    private readonly string _rootDirectory = AppContext.BaseDirectory;
    private readonly NotifyIcon _trayIcon;
    private readonly Icon _applicationIcon;
    private Process? _ownedProxyProcess;
    private bool _exitRequested;

    private static bool UseChinese =>
        string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase);

    private static string Ui(string chinese, string english) => UseChinese ? chinese : english;

    public LauncherForm()
    {
        Text = Ui("魔兽世界", "WoW");
        ClientSize = new Size(500, 225);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = true;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = SystemColors.Control;

        var titleLabel = new Label
        {
            Text = Ui("保持此程序开启", "Keep this program running"),
            AutoSize = false,
            Location = new Point(28, 40),
            Size = new Size(444, 34),
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var launchRemasteredButton = new Button
        {
            Text = Ui("重制版", "Remastered"),
            Location = new Point(48, 116),
            Size = new Size(185, 62),
            Font = new Font(Font.FontFamily, 13F, FontStyle.Regular),
            UseVisualStyleBackColor = true
        };
        launchRemasteredButton.Click += (_, _) => LaunchGame(RemasteredExeRelativePath);

        var launchClassicButton = new Button
        {
            Text = Ui("原始版", "Original"),
            Location = new Point(267, 116),
            Size = new Size(185, 62),
            Font = new Font(Font.FontFamily, 13F, FontStyle.Regular),
            UseVisualStyleBackColor = true
        };
        launchClassicButton.Click += (_, _) => LaunchGame(ClassicExeRelativePath);

        Controls.Add(titleLabel);
        Controls.Add(launchRemasteredButton);
        Controls.Add(launchClassicButton);

        _applicationIcon = LoadApplicationIcon();
        Icon = _applicationIcon;

        var trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add(Ui("显示启动器", "Show launcher"), null, (_, _) => RestoreFromTray());
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(Ui("打开重制版", "Open remastered"), null, (_, _) => LaunchGame(RemasteredExeRelativePath));
        trayMenu.Items.Add(Ui("打开原始版", "Open original"), null, (_, _) => LaunchGame(ClassicExeRelativePath));
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(Ui("退出程序", "Exit program"), null, (_, _) => ExitFromTray());

        _trayIcon = new NotifyIcon
        {
            Icon = _applicationIcon,
            Text = Ui("魔兽世界", "WoW"),
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

    private static Icon LoadApplicationIcon()
    {
        const string resourceName = "ico.ico";

        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName);

        return stream is null
            ? (Icon)SystemIcons.Application.Clone()
            : new Icon(stream);
    }

    private void RunStartupTasks()
    {
        RunQuietly(() => ClearDirectoryContents(Path.Combine(_rootDirectory, ClassicWdbRelativePath)));
        RunQuietly(() => ClearDirectoryContents(Path.Combine(_rootDirectory, RemasteredCacheRelativePath)));
        RunQuietly(WriteClassicRealmlist);
        RunQuietly(StartProxy);
    }

    private static void RunQuietly(Action action)
    {
        try
        {
            action();
        }
        catch
        {
            // This launcher intentionally has no setup-error popups.
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
            ?? throw new InvalidOperationException();
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

        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(_rootDirectory, ProxyExeName),
            WorkingDirectory = _rootDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // HermesProxy loads its own untouched default HermesProxy.config.
        // The launcher supplies only the verified runtime overrides.
        startInfo.ArgumentList.Add("--set");
        startInfo.ArgumentList.Add($"ServerAddress={ServerAddress}");
        startInfo.ArgumentList.Add("--set");
        startInfo.ArgumentList.Add($"ServerPort={ServerPort}");
        startInfo.ArgumentList.Add("--set");
        startInfo.ArgumentList.Add($"ClientBuild={ClientBuild}");
        startInfo.ArgumentList.Add("--set");
        startInfo.ArgumentList.Add($"ServerBuild={ServerBuild}");

        _ownedProxyProcess = Process.Start(startInfo);
    }

    private void LaunchGame(string relativeExePath)
    {
        RunQuietly(() =>
        {
            var gamePath = Path.Combine(_rootDirectory, relativeExePath);

            Process.Start(new ProcessStartInfo
            {
                FileName = gamePath,
                WorkingDirectory = Path.GetDirectoryName(gamePath)!,
                UseShellExecute = true
            });
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

    internal void RestoreFromExternalActivation()
    {
        RestoreFromTray();
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
            // Windows is closing or the proxy exited between checks.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
            _applicationIcon.Dispose();
        }

        base.Dispose(disposing);
    }
}
