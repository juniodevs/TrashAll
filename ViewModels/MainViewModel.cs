using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TrashAll.Models;
using TrashAll.Services;

namespace TrashAll.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly MediaScannerService _scanner = new();
        private readonly MediaLoaderService _loader = new();
        private readonly ZipInspectorService _zipInspector = new();
        private readonly TrashService _trash = new();
        private readonly DuplicateDetectorService _duplicateDetector = new();

        private CancellationTokenSource? _scanCts;
        private CancellationTokenSource? _loadCts;
        private CancellationTokenSource? _duplicateCts;

        private readonly List<MediaItem> _masterList = new();
        private readonly Stack<UndoItem> _undoStack = new();
        private System.Windows.Threading.DispatcherTimer? _toastTimer;

        [ObservableProperty]
        private string? _currentFolderPath;

        [ObservableProperty]
        private ObservableCollection<MediaItem> _items = new();

        [ObservableProperty]
        private int _currentIndex = -1;

        [ObservableProperty]
        private MediaItem? _currentItem;

        [ObservableProperty]
        private BitmapSource? _currentImageSource;

        [ObservableProperty]
        private string? _currentVideoSource;

        [ObservableProperty]
        private ZipPreviewData? _currentZipPreview;

        [ObservableProperty]
        private bool _isImageMode;

        [ObservableProperty]
        private bool _isVideoMode;

        [ObservableProperty]
        private bool _isZipMode;

        [ObservableProperty]
        private bool _isPdfItem;

        [ObservableProperty]
        private bool _isGenericFileMode;

        [ObservableProperty]
        private bool _isLoadingMedia;

        [ObservableProperty]
        private bool _isScanning;

        [ObservableProperty]
        private bool _includeSubfolders = true;

        // Alternância de Telas: Visualizador (false) vs Modo Lista (true)
        [ObservableProperty]
        private bool _isListViewMode = false;

        // Categorias e Contadores
        [ObservableProperty]
        private string _selectedCategory = "Todos";

        [ObservableProperty]
        private string _selectedSort = "Maior Tamanho";

        [ObservableProperty]
        private string _counterText = "0 / 0";

        [ObservableProperty]
        private int _deletedCount = 0;

        [ObservableProperty]
        private long _totalFreedBytes = 0;

        [ObservableProperty]
        private string _freedSpaceText = "0 B liberados";

        [ObservableProperty]
        private string _statusMessage = "Pronto";

        // Contadores por Categoria
        [ObservableProperty]
        private int _countTotal = 0;

        [ObservableProperty]
        private int _countImages = 0;

        [ObservableProperty]
        private int _countVideos = 0;

        [ObservableProperty]
        private int _countExecutables = 0;

        [ObservableProperty]
        private int _countArchives = 0;

        [ObservableProperty]
        private int _countDocuments = 0;

        [ObservableProperty]
        private int _countOthers = 0;

        [ObservableProperty]
        private int _countDuplicates = 0;

        [ObservableProperty]
        private string _duplicateWastedText = string.Empty;

        // Filtros Rápidos e Busca
        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private string _selectedSizeFilter = "Todos";

        // Desfazer (Ctrl+Z) e Toast
        [ObservableProperty]
        private bool _canUndo = false;

        [ObservableProperty]
        private bool _isUndoToastVisible = false;

        [ObservableProperty]
        private string _undoToastMessage = string.Empty;

        public MainViewModel()
        {
            _toastTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            _toastTimer.Tick += (s, e) =>
            {
                _toastTimer.Stop();
                IsUndoToastVisible = false;
            };
        }

        public void ShowUndoToast(int milliseconds = 5000)
        {
            if (_toastTimer != null)
            {
                _toastTimer.Stop();
                _toastTimer.Interval = TimeSpan.FromMilliseconds(milliseconds);
                _toastTimer.Start();
            }
            IsUndoToastVisible = true;
        }

        [RelayCommand]
        public void HideUndoToast()
        {
            _toastTimer?.Stop();
            IsUndoToastVisible = false;
        }

        public async Task LoadFolderAsync(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return;

            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;

            CurrentFolderPath = folderPath;
            IsScanning = true;
            StatusMessage = "Varrendo pasta...";

            _masterList.Clear();
            Items.Clear();
            CurrentIndex = -1;
            CurrentItem = null;
            CurrentImageSource = null;
            CurrentVideoSource = null;
            CurrentZipPreview = null;
            IsImageMode = false;
            IsVideoMode = false;
            IsZipMode = false;
            IsPdfItem = false;
            IsGenericFileMode = false;
            IsLoadingMedia = false;
            CounterText = "0 / 0";
            _loader.ClearCache();

            _undoStack.Clear();
            CanUndo = false;
            HideUndoToast();

            ResetCategoryCounts();
            bool isFirstItem = true;

            try
            {
                await _scanner.ScanDirectoryAsync(
                    folderPath,
                    recursive: IncludeSubfolders,
                    onBatchFound: batch =>
                    {
                        if (token.IsCancellationRequested) return;

                        _masterList.AddRange(batch);

                        Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (var item in batch)
                            {
                                IncrementCategoryCount(item.Type);

                                if (MatchesFilter(item))
                                {
                                    Items.Add(item);
                                }
                            }

                            CountTotal = _masterList.Count;

                            if (isFirstItem && Items.Count > 0)
                            {
                                isFirstItem = false;
                                CurrentIndex = 0;
                            }

                            UpdateCounter();
                        });
                    },
                    token);

                StatusMessage = $"{_masterList.Count} arquivos indexados.";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Varredura cancelada.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro: {ex.Message}";
            }
            finally
            {
                IsScanning = false;
                _ = TriggerDuplicateDetectionAsync();
            }
        }

        private async Task TriggerDuplicateDetectionAsync()
        {
            _duplicateCts?.Cancel();
            _duplicateCts = new CancellationTokenSource();
            var token = _duplicateCts.Token;

            try
            {
                var res = await _duplicateDetector.DetectDuplicatesAsync(_masterList, token);
                if (!token.IsCancellationRequested)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        CountDuplicates = res.TotalDuplicateFiles;
                        DuplicateWastedText = res.TotalWastedBytes > 0 
                            ? $"{res.FormattedWastedBytes} em {res.TotalDuplicateCopies} cópias"
                            : string.Empty;

                        if (SelectedCategory == "Duplicados")
                        {
                            ApplyFilterAndSort();
                        }
                    });
                }
            }
            catch
            {
                // Processo cancelado ou erro menor na leitura de arquivo
            }
        }

        private void ResetCategoryCounts()
        {
            CountTotal = 0;
            CountImages = 0;
            CountVideos = 0;
            CountExecutables = 0;
            CountArchives = 0;
            CountDocuments = 0;
            CountOthers = 0;
            CountDuplicates = 0;
            DuplicateWastedText = string.Empty;
        }

        private void IncrementCategoryCount(MediaType type)
        {
            switch (type)
            {
                case MediaType.Image: CountImages++; break;
                case MediaType.Video:
                case MediaType.Audio: CountVideos++; break;
                case MediaType.Executable: CountExecutables++; break;
                case MediaType.Archive: CountArchives++; break;
                case MediaType.Document: CountDocuments++; break;
                default: CountOthers++; break;
            }
        }

        private void RecalculateCategoryCounts()
        {
            CountTotal = _masterList.Count;
            CountImages = _masterList.Count(x => x.Type == MediaType.Image);
            CountVideos = _masterList.Count(x => x.Type == MediaType.Video || x.Type == MediaType.Audio);
            CountExecutables = _masterList.Count(x => x.Type == MediaType.Executable);
            CountArchives = _masterList.Count(x => x.Type == MediaType.Archive);
            CountDocuments = _masterList.Count(x => x.Type == MediaType.Document);
            CountOthers = _masterList.Count(x => x.Type == MediaType.Other);
            CountDuplicates = _masterList.Count(x => x.IsDuplicate);
        }

        partial void OnCurrentIndexChanged(int value)
        {
            if (value >= 0 && value < Items.Count)
            {
                CurrentItem = Items[value];
                UpdateCounter();
                _ = DisplayMediaAsync(CurrentItem);
            }
            else
            {
                CurrentItem = null;
                CurrentImageSource = null;
                CurrentVideoSource = null;
                CurrentZipPreview = null;
                IsImageMode = false;
                IsVideoMode = false;
                IsZipMode = false;
                IsPdfItem = false;
                IsGenericFileMode = false;
                IsLoadingMedia = false;
                CounterText = "0 / 0";
            }
        }

        private async Task DisplayMediaAsync(MediaItem? item)
        {
            if (item == null) return;

            _loadCts?.Cancel();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            IsLoadingMedia = true;

            try
            {
                bool isPdf = string.Equals(item.Extension, ".PDF", StringComparison.OrdinalIgnoreCase);
                bool isZip = string.Equals(item.Extension, ".ZIP", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(item.Extension, ".JAR", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(item.Extension, ".APK", StringComparison.OrdinalIgnoreCase);

                IsPdfItem = isPdf;

                if (item.Type == MediaType.Video)
                {
                    CurrentImageSource = null;
                    CurrentZipPreview = null;
                    IsImageMode = false;
                    IsZipMode = false;
                    IsGenericFileMode = false;
                    CurrentVideoSource = item.FilePath;
                    IsVideoMode = true;
                }
                else if (item.Type == MediaType.Image || isPdf)
                {
                    CurrentVideoSource = null;
                    CurrentZipPreview = null;
                    CurrentImageSource = null;
                    IsVideoMode = false;
                    IsZipMode = false;

                    var bmp = await _loader.LoadBitmapAsync(item, token);
                    if (!token.IsCancellationRequested)
                    {
                        if (bmp != null)
                        {
                            CurrentImageSource = bmp;
                            IsGenericFileMode = false;
                            IsImageMode = true;
                        }
                        else
                        {
                            IsImageMode = false;
                            IsGenericFileMode = true;
                        }
                    }
                }
                else if (isZip)
                {
                    CurrentVideoSource = null;
                    CurrentImageSource = null;
                    CurrentZipPreview = null;
                    IsVideoMode = false;
                    IsImageMode = false;

                    var zipData = await _zipInspector.InspectZipAsync(item.FilePath, token);
                    if (!token.IsCancellationRequested)
                    {
                        if (zipData != null)
                        {
                            CurrentZipPreview = zipData;
                            IsGenericFileMode = false;
                            IsZipMode = true;
                        }
                        else
                        {
                            IsZipMode = false;
                            IsGenericFileMode = true;
                        }
                    }
                }
                else
                {
                    CurrentVideoSource = null;
                    CurrentImageSource = null;
                    CurrentZipPreview = null;
                    IsImageMode = false;
                    IsVideoMode = false;
                    IsZipMode = false;
                    IsGenericFileMode = true;
                }

                try { PreloadNext(); } catch { }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Erro ao exibir mídia: {ex.Message}");
            }
            finally
            {
                if (!token.IsCancellationRequested)
                {
                    IsLoadingMedia = false;
                }
            }
        }

        private void PreloadNext()
        {
            int nextIdx = CurrentIndex + 1;
            if (nextIdx < Items.Count)
            {
                var next = Items[nextIdx];
                if (next.Type == MediaType.Image || string.Equals(next.Extension, ".PDF", StringComparison.OrdinalIgnoreCase))
                {
                    _ = _loader.LoadBitmapAsync(next, CancellationToken.None);
                }
            }
        }

        [RelayCommand]
        public void ToggleViewMode()
        {
            IsListViewMode = !IsListViewMode;
        }

        [RelayCommand]
        public void SetCategory(string category)
        {
            SelectedCategory = category;
        }

        [RelayCommand]
        public void Next()
        {
            if (Items.Count == 0) return;
            if (CurrentIndex < Items.Count - 1)
                CurrentIndex++;
            else
                CurrentIndex = 0;
        }

        [RelayCommand]
        public void Previous()
        {
            if (Items.Count == 0) return;
            if (CurrentIndex > 0)
                CurrentIndex--;
            else
                CurrentIndex = Items.Count - 1;
        }

        [RelayCommand]
        public async Task DeleteCurrentAsync()
        {
            if (CurrentItem == null) return;
            await DeleteItemAsync(CurrentItem);
        }

        public async Task DeleteItemAsync(MediaItem itemToDelete)
        {
            int deletedIdx = Items.IndexOf(itemToDelete);
            if (deletedIdx < 0) return;

            if (IsVideoMode && CurrentItem == itemToDelete)
            {
                CurrentVideoSource = null;
                IsVideoMode = false;
            }

            _loader.InvalidateCache(itemToDelete.FilePath);

            // Armazena na pilha de Desfazer
            _undoStack.Push(new UndoItem(itemToDelete, deletedIdx));
            CanUndo = true;
            UndoToastMessage = $"🗑️ '{itemToDelete.FileName}' movido para a Lixeira.";
            ShowUndoToast();

            // Atualização otimista imediata
            DeletedCount++;
            TotalFreedBytes += itemToDelete.FileSizeBytes;
            FreedSpaceText = $"{MediaItem.FormatBytes(TotalFreedBytes)} liberados ({DeletedCount} itens)";

            _masterList.Remove(itemToDelete);
            Items.RemoveAt(deletedIdx);

            // Atualiza e decrementa os contadores de categoria e total imediatamente
            RecalculateCategoryCounts();

            if (Items.Count == 0)
            {
                CurrentIndex = -1;
                CurrentItem = null;
                CurrentImageSource = null;
                CurrentVideoSource = null;
                CurrentZipPreview = null;
                IsImageMode = false;
                IsVideoMode = false;
                IsZipMode = false;
                IsPdfItem = false;
                IsGenericFileMode = false;
                UpdateCounter();
            }
            else if (deletedIdx >= Items.Count)
            {
                CurrentIndex = Items.Count - 1;
                CurrentItem = Items[CurrentIndex];
                UpdateCounter();
                _ = DisplayMediaAsync(CurrentItem);
            }
            else
            {
                CurrentIndex = deletedIdx;
                CurrentItem = Items[deletedIdx];
                UpdateCounter();
                _ = DisplayMediaAsync(CurrentItem);
            }

            StatusMessage = $"🗑️ '{itemToDelete.FileName}' enviado para a Lixeira.";

            var (success, err) = await _trash.MoveToRecycleBinAsync(itemToDelete.FilePath);
            if (!success)
            {
                StatusMessage = $"⚠️ Erro ao mover para a lixeira: {err}";
            }
        }

        [RelayCommand]
        public async Task UndoDeleteAsync()
        {
            if (_undoStack.Count == 0) return;

            var last = _undoStack.Pop();
            CanUndo = _undoStack.Count > 0;

            StatusMessage = $"Restaurando '{last.Item.FileName}'...";

            var (success, err) = await _trash.RestoreFromRecycleBinAsync(last.Item.FilePath);
            if (success)
            {
                int insertMaster = Math.Min(last.OriginalIndex, _masterList.Count);
                _masterList.Insert(insertMaster, last.Item);

                DeletedCount = Math.Max(0, DeletedCount - 1);
                TotalFreedBytes = Math.Max(0, TotalFreedBytes - last.Item.FileSizeBytes);
                FreedSpaceText = DeletedCount > 0 
                    ? $"{MediaItem.FormatBytes(TotalFreedBytes)} liberados ({DeletedCount} itens)" 
                    : "0 B liberados";

                RecalculateCategoryCounts();

                if (MatchesFilter(last.Item))
                {
                    int insertIdx = Math.Min(last.OriginalIndex, Items.Count);
                    Items.Insert(insertIdx, last.Item);
                    CurrentIndex = insertIdx;
                }

                StatusMessage = $"↩️ '{last.Item.FileName}' restaurado da Lixeira com sucesso!";
                UndoToastMessage = $"↩️ '{last.Item.FileName}' restaurado!";
                ShowUndoToast(2500);
            }
            else
            {
                StatusMessage = $"⚠️ Não foi possível restaurar: {err}";
                HideUndoToast();
            }
        }

        [RelayCommand]
        public void ClearSearch()
        {
            SearchQuery = string.Empty;
        }

        [RelayCommand]
        public void SetSizeFilter(string size)
        {
            SelectedSizeFilter = size;
        }

        [RelayCommand]
        public void OpenInExplorer()
        {
            if (CurrentItem == null || !File.Exists(CurrentItem.FilePath))
                return;

            try
            {
                Process.Start("explorer.exe", $"/select,\"{CurrentItem.FilePath}\"");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erro ao abrir Explorer: {ex.Message}";
            }
        }

        private bool MatchesFilter(MediaItem item)
        {
            // 1. Categoria
            bool matchesCategory = SelectedCategory switch
            {
                "Imagens" => item.Type == MediaType.Image,
                "Vídeos" => item.Type == MediaType.Video || item.Type == MediaType.Audio,
                "Executáveis" => item.Type == MediaType.Executable,
                "Compactados" => item.Type == MediaType.Archive,
                "Documentos" => item.Type == MediaType.Document,
                "Outros" => item.Type == MediaType.Other,
                "Duplicados" => item.IsDuplicate,
                _ => true
            };

            if (!matchesCategory) return false;

            // 2. Filtro Rápido por Tamanho
            bool matchesSize = SelectedSizeFilter switch
            {
                "> 1 GB" => item.FileSizeBytes >= 1024L * 1024 * 1024,
                "> 100 MB" => item.FileSizeBytes >= 100L * 1024 * 1024,
                "> 10 MB" => item.FileSizeBytes >= 10L * 1024 * 1024,
                "< 5 MB" => item.FileSizeBytes <= 5L * 1024 * 1024,
                _ => true
            };

            if (!matchesSize) return false;

            // 3. Busca em Tempo Real
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                string q = SearchQuery.Trim();
                bool matchesSearch = item.FileName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                     item.Extension.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                     item.DirectoryPath.Contains(q, StringComparison.OrdinalIgnoreCase);
                if (!matchesSearch) return false;
            }

            return true;
        }

        partial void OnSearchQueryChanged(string value)
        {
            ApplyFilterAndSort();
        }

        partial void OnSelectedSizeFilterChanged(string value)
        {
            ApplyFilterAndSort();
        }

        partial void OnSelectedCategoryChanged(string value)
        {
            ApplyFilterAndSort();
        }

        partial void OnSelectedSortChanged(string value)
        {
            ApplyFilterAndSort();
        }

        partial void OnIncludeSubfoldersChanged(bool value)
        {
            if (!string.IsNullOrEmpty(CurrentFolderPath))
            {
                _ = LoadFolderAsync(CurrentFolderPath);
            }
        }

        private void ApplyFilterAndSort()
        {
            var filtered = _masterList.Where(MatchesFilter);

            IEnumerable<MediaItem> sorted = SelectedCategory == "Duplicados"
                ? filtered.OrderBy(x => x.DuplicateGroupId).ThenBy(x => x.IsDuplicateCopy).ThenBy(x => x.FileName)
                : SelectedSort switch
                {
                    "Maior Tamanho" => filtered.OrderByDescending(x => x.FileSizeBytes),
                    "Mais Recente" => filtered.OrderByDescending(x => x.DateModified),
                    "Nome (A-Z)" => filtered.OrderBy(x => x.FileName, StringComparer.OrdinalIgnoreCase),
                    _ => filtered
                };

            var list = sorted.ToList();
            Items = new ObservableCollection<MediaItem>(list);

            if (Items.Count > 0)
            {
                CurrentIndex = 0;
            }
            else
            {
                CurrentIndex = -1;
                CurrentItem = null;
                CurrentImageSource = null;
                CurrentVideoSource = null;
                CurrentZipPreview = null;
                IsImageMode = false;
                IsVideoMode = false;
                IsZipMode = false;
                IsPdfItem = false;
                IsGenericFileMode = false;
                IsLoadingMedia = false;
                CounterText = "0 / 0";
            }
        }

        private void UpdateCounter()
        {
            if (Items.Count == 0 || CurrentIndex < 0)
            {
                CounterText = "0 / 0";
            }
            else
            {
                CounterText = $"{CurrentIndex + 1} / {Items.Count}";
            }
        }
    }
}
