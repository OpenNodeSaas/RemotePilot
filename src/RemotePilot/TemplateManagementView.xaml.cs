using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using RemotePilot.Models;
using RemotePilot.Services;

namespace RemotePilot;

public partial class TemplateManagementView : UserControl
{
    public const string ResWinTitle = "TmplMgmt_WinTitle";
    public const string ResHeader = "TmplMgmt_Header";
    public const string ResTemplateList = "TmplMgmt_TemplateList";
    public const string ResSortByName = "TmplMgmt_SortByName";
    public const string ResSortByUsage = "TmplMgmt_SortByUsage";
    public const string ResDetailTitle = "TmplMgmt_DetailTitle";
    public const string ResFldName = "Fld_Name";
    public const string ResFldProtocol = "Fld_Protocol";
    public const string ResFldPort = "Fld_Port";
    public const string ResFldUsername = "Fld_Username";
    public const string ResRdpSettings = "TmplMgmt_RdpSettings";
    public const string ResFldResolution = "TmplMgmt_Resolution";
    public const string ResFldColorDepth = "TmplMgmt_ColorDepth";
    public const string ResFldGateway = "TmplMgmt_Gateway";
    public const string ResRedirectSettings = "TmplMgmt_RedirectSettings";
    public const string ResRedirectDrives = "Dlg_DriveMap";
    public const string ResRedirectAudio = "Dlg_Audio";
    public const string ResRedirectClipboard = "Dlg_Clipboard";
    public const string ResBtnNew = "Btn_New";
    public const string ResBtnEdit = "Btn_Edit";
    public const string ResBtnCopy = "Btn_Copy";
    public const string ResBtnDelete = "TmplMgmt_BtnDelete";
    public const string ResBtnClose = "Btn_Close";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    private readonly AssetStore _assetStore;
    private AssetStoreData _data;
    private List<TemplateDisplayItem> _displayItems = new();
    private bool _uiReady;

    /// <summary>模板数据发生变化（增删改并落盘成功）时触发，供外层刷新引用模板的资产列表。</summary>
    public event Action? DataChanged;

    /// <summary>用户点击底部「关闭」按钮时触发，由外层切换回资产中心。</summary>
    public event Action? CloseRequested;

    public TemplateManagementView() : this(new AssetStore())
    {
    }

    public TemplateManagementView(AssetStore assetStore)
    {
        InitializeComponent();
        _assetStore = assetStore;
        _data = _assetStore.Load();
        _uiReady = true;
        CmbTemplateSort.SelectedIndex = 0; // 默认按名称排序
        LoadTemplates();
    }

    /// <summary>重新从存储加载模板并刷新列表，供外部在切换到本视图时保证数据最新。</summary>
    public void Reload()
    {
        _data = _assetStore.Load();
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        var usageCount = CalculateUsageCount();
        var selectedId = (LstTemplates.SelectedItem as TemplateDisplayItem)?.Template.Id;

        IEnumerable<TemplateDisplayItem> items = _data.Templates.Select(t => new TemplateDisplayItem
        {
            Template = t,
            UsageCount = usageCount.GetValueOrDefault(t.Id, 0)
        });

        items = CmbTemplateSort?.SelectedIndex == 1
            ? items.OrderByDescending(i => i.UsageCount).ThenBy(i => i.Template.Name, StringComparer.CurrentCulture)
            : items.OrderBy(i => i.Template.Name, StringComparer.CurrentCulture);

        _displayItems = items.ToList();
        LstTemplates.ItemsSource = _displayItems;

        var toSelect = selectedId.HasValue ? _displayItems.FirstOrDefault(i => i.Template.Id == selectedId.Value) : null;
        LstTemplates.SelectedItem = toSelect ?? (_displayItems.Count > 0 ? _displayItems[0] : null);
    }

