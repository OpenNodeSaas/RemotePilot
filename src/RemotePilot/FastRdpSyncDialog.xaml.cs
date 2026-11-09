using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using RemotePilot.Models;
using RemotePilot.Services;

namespace RemotePilot;

public class SyncPreviewItemViewModel : INotifyPropertyChanged
{
    private bool _isSelected = true;
    public ServerAsset Asset { get; set; }
    
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }
    }

    public string Name => Asset.Name;
    public string Host => Asset.Host;

    public SyncPreviewItemViewModel(ServerAsset asset)
    {
        Asset = asset;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public partial class FastRdpSyncDialog : Window
{
    public const string ResSyncTitle = "Sync_Title";
    public const string ResSyncHeader = "Sync_Header";
    public const string ResSyncDesc = "Sync_Desc";
    public const string ResSyncBaseUrl = "Sync_BaseUrl";
    public const string ResSyncToken = "Sync_Token";
    public const string ResSyncPreview = "Sync_Preview";
    public const string ResSyncBtnSync = "Sync_BtnSync";
    public const string ResBtnCancel = "Btn_Cancel";

    private static LocalizationManager L10n => LocalizationManager.Instance;

    private readonly ObservableCollection<SyncPreviewItemViewModel> _previewItems = [];
    public List<ServerAsset> SyncedAssets { get; private set; } = [];
    public string ApiUrl => TxtBaseUrl.Text.Trim();

    public FastRdpSyncDialog(string? defaultApiUrl = null)
    {
        InitializeComponent();
        if (!string.IsNullOrWhiteSpace(defaultApiUrl))
        {
            TxtBaseUrl.Text = defaultApiUrl;
        }
        GridPreview.ItemsSource = _previewItems;
    }

    private async void BtnSync_Click(object sender, RoutedEventArgs e)
    {
        var url = TxtBaseUrl.Text.Trim();
        var token = TxtToken.Text.Trim();

        if (string.IsNullOrWhiteSpace(url))
        {
            MessageBox.Show(L10n["Sync_MsgUrlRequired"], L10n["Msg_Tip"], MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnSync.IsEnabled = false;
        TxtStatus.Text = L10n["Sync_Connecting"];

        try
        {
            using var apiClient = new FastRdpApiClient();
            TxtStatus.Text = L10n["Sync_Fetching"];
            var assets = await apiClient.FetchAssetsFromEndpointAsync(url, token);

            if (_previewItems.Count == 0 && assets.Count > 0)
            {
                // 去重显示并加载到预览表格
                var uniqueAssets = assets.GroupBy(a => a.Host).Select(g => g.First()).ToList();
                foreach (var asset in uniqueAssets)
                {
                    _previewItems.Add(new SyncPreviewItemViewModel(asset));
                }
                TxtStatus.Text = L10n.Format("Sync_Fetched", assets.Count, uniqueAssets.Count);
                BtnSync.Content = L10n["Sync_BtnConfirm"];
                BtnSync.IsEnabled = true;
                return;
            }

            // 二次点击：收集勾选项导入
            SyncedAssets = _previewItems.Where(p => p.IsSelected).Select(p => p.Asset).ToList();
            TxtStatus.Text = L10n.Format("Sync_Imported", SyncedAssets.Count);

            await Task.Delay(500);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            TxtStatus.Text = L10n.Format("Sync_Error", ex.Message);
            BtnSync.IsEnabled = true;
        }
    }
}
