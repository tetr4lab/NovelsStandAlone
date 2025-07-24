using System.Text;
using System.Net.Http;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Novels.Data;
using Novels.Services;
using Tetr4lab;

namespace Novels.Components.Pages;

public class BookListBase : ItemListBase<Book> {
    [Inject] protected HttpClient HttpClient { get; set; } = null!;

    /// <summary>書籍を追加する</summary>
    protected virtual async Task AddBook () {
        if (IsDirty || items is not List<Book> books) { return; }
        await SetBusyAsync ();
        try {
            var url = await JSRuntime.GetClipboardText ();
            // urlを修正する機会を与えるダイアログを表示
            var dialogResult = await DialogService.OpenAddItemDialog<Book> (
                message: $"取得先URLを確認して{Book.TableLabel}の追加を完了してください。",
                label: "URL",
                value: url,
                onOpend: SetIdleAsync
            );
            if (dialogResult is not null && !dialogResult.Canceled && dialogResult.Data is string newUrl && !string.IsNullOrEmpty (newUrl)) {
                newUrl = newUrl.Trim ();
                // 既存のURLと比較する
                var existingBook = books?.FirstOrDefault (x => x.Url1 == newUrl || x.Url2 == newUrl);
                if (existingBook is not null) {
                    Snackbar.Add ($"既存の{Book.TableLabel}: 『{existingBook.Title}』", Severity.Warning);
                    await ChangeCurrentBookAsync (existingBook);
                    await ScrollToCurrentAsync ();
                    return;
                }
                // オーバーレイ
                await SetBusyAsync ();
                // 入力されたurlからあたらしいBookに情報を取得、DBへ追加・選択する
                var result = await DataSet.UpdateBookFromSiteAsync (HttpClient, newUrl, UserIdentifier);
                foreach (var issue in result.Value.issues) {
                    Snackbar.Add (issue, Severity.Error);
                }
                if (result.IsSuccess) {
                    var newBook = result.Value.book;
                    await ChangeCurrentBookAsync (newBook);
                    // Issueページへ移動する
                    await SetAppMode (AppMode.Issue);
                } else {
                    Snackbar.Add ($"{Book.TableLabel}の追加に失敗しました。", Severity.Error);
                }
            }
        }
        catch (Exception ex) {
            Snackbar.Add ($"Exception: {ex.Message}", Severity.Error);
        }
        finally {
            await SetIdleAsync ();
        }
    }
}