    private void CmbTemplateSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_uiReady) return;
        LoadTemplates();
    }

    private Dictionary<Guid, int> CalculateUsageCount()
    {
        var count = new Dictionary<Guid, int>();
        foreach (var asset in _data.Assets)
        {
            if (asset.TemplateId.HasValue)
            {
                count[asset.TemplateId.Value] = count.GetValueOrDefault(asset.TemplateId.Value, 0) + 1;
            }
        }
        return count;
    }

    private void LstTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstTemplates.SelectedItem is TemplateDisplayItem item)
        {
            DisplayTemplateDetails(item.Template);
            BtnEdit.IsEnabled = true;
            BtnCopy.IsEnabled = true;
            BtnDelete.IsEnabled = true;
        }
        else
        {
            PanelDetails.Visibility = Visibility.Collapsed;
            BtnEdit.IsEnabled = false;
            BtnCopy.IsEnabled = false;
            BtnDelete.IsEnabled = false;
        }
    }

    private void DisplayTemplateDetails(ConnectionTemplate template)
    {
        PanelDetails.Visibility = Visibility.Visible;
        TxtDetailName.Text = template.Name;
        TxtDetailProtocol.Text = template.Protocol;
        TxtDetailPort.Text = template.Port.ToString();
        TxtDetailUsername.Text = string.IsNullOrWhiteSpace(template.Username) 
            ? L10n["TmplMgmt_NoValue"] ?? "-" 
            : template.Username;
        
        TxtDetailResolution.Text = $"{template.RdpWidth} x {template.RdpHeight}";
        TxtDetailColorDepth.Text = $"{template.RdpColorDepth} " + (L10n["TmplMgmt_Bit"] ?? "位");
        TxtDetailGateway.Text = string.IsNullOrWhiteSpace(template.RdpGateway) 
            ? L10n["TmplMgmt_NoGateway"] ?? "无网关" 
            : template.RdpGateway;

        ChkDetailDrives.IsChecked = template.RedirectDrives;
        ChkDetailAudio.IsChecked = template.RedirectAudio;
        ChkDetailClipboard.IsChecked = template.RedirectClipboard;
    }

    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new TemplateEditDialog();
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            _data.Templates.Add(dialog.Result);
            _assetStore.Save(_data);
            LoadTemplates();
            LstTemplates.SelectedItem = _displayItems.FirstOrDefault(i => i.Template.Id == dialog.Result.Id);
            DataChanged?.Invoke();
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (LstTemplates.SelectedItem is not TemplateDisplayItem item) return;

        var dialog = new TemplateEditDialog(item.Template);
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            var index = _data.Templates.FindIndex(t => t.Id == item.Template.Id);
            if (index >= 0)
            {
                _data.Templates[index] = dialog.Result;
                _assetStore.Save(_data);
                LoadTemplates();
                LstTemplates.SelectedItem = _displayItems.FirstOrDefault(i => i.Template.Id == dialog.Result.Id);
                DataChanged?.Invoke();
            }
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (LstTemplates.SelectedItem is not TemplateDisplayItem item) return;

        var copied = item.Template with 
        { 
            Id = Guid.NewGuid(),
            Name = item.Template.Name + " - " + (L10n["TmplMgmt_Copy"] ?? "副本")
        };

        var dialog = new TemplateEditDialog(copied);
        if (dialog.ShowDialog() == true && dialog.Result != null)
        {
            _data.Templates.Add(dialog.Result);
            _assetStore.Save(_data);
            LoadTemplates();
            LstTemplates.SelectedItem = _displayItems.FirstOrDefault(i => i.Template.Id == dialog.Result.Id);
            DataChanged?.Invoke();
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (LstTemplates.SelectedItem is not TemplateDisplayItem item) return;

        if (item.UsageCount > 0)
        {
            var message = string.Format(
                L10n["TmplMgmt_DeleteWarning"] ?? "该模板被 {0} 个资产使用，删除后这些资产将使用顶栏默认值。\n\n确定要删除吗？",
                item.UsageCount);
            var result = MessageBox.Show(
                message,
                L10n["TmplMgmt_DeleteTitle"] ?? "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes) return;
        }
        else
        {
            var result = MessageBox.Show(
                L10n["TmplMgmt_DeleteConfirm"] ?? "确定要删除该模板吗？",
                L10n["TmplMgmt_DeleteTitle"] ?? "确认删除",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            
            if (result != MessageBoxResult.Yes) return;
        }

        _data.Templates.RemoveAll(t => t.Id == item.Template.Id);
        // 同步清除引用了该模板的资产绑定，避免悬空引用导致连接异常
        for (int i = 0; i < _data.Assets.Count; i++)
        {
            if (_data.Assets[i].TemplateId == item.Template.Id)
            {
                _data.Assets[i] = _data.Assets[i] with { TemplateId = null };
            }
        }
        _assetStore.Save(_data);
        LoadTemplates();
        DataChanged?.Invoke();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        CloseRequested?.Invoke();
    }

    private class TemplateDisplayItem
    {
        public ConnectionTemplate Template { get; init; } = null!;
        public int UsageCount { get; init; }
        public string UsageText => UsageCount > 0
            ? $"被 {UsageCount} 个资产使用"
            : "未被使用";
    }
}
