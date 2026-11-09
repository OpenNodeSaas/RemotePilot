using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using RemotePilot.ViewModels;
using VirtualTerminal;

namespace RemotePilot.Controls;

public partial class SessionCellControl : UserControl
{
    public const string ResTipMaximize = "Tip_MaximizeRestore";
    public const string ResTipClose = "Tip_CloseSession";

    private SessionItemViewModel? _viewModel;
    private bool _wasFocused;

    public event EventHandler? Focused;
    public event EventHandler? Closed;
    public event EventHandler? MaximizedToggled;

    public SessionCellControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // 从资产列表切回工作台（控件由隐藏变可见）时，让当前活动会话重新拿到键盘焦点
        IsVisibleChanged += (s, e) =>
        {
            if (e.NewValue is true && _viewModel?.IsFocused == true)
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusTerminal));
            }
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is SessionItemViewModel oldVm)
        {
            oldVm.PropertyChanged -= Vm_PropertyChanged;
        }

        if (e.NewValue is SessionItemViewModel newVm)
        {
            _viewModel = newVm;
            _viewModel.PropertyChanged += Vm_PropertyChanged;
            UpdateUI();
        }
    }

    private void Vm_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Dispatcher.Invoke(UpdateUI);
    }

    private void UpdateUI()
    {
        if (_viewModel == null) return;

        TxtAssetName.Text = _viewModel.Name;
        TxtHostPort.Text = $" ({_viewModel.HostPort})";
        TxtProtocol.Text = _viewModel.Protocol.ToUpper();

        // 更新状态指示灯颜色
        switch (_viewModel.StatusKind)
        {
            case SessionStatusKind.Connected:
                IndicatorStatus.Fill = (Brush)FindResource("Success");
                break;
            case SessionStatusKind.Disconnected:
            case SessionStatusKind.Failed:
                IndicatorStatus.Fill = (Brush)FindResource("Danger");
                break;
            default:
                IndicatorStatus.Fill = (Brush)FindResource("Warning");
                break;
        }

        // 更新聚焦边框高亮
        if (_viewModel.IsFocused)
        {
            CellOuterBorder.BorderBrush = (Brush)FindResource("Primary");
            CellOuterBorder.BorderThickness = new Thickness(2);
        }
        else
        {
            CellOuterBorder.BorderBrush = (Brush)FindResource("BorderBrush");
            CellOuterBorder.BorderThickness = new Thickness(1);
        }

        // 会话被激活（点标签页 / 切换布局）时，把键盘焦点交给该会话的终端
        bool focusGained = _viewModel.IsFocused && !_wasFocused;
        _wasFocused = _viewModel.IsFocused;

        // 更新最大化还原按钮图标
        BtnMaximizeRestore.Content = _viewModel.IsMaximized ? "🗗" : "🗖";

        // 附加视图元素到 ContentHost（切换布局时元素会换父容器，需先从旧父级摘除）
        var viewElement = _viewModel.ViewElement;
        if (viewElement != null && !ContentHost.Children.Contains(viewElement))
        {
            if (viewElement.Parent is Panel oldPanel)
            {
                oldPanel.Children.Remove(viewElement);
            }
            else if (viewElement.Parent is Decorator oldDecorator && ReferenceEquals(oldDecorator.Child, viewElement))
            {
                oldDecorator.Child = null;
            }
            else if (viewElement.Parent is ContentControl oldContentControl && ReferenceEquals(oldContentControl.Content, viewElement))
            {
                oldContentControl.Content = null;
            }

            ContentHost.Children.Clear();
            ContentHost.Children.Add(viewElement);
        }

        if (focusGained)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(FocusTerminal));
        }
    }

    /// <summary>把键盘焦点交给内容区内的终端控件（SSH 终端只有持有键盘焦点才能输入）。</summary>
    private void FocusTerminal()
    {
        var terminal = FindTerminal(ContentHost);
        if (terminal is null || terminal.IsKeyboardFocused) return;
        terminal.Focus();
    }

    private static TerminalControl? FindTerminal(DependencyObject root)
    {
        if (root is TerminalControl terminal) return terminal;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var found = FindTerminal(VisualTreeHelper.GetChild(root, i));
            if (found != null) return found;
        }
        return null;
    }

    private void UserControl_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // 只负责把当前单元格标记为活动会话；绝不调用 Focus()，否则会抢走终端的键盘焦点
        Focused?.Invoke(this, EventArgs.Empty);
    }

    private void HeaderBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        Focused?.Invoke(this, EventArgs.Empty);

        if (e.ClickCount == 2)
        {
            // 双击标题栏快速切换最大化
            MaximizedToggled?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void BtnMaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        MaximizedToggled?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Closed?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
