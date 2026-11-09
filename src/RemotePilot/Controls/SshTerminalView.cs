using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using RemotePilot.Services;
using VirtualTerminal;
using VirtualTerminal.Extensions;

namespace RemotePilot.Controls
{
    /// <summary>
    /// SSH 终端视图：内部使用 VT 仿真器渲染，支持 nano / vim / htop 等全屏 TUI。
    /// 只负责界面与工具栏，SSH 会话由外部挂到 <see cref="Terminal"/> 上。
    /// </summary>
    public class SshTerminalView : Grid
    {
        private const double MinFontSize = 9;
        private const double MaxFontSize = 28;

        private static readonly string[] SchemeNames = { "Term_SchemeDark", "Term_SchemeGreen", "Term_SchemeLight" };

        // 所有终端会话共享字体与配色设置
        private static double _fontSize = 13;
        private static int _schemeIndex;

        private readonly TerminalControl _terminal;
        private readonly Border _statusBar;
        private readonly TextBlock _status;
        private readonly TextBlock _fontLabel;
        private readonly StackPanel _tools;
        private readonly Button _schemeButton;

        public SshTerminalView()
        {
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _terminal = new TerminalControl
            {
                Focusable = true,
                IsTabStop = true,
                AllowDirectInput = true,
                CursorVisible = true,
                ScrollbackLines = 5000,
                ScrollDownVisible = true,
                FontFamily = new FontFamily("Consolas"),
                FontSize = _fontSize,
                Padding = new Thickness(8, 6, 8, 6)
            };
            // 必须在 _terminal 赋值后再建菜单，否则 CommandTarget 取到的是 null
            _terminal.ContextMenu = BuildTerminalMenu();
            SetRow(_terminal, 0);
            Children.Add(_terminal);

            _statusBar = new Border { Padding = new Thickness(8, 1, 8, 1) };
            var bar = new DockPanel { LastChildFill = false };

            _status = new TextBlock
            {
                Text = "SSH",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(_status, Dock.Left);
            bar.Children.Add(_status);

            _tools = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockPanel.SetDock(_tools, Dock.Right);

            _schemeButton = CreateBarButton(L10n[SchemeNames[_schemeIndex]]);
            _schemeButton.Click += (s, e) =>
            {
                _schemeIndex = (_schemeIndex + 1) % SchemeNames.Length;
                _schemeButton.Content = L10n[SchemeNames[_schemeIndex]];
                ApplyScheme();
            };
            _tools.Children.Add(_schemeButton);

            var smaller = CreateBarButton("A-");
            smaller.Click += (s, e) => ChangeFontSize(-1);
            _tools.Children.Add(smaller);

            _fontLabel = new TextBlock
            {
                Text = ((int)_fontSize).ToString(),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(2, 0, 2, 0)
            };
            _tools.Children.Add(_fontLabel);

            var larger = CreateBarButton("A+");
            larger.Click += (s, e) => ChangeFontSize(1);
            _tools.Children.Add(larger);

            bar.Children.Add(_tools);
            _statusBar.Child = bar;
            SetRow(_statusBar, 1);
            Children.Add(_statusBar);

            PreviewMouseWheel += (s, e) =>
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
                ChangeFontSize(e.Delta > 0 ? 1 : -1);
                e.Handled = true;
            };

            ApplyScheme();

            // 鼠标按下终端任意位置都必须把键盘焦点交还终端，否则 nano / vim 里打字与快捷键全部无效
            _terminal.AddHandler(PreviewMouseDownEvent, new MouseButtonEventHandler((s, e) => FocusTerminal()), true);

            // WPF 不会为 Ctrl+字母 产生 TextInput，TerminalControl 也不编码控制字符，这里补齐。
            // 必须挂在终端的上层（隧道阶段先于 TerminalControl 的类处理器），才能拦下 Enter 的 CRLF。
            AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(OnTerminalKeyDown), true);
        }

