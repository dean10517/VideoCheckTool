using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using VideoCheckTool.Models;
using VideoCheckTool.Services;

namespace VideoCheckTool;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        aFolderTextBox.Text = settings.AFolder;
        dFolderTextBox.Text = settings.DFolder;
        BuildExtensionChecks(videoExtensionsPanel, MediaCatalogService.VideoExtensions);
        BuildExtensionChecks(imageExtensionsPanel, MediaCatalogService.ImageExtensions);
    }

    private void BuildExtensionChecks(Panel panel, IEnumerable<string> extensions)
    {
        foreach (var extension in extensions.OrderBy(x => x))
        {
            panel.Children.Add(new CheckBox
            {
                Content = extension,
                Tag = extension,
                IsChecked = _settings.EnabledExtensions.Contains(extension),
                Width = 92,
                Margin = new Thickness(5, 10, 5, 10)
            });
        }
    }

    private void BrowseAFolder_Click(object sender, RoutedEventArgs e) => BrowseInto(aFolderTextBox);
    private void BrowseDFolder_Click(object sender, RoutedEventArgs e) => BrowseInto(dFolderTextBox);

    private static void BrowseInto(TextBox target)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "選擇分類資料夾",
            InitialDirectory = Directory.Exists(target.Text) ? target.Text : null
        };
        if (dialog.ShowDialog() == true) target.Text = dialog.FolderName;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var aFolder = aFolderTextBox.Text.Trim();
        var dFolder = dFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(aFolder) || string.IsNullOrWhiteSpace(dFolder))
        {
            MessageBox.Show(this, "請設定 A 與 D 兩個分類資料夾。", "設定未完成", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var enabled = videoExtensionsPanel.Children.OfType<CheckBox>()
            .Concat(imageExtensionsPanel.Children.OfType<CheckBox>())
            .Where(x => x.IsChecked == true).Select(x => (string)x.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (enabled.Count == 0)
        {
            MessageBox.Show(this, "請至少勾選一種影片或照片格式。", "未選擇格式", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            if (string.Equals(Path.GetFullPath(aFolder), Path.GetFullPath(dFolder), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "A 與 D 必須使用不同的資料夾。", "資料夾重複", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Directory.CreateDirectory(aFolder);
            Directory.CreateDirectory(dFolder);
            _settings.AFolder = Path.GetFullPath(aFolder);
            _settings.DFolder = Path.GetFullPath(dFolder);
            _settings.EnabledExtensions = enabled;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"無法使用指定資料夾：\n{ex.Message}", "設定失敗", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
