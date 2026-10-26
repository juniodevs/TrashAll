using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using TrashAll.Models;
using TrashAll.ViewModels;
using Wpf.Ui.Controls;

namespace TrashAll
{
    public partial class MainWindow : FluentWindow
    {
        public MainViewModel ViewModel { get; }
        private readonly DispatcherTimer _videoTimer;
        private bool _isUserDraggingSlider = false;
        private bool _isVideoPlaying = false;
        private WindowState _previousWindowState = WindowState.Normal;
        private WindowStyle _previousWindowStyle = WindowStyle.SingleBorderWindow;

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainViewModel();
            DataContext = ViewModel;

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            _videoTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _videoTimer.Tick += VideoTimer_Tick;

            Loaded += (s, e) => Focus();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (e.ClickCount == 2)
                {
                    WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                }
                else
                {
                    DragMove();
                }
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.CurrentVideoSource))
            {
                if (!string.IsNullOrEmpty(ViewModel.CurrentVideoSource))
                {
                    try
                    {
                        VideoPlayer.Source = new Uri(ViewModel.CurrentVideoSource);
                        VideoPlayer.Play();
                        _isVideoPlaying = true;
                        BtnPlayPause.Content = "⏸️";
                        _videoTimer.Start();
                    }
                    catch
                    {
                    }
                }
                else
                {
                    _videoTimer.Stop();
                    VideoPlayer.Stop();
                    VideoPlayer.Source = null;
                    _isVideoPlaying = false;
                }
            }
        }

        private async void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Selecione a pasta para varrer",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                await ViewModel.LoadFolderAsync(dialog.FolderName);
            }
        }

        private void RbVisualizer_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsListViewMode = false;
        }

        private void RbList_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.IsListViewMode = true;
        }

        private async void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.Tag is MediaItem item)
            {
                await ViewModel.DeleteItemAsync(item);
            }
        }

        #region Player de Vídeo

        private void VideoPlayer_MediaOpened(object sender, RoutedEventArgs e)
        {
            if (VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var total = VideoPlayer.NaturalDuration.TimeSpan;
                VideoTimeline.Maximum = total.TotalSeconds;
                VideoTimeline.Value = 0;
                TxtVideoTime.Text = $"00:00 / {FormatTime(total)}";
            }
        }

        private void VideoPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            VideoPlayer.Position = TimeSpan.Zero;
            VideoPlayer.Play();
        }

        private void VideoTimer_Tick(object? sender, EventArgs e)
        {
            if (!_isUserDraggingSlider && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var pos = VideoPlayer.Position;
                var total = VideoPlayer.NaturalDuration.TimeSpan;
                VideoTimeline.Value = pos.TotalSeconds;
                TxtVideoTime.Text = $"{FormatTime(pos)} / {FormatTime(total)}";
            }
        }

        private void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            ToggleVideoPlayPause();
        }

        private void ToggleVideoPlayPause()
        {
            if (!ViewModel.IsVideoMode) return;

            if (_isVideoPlaying)
            {
                VideoPlayer.Pause();
                _isVideoPlaying = false;
                BtnPlayPause.Content = "▶️";
                _videoTimer.Stop();
            }
            else
            {
                VideoPlayer.Play();
                _isVideoPlaying = true;
                BtnPlayPause.Content = "⏸️";
                _videoTimer.Start();
            }
        }

        private void VideoTimeline_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _isUserDraggingSlider = true;
        }

        private void VideoTimeline_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            _isUserDraggingSlider = false;
            VideoPlayer.Position = TimeSpan.FromSeconds(VideoTimeline.Value);
        }

        private void VideoTimeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUserDraggingSlider && VideoPlayer.NaturalDuration.HasTimeSpan)
            {
                var cur = TimeSpan.FromSeconds(e.NewValue);
                var total = VideoPlayer.NaturalDuration.TimeSpan;
                TxtVideoTime.Text = $"{FormatTime(cur)} / {FormatTime(total)}";
            }
        }

        private static string FormatTime(TimeSpan ts)
        {
            return ts.TotalHours >= 1 
                ? ts.ToString(@"hh\:mm\:ss") 
                : ts.ToString(@"mm\:ss");
        }

        #endregion

        #region Atalhos de Teclado

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.TextBox) return;

            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (ViewModel.CanUndo)
                {
                    _ = ViewModel.UndoDeleteAsync();
                    e.Handled = true;
                }
                return;
            }

            switch (e.Key)
            {
                case Key.Right:
                case Key.D:
                    if (!ViewModel.IsListViewMode)
                    {
                        ViewModel.NextCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                case Key.Left:
                case Key.A:
                    if (!ViewModel.IsListViewMode)
                    {
                        ViewModel.PreviousCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                case Key.Delete:
                case Key.X:
                case Key.Back:
                    ViewModel.DeleteCurrentCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Space:
                    if (ViewModel.IsVideoMode && !ViewModel.IsListViewMode)
                    {
                        ToggleVideoPlayPause();
                        e.Handled = true;
                    }
                    break;

                case Key.F:
                case Key.F11:
                    ToggleFullscreen();
                    e.Handled = true;
                    break;
            }
        }

        private void ToggleFullscreen()
        {
            if (WindowStyle == WindowStyle.None)
            {
                WindowStyle = _previousWindowStyle;
                WindowState = _previousWindowState;
            }
            else
            {
                _previousWindowStyle = WindowStyle;
                _previousWindowState = WindowState;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
            }
        }

        #endregion

        #region Arraste e Solte (Drag & Drop)

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string path = files[0];
                    if (Directory.Exists(path))
                    {
                        await ViewModel.LoadFolderAsync(path);
                    }
                    else if (File.Exists(path))
                    {
                        string? parent = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(parent))
                        {
                            await ViewModel.LoadFolderAsync(parent);
                        }
                    }
                }
            }
        }

        #endregion
    }
}
