using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using VideoCheckTool.Models;
using VideoCheckTool.Services;

namespace VideoCheckTool;

public partial class MainWindow : Window
{
    private enum PreviewCollection { Source, A, D }

    private readonly SettingsService _settingsService = new();
    private readonly MediaCatalogService _catalog = new();
    private readonly FileMoveService _moves = new();
    private readonly ObservableCollection<MediaItem> _sourceItems = [];
    private readonly ObservableCollection<MediaItem> _aItems = [];
    private readonly ObservableCollection<MediaItem> _dItems = [];
    private readonly DispatcherTimer _positionTimer;
    private AppSettings _settings;
    private MediaItem? _currentItem;
    private PreviewCollection _previewCollection = PreviewCollection.Source;
    private bool _isPlaying;
    private bool _isSeeking;
    private bool _isBusy;

    public MainWindow()
    {
        _settings = _settingsService.Load();
        InitializeComponent();
        sourceList.ItemsSource = _sourceItems;
        aList.ItemsSource = _aItems;
        dList.ItemsSource = _dItems;
        includeSubfoldersCheck.IsChecked = _settings.IncludeSubfolders;

        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _positionTimer.Tick += PositionTimer_Tick;
        seekSlider.AddHandler(Thumb.DragStartedEvent, new DragStartedEventHandler(Seek_DragStarted));
        seekSlider.AddHandler(Thumb.DragCompletedEvent, new DragCompletedEventHandler(Seek_DragCompleted));
        ContentRendered += MainWindow_ContentRendered;
    }

