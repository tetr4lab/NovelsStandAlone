using System.ComponentModel;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

public class ItemListBase<T> : NovelsComponentBase, IDisposable where T : NovelsBaseModel<T>, INovelsBaseModel, new() {

    /// <summary>ページング機能の有効性</summary>
    protected const bool AllowPaging = true;

    /// <summary>列挙する最大数</summary>
    protected const int MaxListingNumber = int.MaxValue;

    [Inject] protected NovelsDataSet DataSet { get; set; } = null!;
    [Inject] protected IDialogService DialogService { get; set; } = null!;
    [Inject] protected ISnackbar Snackbar { get; set; } = null!;
    [Inject] protected IScrollManager ScrollManager { get; set; } = null!;
    [Inject] protected IBrowserViewportService BrowserViewportService { get; set; } = null!;
    [Inject] protected IJSRuntime JSRuntime { get; set; } = null!;

    /// <summary>UIロック</summary>
    protected async Task SetBusyAsync () {
        UiState.Lock ();
        StateHasChanged ();
        await TaskEx.DelayOneFrame;
    }

    /// <summary>UIアンロック</summary>
    protected async Task SetIdleAsync () {
        UiState.Unlock ();
        StateHasChanged ();
        await TaskEx.DelayOneFrame;
    }

    /// <summary>項目一覧</summary>
    protected List<T>? items => DataSet.IsReady ? DataSet.GetList<T> () : null;

    /// <summary>選択項目</summary>
    protected T SelectedItem { get; set; } = new ();

