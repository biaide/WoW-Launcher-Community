using System.Threading;

namespace WoWLauncherCommunity;

internal static class Program
{
    private const string InstanceMutexName = @"Local\WoWLauncherCommunity_Launcher";
    private const string ActivateEventName = @"Local\WoWLauncherCommunity_Activate";

    [STAThread]
    private static void Main()
    {
        using var instanceMutex = new Mutex(initiallyOwned: true, InstanceMutexName, out var isFirstInstance);

        if (!isFirstInstance)
        {
            SignalExistingLauncher();
            return;
        }

        using var activateEvent = new EventWaitHandle(
            initialState: false,
            mode: EventResetMode.AutoReset,
            name: ActivateEventName);

        ApplicationConfiguration.Initialize();

        using var form = new LauncherForm();
        using var shutdown = new CancellationTokenSource();

        var activationWatcher = Task.Run(() => WatchForActivation(activateEvent, form, shutdown.Token));

        try
        {
            Application.Run(form);
        }
        finally
        {
            shutdown.Cancel();
            try
            {
                activationWatcher.Wait(500);
            }
            catch
            {
                // The process is exiting.
            }
        }
    }

    private static void SignalExistingLauncher()
    {
        try
        {
            using var activateEvent = EventWaitHandle.OpenExisting(ActivateEventName);
            activateEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // The first instance is still creating its activation event.
        }
    }

    private static void WatchForActivation(
        EventWaitHandle activateEvent,
        LauncherForm form,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!activateEvent.WaitOne(200))
            {
                continue;
            }

            if (form.IsDisposed || !form.IsHandleCreated)
            {
                continue;
            }

            try
            {
                form.BeginInvoke((Action)form.RestoreFromExternalActivation);
            }
            catch (InvalidOperationException)
            {
                // The form is closing.
            }
        }
    }
}