    private void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        ContentRendered -= MainWindow_ContentRendered;
        ApplySettingsAndRefresh();
        if (string.IsNullOrWhiteSpace(_settings.AFolder) || string.IsNullOrWhiteSpace(_settings.DFolder)) OpenSettings();
    }

    private void ChooseSource_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "選擇要檢視的資料夾",
            InitialDirectory = Directory.Exists(_settings.SourceFolder) ? _settings.SourceFolder : null
        };
        if (dialog.ShowDialog(this) != true) return;
        _settings.SourceFolder = dialog.FolderName;
        _settingsService.Save(_settings);
        RefreshAll();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void OpenSettings()
    {
        var window = new SettingsWindow(_settings) { Owner = this };
        if (window.ShowDialog() != true) return;
        _settingsService.Save(_settings);
        ApplySettingsAndRefresh();
    }

    private void ApplySettingsAndRefresh()
    {
        sourcePathText.Text = string.IsNullOrWhiteSpace(_settings.SourceFolder) ? "尚未選擇來源資料夾" : _settings.SourceFolder;
        aPathText.Text = string.IsNullOrWhiteSpace(_settings.AFolder) ? "尚未設定" : _settings.AFolder;
        dPathText.Text = string.IsNullOrWhiteSpace(_settings.DFolder) ? "尚未設定" : _settings.DFolder;
        includeSubfoldersCheck.IsChecked = _settings.IncludeSubfolders;
        RefreshAll();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshAll();

    private void IncludeSubfolders_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.IncludeSubfolders = includeSubfoldersCheck.IsChecked == true;
        _settingsService.Save(_settings);
        RefreshSource();
    }

    private void RefreshAll()
    {
        RefreshSource();
        Replace(_aItems, _catalog.Scan(_settings.AFolder, false, _settings.EnabledExtensions));
        Replace(_dItems, _catalog.Scan(_settings.DFolder, false, _settings.EnabledExtensions));
        statusText.Text = $"來源 {_sourceItems.Count} 個，A {_aItems.Count} 個，D {_dItems.Count} 個";
    }

    private void RefreshSource()
    {
        var exclusions = new[] { _settings.AFolder, _settings.DFolder };
        Replace(_sourceItems, _catalog.Scan(_settings.SourceFolder, _settings.IncludeSubfolders,
            _settings.EnabledExtensions, exclusions));
        sourceCountText.Text = _sourceItems.Count.ToString();
        sourcePathText.Text = string.IsNullOrWhiteSpace(_settings.SourceFolder) ? "尚未選擇來源資料夾" : _settings.SourceFolder;
    }

    private static void Replace(ObservableCollection<MediaItem> target, IEnumerable<MediaItem> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    private void SourceList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sourceList.SelectedItem is MediaItem item) ShowPreview(item, PreviewCollection.Source);
    }

    private void SourceList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sourceList.SelectedItem is MediaItem item) ShowPreview(item, PreviewCollection.Source, true);
    }

    private void TargetList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender == aList && aList.SelectedItem is MediaItem aItem) ShowPreview(aItem, PreviewCollection.A, true);
        if (sender == dList && dList.SelectedItem is MediaItem dItem) ShowPreview(dItem, PreviewCollection.D, true);
    }

    private void ShowPreview(MediaItem item, PreviewCollection collection, bool autoPlay = false)
    {
        StopAndReleasePreview();
        _currentItem = item;
        _previewCollection = collection;
        currentFileText.Text = item.Name;
        emptyPreview.Visibility = Visibility.Collapsed;

        try
        {
            if (item.Kind == MediaKind.Image)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(item.FullPath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();
                imagePreview.Source = bitmap;
                imagePreview.Visibility = Visibility.Visible;
                playPauseButton.IsEnabled = false;
                statusText.Text = $"預覽照片：{item.Name}";
            }
            else
            {
                videoPreview.Source = new Uri(item.FullPath, UriKind.Absolute);
                videoPreview.Visibility = Visibility.Visible;
                videoPreview.Pause();
                playPauseButton.IsEnabled = true;
                if (autoPlay) PlayVideo();
                statusText.Text = $"預覽影片：{item.Name}";
            }
        }
        catch (Exception ex)
        {
            statusText.Text = $"無法預覽：{ex.Message}";
        }
    }

    private void StopAndReleasePreview()
    {
        _positionTimer.Stop();
        try { videoPreview.Stop(); } catch { }
        videoPreview.Source = null;
        videoPreview.Visibility = Visibility.Collapsed;
        imagePreview.Source = null;
        imagePreview.Visibility = Visibility.Collapsed;
        _isPlaying = false;
        playPauseButton.Content = "▶ 播放";
        playPauseButton.IsEnabled = false;
        seekSlider.IsEnabled = false;
        seekSlider.Value = 0;
        currentTimeText.Text = "00:00";
        durationText.Text = "00:00";
    }

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_currentItem?.Kind != MediaKind.Video) return;
        if (_isPlaying) PauseVideo(); else PlayVideo();
    }

    private void PlayVideo()
    {
        videoPreview.Play();
        _isPlaying = true;
        playPauseButton.Content = "❚❚ 暫停";
        _positionTimer.Start();
    }

    private void PauseVideo()
    {
        videoPreview.Pause();
        _isPlaying = false;
        playPauseButton.Content = "▶ 播放";
    }

    private void VideoPreview_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (!videoPreview.NaturalDuration.HasTimeSpan) return;
        seekSlider.Maximum = videoPreview.NaturalDuration.TimeSpan.TotalSeconds;
        seekSlider.IsEnabled = true;
        durationText.Text = FormatTime(videoPreview.NaturalDuration.TimeSpan);
    }

    private void VideoPreview_MediaEnded(object sender, RoutedEventArgs e)
    {
        _isPlaying = false;
        playPauseButton.Content = "▶ 播放";
        _positionTimer.Stop();
    }

    private void VideoPreview_MediaFailed(object sender, ExceptionRoutedEventArgs e)
    {
        statusText.Text = $"影片無法播放：{e.ErrorException.Message}";
        PauseVideo();
    }

    private void PositionTimer_Tick(object? sender, EventArgs e)
    {
        if (_isSeeking || _currentItem?.Kind != MediaKind.Video) return;
        seekSlider.Value = videoPreview.Position.TotalSeconds;
        currentTimeText.Text = FormatTime(videoPreview.Position);
    }

    private void Seek_DragStarted(object sender, DragStartedEventArgs e) => _isSeeking = true;
    private void Seek_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        videoPreview.Position = TimeSpan.FromSeconds(seekSlider.Value);
        currentTimeText.Text = FormatTime(videoPreview.Position);
        _isSeeking = false;
    }

    private static string FormatTime(TimeSpan value) => value.TotalHours >= 1
        ? value.ToString(@"hh\:mm\:ss") : value.ToString(@"mm\:ss");

    private void Previous_Click(object sender, RoutedEventArgs e) => Navigate(-1);
    private void Next_Click(object sender, RoutedEventArgs e) => Navigate(1);

    private void Navigate(int offset)
    {
        var (items, list) = GetActiveCollection();
        if (items.Count == 0) return;
        var index = _currentItem is null ? 0 : items.IndexOf(_currentItem);
        if (index < 0) index = 0; else index = Math.Clamp(index + offset, 0, items.Count - 1);
        if (list.SelectedIndex == index)
            ShowPreview(items[index], _previewCollection);
        else
            list.SelectedIndex = index;
        list.ScrollIntoView(list.SelectedItem);
    }

    private (ObservableCollection<MediaItem> Items, ListBox List) GetActiveCollection() => _previewCollection switch
    {
        PreviewCollection.A => (_aItems, aList),
        PreviewCollection.D => (_dItems, dList),
        _ => (_sourceItems, sourceList)
    };

    private async void MoveToA_Click(object sender, RoutedEventArgs e) => await MoveCurrentAsync(_settings.AFolder, PreviewCollection.A);
    private async void MoveToD_Click(object sender, RoutedEventArgs e) => await MoveCurrentAsync(_settings.DFolder, PreviewCollection.D);

    private async Task MoveCurrentAsync(string destinationFolder, PreviewCollection destinationCollection)
    {
        if (_isBusy || _currentItem is null) return;
        if (string.IsNullOrWhiteSpace(destinationFolder)) { OpenSettings(); return; }
        if (_previewCollection == destinationCollection)
        {
            statusText.Text = "檔案已在這個分類資料夾中。";
            return;
        }
        _isBusy = true;
        var item = _currentItem;
        var oldCollection = _previewCollection;
        var oldIndex = GetActiveCollection().Items.IndexOf(item);
        try
        {
            StopAndReleasePreview();
            await Task.Delay(120);
            var destination = _moves.MoveToFolder(item.FullPath, destinationFolder);
            _currentItem = null;
            RefreshAll();
            statusText.Text = $"已分類：{Path.GetFileName(destination)}";
            SelectAfterMove(oldCollection, oldIndex);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "搬移失敗", MessageBoxButton.OK, MessageBoxImage.Error);
            RefreshAll();
        }
        finally { _isBusy = false; }
    }

    private void SelectAfterMove(PreviewCollection oldCollection, int oldIndex)
    {
        _previewCollection = oldCollection;
        var (items, list) = GetActiveCollection();
        if (items.Count == 0)
        {
            emptyPreview.Visibility = Visibility.Visible;
            currentFileText.Text = "已完成此清單";
            return;
        }
        var index = Math.Clamp(oldIndex, 0, items.Count - 1);
        if (list.SelectedIndex == index)
            ShowPreview(items[index], oldCollection);
        else
            list.SelectedIndex = index;
    }

    private void MoveAItemToD_Click(object sender, RoutedEventArgs e) => MoveTargetItem(aList, _settings.DFolder);
    private void MoveDItemToA_Click(object sender, RoutedEventArgs e) => MoveTargetItem(dList, _settings.AFolder);
    private void RestoreAItem_Click(object sender, RoutedEventArgs e) => RestoreTargetItem(aList);
    private void RestoreDItem_Click(object sender, RoutedEventArgs e) => RestoreTargetItem(dList);

    private async void MoveTargetItem(ListBox list, string destinationFolder)
    {
        if (list.SelectedItem is not MediaItem item || _isBusy) return;
        _isBusy = true;
        try
        {
            if (_currentItem?.FullPath == item.FullPath) { StopAndReleasePreview(); _currentItem = null; await Task.Delay(120); }
            var destination = _moves.MoveToFolder(item.FullPath, destinationFolder);
            RefreshAll();
            statusText.Text = $"已重新分類：{Path.GetFileName(destination)}";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "重新分類失敗", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { _isBusy = false; }
    }

    private async void RestoreTargetItem(ListBox list)
    {
        if (list.SelectedItem is not MediaItem item || _isBusy) return;
        _isBusy = true;
        try
        {
            if (_currentItem?.FullPath == item.FullPath) { StopAndReleasePreview(); _currentItem = null; await Task.Delay(120); }
            var destination = _moves.Restore(item.FullPath);
            RefreshAll();
            statusText.Text = $"已還原：{destination}";
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "還原失敗", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { _isBusy = false; }
    }

    private void TargetList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var element = e.OriginalSource as DependencyObject;
        while (element is not null && element is not ListBoxItem) element = VisualTreeHelper.GetParent(element);
        if (element is ListBoxItem item) item.IsSelected = true;
    }

    private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_isBusy || Keyboard.Modifiers != ModifierKeys.None || e.OriginalSource is TextBox) return;
        switch (e.Key)
        {
            case Key.A: e.Handled = true; await MoveCurrentAsync(_settings.AFolder, PreviewCollection.A); break;
            case Key.D: e.Handled = true; await MoveCurrentAsync(_settings.DFolder, PreviewCollection.D); break;
            case Key.Left: e.Handled = true; Navigate(-1); break;
            case Key.Right: e.Handled = true; Navigate(1); break;
            case Key.Space when _currentItem?.Kind == MediaKind.Video: e.Handled = true; if (_isPlaying) PauseVideo(); else PlayVideo(); break;
        }
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _settings.IncludeSubfolders = includeSubfoldersCheck.IsChecked == true;
        _settingsService.Save(_settings);
        StopAndReleasePreview();
    }
}