        /// <summary>
        /// 补齐库没有编码的按键：Enter 只发 \r（库默认发 \r\n，shell/nano 会多出一个空行），
        /// 以及 Ctrl+字母 / Ctrl+[ 等控制字符（nano 的 ^O 保存、^X 退出、^K 剪切都依赖它）。
        /// 复制粘贴不再依赖库的按键绑定，统一在这里执行命令，保证 Ctrl+Shift+C / Ctrl+V / Shift+Insert 一定生效。
        /// </summary>
        private void OnTerminalKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled || !_terminal.AllowDirectInput) return;

            // 只有终端持有焦点时才需要转发，工具栏按钮等位置的按键交给控件自己处理
            if (!_terminal.IsKeyboardFocusWithin) return;

            ModifierKeys modifiers = Keyboard.Modifiers;
            bool ctrl = (modifiers & ModifierKeys.Control) != 0;
            bool shift = (modifiers & ModifierKeys.Shift) != 0;

            // 复制 / 粘贴快捷键
            if (e.Key == Key.Insert && (ctrl || shift))
            {
                if (shift) PasteFromClipboard();
                else CopySelection();
                e.Handled = true;
                return;
            }
            if (ctrl && e.Key == Key.V)
            {
                PasteFromClipboard();
                e.Handled = true;
                return;
            }
            if (ctrl && shift && e.Key == Key.C)
            {
                CopySelection();
                e.Handled = true;
                return;
            }

            if ((modifiers & ModifierKeys.Control) == 0 && (modifiers & ModifierKeys.Alt) == 0)
            {
                if (e.Key == Key.Return)
                {
                    Send("\r", e);
                }
                return;
            }

            if ((modifiers & ModifierKeys.Alt) != 0) return; // Alt 组合由库自己发 ESC 前缀

            int key = (int)e.Key;
            int code = key switch
            {
                >= (int)Key.A and <= (int)Key.Z => key - (int)Key.A + 1,
                (int)Key.Space => 0x00,
                (int)Key.D2 => 0x00,            // ^@
                (int)Key.OemOpenBrackets => 0x1B, // ^[
                (int)Key.OemPipe => 0x1C,        // ^\（美式键盘的反斜杠）
                (int)Key.OemBackslash => 0x1C,
                (int)Key.OemCloseBrackets => 0x1D, // ^]
                (int)Key.D6 => 0x1E,             // ^^
                (int)Key.OemMinus => 0x1F,       // ^_
                _ => -1
            };
            if (code < 0) return;

            Send(((char)code).ToString(), e);
        }

        /// <summary>右键菜单：复制 / 粘贴 / 全选 / 清屏，文案在每次打开时按当前语言刷新。</summary>
        private ContextMenu BuildTerminalMenu()
        {
            var copy = new MenuItem { Command = TerminalControl.CopyCommand, CommandTarget = _terminal };
            var paste = new MenuItem { Command = TerminalControl.PasteCommand, CommandTarget = _terminal };
            var selectAll = new MenuItem { Command = TerminalControl.SelectAllCommand, CommandTarget = _terminal };
            var clear = new MenuItem { Command = TerminalControl.ClearCommand, CommandTarget = _terminal };

            var menu = new ContextMenu();
            menu.Items.Add(copy);
            menu.Items.Add(paste);
            menu.Items.Add(new Separator());
            menu.Items.Add(selectAll);
            menu.Items.Add(clear);
            menu.Opened += (s, e) =>
            {
                copy.Header = L10n["Term_Copy"];
                paste.Header = L10n["Term_Paste"];
                selectAll.Header = L10n["Term_SelectAll"];
                clear.Header = L10n["Term_Clear"];
            };
            return menu;
        }

        private void CopySelection()
        {
            try { TerminalControl.CopyCommand.Execute(null, _terminal); } catch { }
        }

        private void PasteFromClipboard()
        {
            try { TerminalControl.PasteCommand.Execute(null, _terminal); } catch { }
        }

        private void Send(string text, KeyEventArgs e)
        {
            try
            {
                _terminal.Session?.Append(text);
                e.Handled = true;
            }
            catch
            {
                // 会话已断开时忽略
            }
        }

        private static LocalizationManager L10n => LocalizationManager.Instance;

        /// <summary>VT 终端控件，由外部把 SSH 会话挂到 Session 上。</summary>
        public TerminalControl Terminal => _terminal;

        /// <summary>把键盘焦点交给终端；延迟到布局完成后再执行，避免元素尚未进入可视树时聚焦失败。</summary>
        public void FocusTerminal() => Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => _terminal.Focus()));

        public void SetStatus(string status) => _status.Text = status;

        private Button CreateBarButton(string text) => new()
        {
            Content = text,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6, 0, 6, 0),
            FontSize = 11,
            Cursor = Cursors.Hand,
            Focusable = false
        };

        private void ChangeFontSize(double delta)
        {
            _fontSize = Math.Clamp(_fontSize + delta, MinFontSize, MaxFontSize);
            _terminal.FontSize = _fontSize;
            _fontLabel.Text = ((int)_fontSize).ToString();
        }

        private void ApplyScheme()
        {
            (Color bg, Color fg, Color barBg, Color barFg) = _schemeIndex switch
            {
                1 => (ColorOf("#0B1F0B"), ColorOf("#33FF66"), ColorOf("#132613"), ColorOf("#6FD96F")),
                2 => (ColorOf("#F7F7F7"), ColorOf("#1F2328"), ColorOf("#E5E7EB"), ColorOf("#57606A")),
                _ => (ColorOf("#1E1E1E"), ColorOf("#D4D4D4"), ColorOf("#252526"), ColorOf("#9D9D9D"))
            };

            Background = new SolidColorBrush(bg);
            _terminal.ScreenBackground = bg;
            _terminal.ScreenForeground = fg;
            _statusBar.Background = new SolidColorBrush(barBg);
            _status.Foreground = new SolidColorBrush(barFg);
            foreach (UIElement child in _tools.Children)
            {
                if (child is Control control) control.Foreground = new SolidColorBrush(barFg);
            }
            _fontLabel.Foreground = new SolidColorBrush(barFg);
        }

        private static Color ColorOf(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    }
}
