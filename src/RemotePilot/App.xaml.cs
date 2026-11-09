using System.Configuration;
using System.Data;
using System.Threading;
using System.Windows;
using RemotePilot.Services;

namespace RemotePilot;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private const string MutexName = @"Global\RemotePilot.SingleInstance";
    private const string ActivateEventName = @"Global\RemotePilot.Activate";

    // 命名互斥体/事件，进程存活期间必须持有引用，否则会被 GC 回收导致单实例失效
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _activateEvent;
    private RegisteredWaitHandle? _activateWait;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, MutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            // 已有实例在跑：通知它把窗口放到最前，然后本进程直接退出
            SignalExistingInstance();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown();
            return;
        }

        base.OnStartup(e);
        ThemeManager.Instance.Initialize();
        LocalizationManager.Instance.Initialize();

        // 首个实例监听"再次启动"信号，收到后激活自己的主窗口
        _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivateEventName);
        _activateWait = ThreadPool.RegisterWaitForSingleObject(
            _activateEvent,
            (_, _) => ActivateMainWindow(),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    private static void SignalExistingInstance()
    {
        // 首个实例创建事件句柄前可能有极短的窗口期，重试几次即可
        for (int i = 0; i < 5; i++)
        {
            try
            {
                using var handle = EventWaitHandle.OpenExisting(ActivateEventName);
                handle.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(100);
            }
        }
    }

    private void ActivateMainWindow()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (MainWindow is not { } window)
            {
                return;
            }

            if (window.WindowState == WindowState.Minimized)
            {
                window.WindowState = WindowState.Normal;
            }

            window.Show();
            window.Activate();
            // Windows 前台窗口限制会让 Activate 失效，用 TOPMOST 瞬时切换强制提到最前
            window.Topmost = true;
            window.Topmost = false;
            window.Focus();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activateWait?.Unregister(null);
        _activateEvent?.Dispose();
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