    /// <summary>初期化</summary>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        AppModeService.SetSectionTitle ($"{typeof (T).Name}s");
        if (typeof (T) == typeof (Book)) {
            if (items?.Count > 0) {
                var item = items.Find (item => item.Id == AppModeService.CurrentBookId);
                if (item is not null) {
                    SelectedItem = item;
                }
            }
        } else if (typeof (T) == typeof (Sheet)) {
            if (AppModeService.CurrentSheetIndex <= 0) {
                AppModeService.SetCurrentSheetIndex (1);
            }
            if (items?.Count >= AppModeService.CurrentSheetIndex) {
                SelectedItem = items [AppModeService.CurrentSheetIndex - 1];
            }
        } else if (typeof (T) == typeof (Setting)) {
            if (items?.Count > 0) {
                SelectedItem = items [0];
            }
        }
    }

    /// <summary>遅延初期化</summary>
    protected override async Task OnAfterRenderAsync (bool firstRender) {
        await base.OnAfterRenderAsync (firstRender);
        if (firstRender) {
            /// 初期アンロック
            if (UiState.IsLocked) {
                await ScrollToCurrentAsync ();
                UiState.Unlock ();
            }
        }
    }

    /// <summary>破棄</summary>
    public override void Dispose () {
        base.Dispose ();
        if (IsEditing) {
            Cancel ();
        }
    }

    //// <summary>着目書籍の変更</summary>
    protected virtual async Task ChangeCurrentBookAsync (Book book) {
        if (book is T item) {
            SelectedItem = item;
        }
        if (AppModeService.CurrentBookId != book.Id) {
            AppModeService.SetCurrentBookId (book.Id, 1);
        }
        // ToDo: asyncを外す
        await Task.Delay (0);
    }

    /// <summary>データグリッド</summary>
    protected MudDataGrid<T>? _dataGrid;

    /// <summary>バックアップ</summary>
    protected virtual T? BackupedItem { get; set; } = null;

    /// <summary>編集中</summary>
    protected bool IsEditing => BackupedItem is not null;

    /// <summary>編集完了</summary>
    protected virtual async Task<bool> Commit () {
        if (BackupedItem is not null) {
            if (!NovelsDataSet.EntityIsValid (SelectedItem)) {
                Snackbar.Add ($"{T.TableLabel}に不備があります。", Severity.Error);
            } else if (!BackupedItem.Equals (SelectedItem)) {
                SelectedItem.Modifier = UserIdentifier;
                var result = await DataSet.UpdateAsync (SelectedItem);
                if (result.IsSuccess) {
                    await ReloadAndFocus (SelectedItem.Id);
                    BackupedItem = null;
                    StateHasChanged ();
                    Snackbar.Add ($"{T.TableLabel}を更新しました。", Severity.Normal);
                    return true;
                } else {
                    Snackbar.Add ($"{T.TableLabel}を更新できませんでした。", Severity.Error);
                }
            }
        }
        return false;
    }

    /// <summary>編集取消</summary>
    protected virtual void Cancel () {
        if (BackupedItem?.Equals (SelectedItem) == false) {
            BackupedItem.CopyTo (SelectedItem);
        }
        BackupedItem = null;
        StateHasChanged ();
    }

    /// <summary>リストの着目項目へスクロール</summary>
    /// <param name="focusedId">書誌ID</param>
    /// <param name="focusedIndex">シートインデックス</param>
    protected virtual async Task ScrollToCurrentAsync (long focusedId = 0, int focusedIndex = 0) {
        if (focusedId <= 0) { focusedId = AppModeService.CurrentBookId; }
        if (focusedIndex <= 0) { focusedIndex = AppModeService.CurrentSheetIndex; }
        var viewportHeightRatio = 0.0d;
        if (items is not null) {
            var index = 0;
            if (typeof (T) == typeof (Book)) {
                var list = AppModeService.FilterText == "" || _dataGrid is null ? items : _dataGrid.FilteredItems.ToList ();
                index = list.FindIndex (x => x.Id == focusedId);
                viewportHeightRatio = Books.ViewportHeightRatio;
            } else if (typeof (T) == typeof (Sheet)) {
                index = AppModeService.CurrentSheetIndex - 1;
                viewportHeightRatio = Sheets.ViewportHeightRatio;
            } else {
                return;
            }
            var rowHeight = _dataGrid?.ItemSize ?? 0.0f; // 行高
            var viewSize = await BrowserViewportService.GetCurrentBrowserWindowSizeAsync ();
            var tableHeight = viewSize.Height / viewportHeightRatio; // テーブル高
            double offset = rowHeight * (double) index - tableHeight / 2.0;
            await ScrollManager.ScrollToAsync (".mud-table-container", 0, (int) ((offset < 0.0) ? 0.0 : offset), ScrollBehavior.Auto);
        }
    }

    /// <summary>着目へ</summary>
    protected async Task ScrollToCurrent () {
        await SetBusyAsync ();
        await ScrollToCurrentAsync ();
        await SetIdleAsync ();
    }

    /// <summary>リストの上端へスクロール</summary>
    protected virtual async Task ScrollToTopAsync () {
        await SetBusyAsync ();
        await ScrollManager.ScrollToTopAsync (".mud-table-container", ScrollBehavior.Auto);
        await SetIdleAsync ();
    }

    /// <summary>リストの下端へスクロール</summary>
    protected virtual async Task ScrollToBottomAsync () {
        await SetBusyAsync ();
        await ScrollManager.ScrollToBottomAsync (".mud-table-container", ScrollBehavior.Auto);
        await SetIdleAsync ();
    }

    /// <summary>リロードして元の位置へ戻る</summary>
    protected virtual async Task ReloadAndFocus (long focusedId, bool editing = false, bool force = false) {
        await DataSet.LoadAsync ();
        var item = DataSet.GetList<T> ().Find (item => item.Id == focusedId);
        if (item is not null) {
            SelectedItem = item;
        }
        if (editing || force) {
            StartEdit (force);
        }
        if (_dataGrid is not null) {
            await ScrollToCurrentAsync (focusedId: focusedId);
        }
    }

    /// <summary>全ての検索語に対して対象列のどれかが真であれば真を返す</summary>
    protected bool FilterFunc (T item) {
        if (item != null) {
            foreach (var word in AppModeService.FilterText.Split ([' ', '　', '\t', '\n'])) {
                if (!string.IsNullOrEmpty (word) && !Any (item.SearchTargets, word)) { return false; }
            }
            return true;
        }
        return false;
        // 対象カラムのどれかが検索語に適合すれば真を返す
        bool Any (IEnumerable<string?> targets, string word) {
            word = word.Replace ('\xA0', ' ').Replace ('␣', ' ');
            var eq = word.StartsWith ('=');
            var notEq = word.StartsWith ('!');
            var not = !notEq && word.StartsWith ('^');
            word = word [(not || eq || notEq ? 1 : 0)..];
            var or = word.Split ('|');
            foreach (var target in targets) {
                if (!string.IsNullOrEmpty (target)) {
                    if (eq || notEq) {
                        // 検索語が'='で始まる場合は、以降がカラムと完全一致する場合に真/偽を返す
                        if (or.Length > 1) {
                            // 検索語が'|'を含む場合は、'|'で分割したいずれかの部分と一致する場合に真/偽を返す
                            foreach (var wd in or) {
                                if (target == wd) { return eq; }
                            }
                        } else {
                            if (target == word) { return eq; }
                        }
                    } else {
                        // 検索語がカラムに含まれる場合に真/偽を返す
                        if (or.Length > 1) {
                            // 検索語が'|'を含む場合は、'|'で分割したいずれかの部分がカラムに含まれる場合に真/偽を返す
                            foreach (var wd in or) {
                                if (target.Contains (wd)) { return !not; }
                            }
                        } else {
                            if (target.Contains (word)) { return !not; }
                        }
                    }
                }
            }
            return notEq || not ? true : false;
        }
    }

    /// <summary>編集開始</summary>
    protected virtual void StartEdit (bool force = false) {
        if (force || !IsEditing) {
            BackupedItem = SelectedItem.Clone ();
        }
    }

    /// <summary>アプリモードが変化した</summary>
    protected override async void OnAppModeChanged (object? sender, PropertyChangedEventArgs e) {
        if (sender is NovelsAppModeService service) {
            if (e.PropertyName == "RequestedMode") {
                // アプリモード遷移の要求があった
                if (service.RequestedMode != AppMode.None) {
                    if (service.RequestedMode != service.CurrentMode) {
                        await SetAppMode (service.RequestedMode);
                    }
                    service.RequestMode (AppMode.None);
                }
            } else if (e.PropertyName == "FilterText") {
                // 検索文字列の変化
                if (_dataGrid is not null && service.FilterText != "") {
                    await InvokeAsync (StateHasChanged); // 反映を促す
                    await TaskEx.DelayOneFrame; // 反映を待つ
                    var filtered = _dataGrid.FilteredItems.ToList ();
                    if (filtered.Count > 0 && !filtered.Contains (SelectedItem)) {
                        // 現選択アイテムが結果にないなら最後のアイテムを選択
                        SelectedItem = filtered.Last ();
                        if (SelectedItem is Book book) {
                            await ChangeCurrentBookAsync (book);
                        }
                    }
                }
                await ScrollToCurrentAsync ();
            }
        }
    }

    /// <summary>アプリモード遷移実施</summary>
    protected virtual async Task SetAppMode (AppMode appMode) {
        if (AppModeService.CurrentMode != appMode && await ConfirmCancelEditAsync ()) {
            await SetBusyAsync ();
            if (DataSet.CurrentBookId != AppModeService.CurrentBookId) {
                // 遅延読み込み
                await DataSet.SetCurrentBookIdAsync (AppModeService.CurrentBookId);
            }
            AppModeService.SetMode (appMode);
        }
    }

    /// <summary>編集内容破棄の確認</summary>
    protected virtual async Task<bool> ConfirmCancelEditAsync () {
        if (IsDirty) {
            await SetBusyAsync ();
            var dialogResult = await DialogService.Confirmation ([$"編集内容を破棄して編集前の状態を復元します。", "　", $"破棄される{SelectedItem}", "　⬇", $"復元される{BackupedItem}",], title: $"{T.TableLabel}編集破棄", position: DialogPosition.BottomCenter, width: MaxWidth.Large, acceptionLabel: "破棄", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete, onOpend: SetIdleAsync);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                Cancel ();
                Snackbar.Add ($"{T.TableLabel}の編集内容を破棄して編集前の状態を復元しました。", Severity.Normal);
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>編集されている</summary>
    protected bool IsDirty => IsEditing && !SelectedItem.Equals (BackupedItem);

    /// <summary>復旧</summary>
    protected async Task RevertAsync () {
        if (await ConfirmCancelEditAsync ()) {
            StartEdit ();
        }
    }

    /// <summary>保存</summary>
    protected async Task SaveAsync () {
        if (IsEditing) {
            await SetBusyAsync ();
            if (await Commit ()) {
                StartEdit ();
            }
            await SetIdleAsync ();
        }
    }

}
