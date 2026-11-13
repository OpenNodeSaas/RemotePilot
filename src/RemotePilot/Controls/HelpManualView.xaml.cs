using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using RemotePilot.Services;

namespace RemotePilot.Controls;

/// <summary>
/// 帮助手册：集中说明各协议的疑点与注意事项。（VNC 客户端配置已迁移到设置页。）
/// </summary>
public partial class HelpManualView : UserControl
{
    // Localization Keys
    public const string ResTitle = "Help_Title";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    /// <summary>章节顺序：先讲 VNC（本页配置项相关），再讲 RDP / SSH 的疑点与通用说明。</summary>
    private static readonly (string TitleKey, string BodyKey)[] Sections =
    [
        ("Help_SecVncUsage_Title", "Help_SecVncUsage_Body"),
        ("Help_SecVncFaq_Title", "Help_SecVncFaq_Body"),
        ("Help_SecRdp_Title", "Help_SecRdp_Body"),
        ("Help_SecRedirection_Title", "Help_SecRedirection_Body"),
        ("Help_SecSsh_Title", "Help_SecSsh_Body"),
        ("Help_SecGeneral_Title", "Help_SecGeneral_Body")
    ];

    public HelpManualView()
    {
        InitializeComponent();

        LocalizationManager.Instance.LanguageChanged += (s, e) =>
        {
            ApplyStaticTexts();
            BuildSections();
        };

        ApplyStaticTexts();
        BuildSections();
    }

    private void ApplyStaticTexts()
    {
        TxtSubtitle.Text = L10n["Help_Subtitle"];
        TxtVncMoved.Text = L10n["Help_VncMoved"];
    }

    private void BuildSections()
    {
        PanelSections.Children.Clear();
        foreach (var (titleKey, bodyKey) in Sections)
        {
            PanelSections.Children.Add(new TextBlock
            {
                Text = L10n[titleKey],
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("Primary"),
                Margin = new Thickness(0, 16, 0, 6)
            });

            PanelSections.Children.Add(new TextBlock
            {
                Text = L10n[bodyKey],
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12.5,
                LineHeight = 20,
                Foreground = (Brush)FindResource("AppForeground")
            });
        }
    }
}
