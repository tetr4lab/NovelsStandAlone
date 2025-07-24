using System.Text.RegularExpressions;
using System.Net.Http;
using System.IO;
using AngleSharp.Html.Parser;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using MimeKit;
using MimeKit.Text;
using MudBlazor;
using Novels.Data;
using Novels.Services;
using QuickEPUB;
using Tetr4lab;

namespace Novels.Components.Pages;

public partial class Issue : BookListBase {

    /// <summary>URI入力の検証</summary>
    protected string ValidateUri (string uri) => uri != "" && IsInvalidUri (uri) ? "bad uri" : "";

    /// <summary>無効なURI</summary>
    protected bool IsInvalidUri (string? url) => !Uri.IsWellFormedUriString (url, UriKind.Absolute);

    //// <summary>着目書籍の変更</summary>
    protected override async Task ChangeCurrentBookAsync (Book book) {
        await base.ChangeCurrentBookAsync (book);
        SetTitle ();
        StartEdit (true);
    }

    /// <summary>書籍の削除 (ホームへ遷移)</summary>
    protected async Task DeleteBook (MouseEventArgs eventArgs) {
        var complete = !eventArgs.CtrlKey;
        if (!complete && SelectedItem.IsEmpty) {
            Snackbar.Add ($"削除すべきシートがありません。", Severity.Warning);
            return;
        }
        await SetBusyAsync ();
        var target = complete ? $"{Book.TableLabel}と{Sheet.TableLabel}" : $"{Sheet.TableLabel}のみ";
        var dialogResult = await DialogService.Confirmation ([
            $"以下の{target}を完全に削除します。",
            SelectedItem.ToString (),
        ], title: $"{target}の削除", position: DialogPosition.BottomCenter, acceptionLabel: complete ? "完全削除" : "シートのみ削除", acceptionColor: complete ? Color.Error : Color.Secondary, acceptionIcon: Icons.Material.Filled.Delete, onOpend: SetIdleAsync);
        if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
            await SetBusyAsync ();
            if (complete) {
                var result = await DataSet.RemoveAsync (SelectedItem);
                if (result.IsSuccess) {
                    if (AppModeService.CurrentBookId == SelectedItem.Id) {
                        AppModeService.SetCurrentBookId (0, 1);
                    }
                    StateHasChanged ();
                    Snackbar.Add ($"{target}を削除しました。", Severity.Normal);
                    await SetAppMode (AppMode.Books);
                } else {
                    Snackbar.Add ($"{target}の削除に失敗しました。", Severity.Error);
                }
            } else {
                try {
                    // 元リストは要素が削除されるので複製でループする
                    var sheets = new List<Sheet> (SelectedItem.Sheets);
                    var success = 0;
                    var count = 0;
                    UiState.Lock (sheets.Count);
                    foreach (var sheet in sheets) {
                        UiState.UpdateProgress (++count);
                        if ((await DataSet.RemoveAsync (sheet)).IsSuccess) {
                            success++;
                        }
                    }
                    await ReloadAndFocus (editing: true);
                    if (success == sheets.Count) {
                        Snackbar.Add ($"{target}を削除しました。", Severity.Normal);
                    } else {
                        Snackbar.Add ($"{target}の一部({success}/{sheets.Count})を削除しました。", Severity.Error);
                    }
                }
                catch (Exception e) {
                    System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                    Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
                }
            }
            await SetIdleAsync ();
        }
    }

    /// <summary>取得と更新の確認</summary>
    protected async Task<bool> ConfirmUpdateBookAsync (MouseEventArgs eventArgs) {
        if (!IsDirty) {
            await SetBusyAsync ();
            var withSheets = !eventArgs.CtrlKey;
            var fullUpdate = eventArgs.ShiftKey || SelectedItem.IsEmpty;
            var operation = SelectedItem.IsEmpty ? "取得" : $"{(withSheets && fullUpdate ? "完全" : "")}更新";
            var target = $"{Book.TableLabel}{(withSheets ? $"と{Sheet.TableLabel}" : "のみ")}";
            var dialogResult = await DialogService.Confirmation ([$"『{SelectedItem.Title}』の{target}を{SelectedItem.Site}から{operation}します。", withSheets ? $"{Book.TableLabel}と{(fullUpdate ? "全ての" : "新しい")}{Sheet.TableLabel}を更新します。" : $"{Book.TableLabel}のみを更新し、{Sheet.TableLabel}は更新しません。"], title: $"{target}の{operation}", position: DialogPosition.BottomCenter, acceptionLabel: operation, acceptionColor: withSheets ? Color.Success : Color.Primary, acceptionIcon: Icons.Material.Filled.Download, onOpend: SetIdleAsync);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                // オーバーレイ
                await SetBusyAsync ();
                Snackbar.Add ($"{target}の{operation}を開始しました。", Severity.Normal);
                if (await UpdateBookFromSiteAsync (withSheets, fullUpdate)) {
                    Snackbar.Add ($"{target}を{operation}しました。", Severity.Normal);
                } else {
                    Snackbar.Add ($"{target}の{operation}に失敗しました。", Severity.Error);
                }
                await SetIdleAsync ();
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>発行の確認</summary>
    protected async Task<bool> ConfirmIssueBookAsync (MouseEventArgs eventArgs) {
        if (!IsDirty) {
            await SetBusyAsync ();
            var issue = !eventArgs.CtrlKey;
            var operation = issue ? "発行" : "生成";
            var dialogResult = await DialogService.Confirmation ([
                $"『{SelectedItem.MainTitle}.epub』を{(issue ? $"<{DataSet.Setting.SmtpMailto}>へ発行": "生成してダウンロード")}します。",
            ], title: $"『{SelectedItem.MainTitle}.epub』{operation}", position: DialogPosition.BottomCenter, acceptionLabel: operation, acceptionColor: issue ? Color.Success : Color.Primary, acceptionIcon: issue ? Icons.Material.Filled.Publish : Icons.Material.Filled.FileDownload, onOpend: SetIdleAsync);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                await SetBusyAsync ();
                await IssueBookAsync (SelectedItem, issue);
                await SetIdleAsync ();
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>発行抹消の確認</summary>
    protected async Task<bool> ConfirmUnIssueBookAsync () {
        if (SelectedItem.IsUpToDateWithIssued && !IsDirty) {
            await SetBusyAsync ();
            var dialogResult = await DialogService.Confirmation ([$"{Book.TableLabel}の発行記録を抹消します。",], title: $"発行抹消", position: DialogPosition.BottomCenter, acceptionLabel: "抹消", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete, onOpend: SetIdleAsync);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                await SetBusyAsync ();
                SelectedItem.NumberOfIsshued = null;
                SelectedItem.IssuedAt = null;
                await SetIdleAsync ();
                Snackbar.Add ($"{Book.TableLabel}の発行記録を抹消しました。", Severity.Normal);
                if ((await UpdateBookAsync (SelectedItem)).IsFailure) {
                    Snackbar.Add ($"{Book.TableLabel}の保存に失敗しました。", Severity.Normal);
                }
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>取得・更新</summary>
    protected async Task<bool> UpdateBookFromSiteAsync (bool withSheets, bool fullUpdate) {
        UiState.Lock ();
        var result = await DataSet.UpdateBookFromSiteAsync (HttpClient, SelectedItem.Url, UserIdentifier, withSheets, fullUpdate,
            (value, max) => {
                if (value == 0) {
                    UiState.Lock (max);
                } else {
                    UiState.UpdateProgress (value);
                }
            });
        foreach (var issue in result.Value.issues) {
            Snackbar.Add (issue, Severity.Error);
        }
        var rc = false;
        if (result.IsSuccess) {
            if (SelectedItem.Id != result.Value.book.Id) { throw new InvalidOperationException ($"id mismatch {SelectedItem.Id} -> {result.Value.book.Id}"); }
            await ReloadAndFocus (SelectedItem.Id, editing: true);
            await ChangeCurrentBookAsync (SelectedItem);
            rc = true;
        }
        UiState.Unlock ();
        return rc;
    }

    /// <summary>発行</summary>
    protected async Task IssueBookAsync (Book book, bool sendToKindle = true) {
        if (book is not null) {
            var title = $"{book.MainTitle}.epub";
            Snackbar.Add ($"『{title}』の生成を開始しました。", Severity.Normal);
            var epubPath = Path.GetTempFileName ();
            try {
                // Create an Epub instance
                var doc = new Epub (book.Title, book.Author);
                doc.Language = "ja";
                // 右綴じにするために"content.opf"の`<spine toc="ncx">`に`page-progression-direction="rtl"`を含める必要がある
                doc.IsLeftToRight = false;
                // Adding sections of HTML content
                doc.AddTitle ();
                if (DataSet.Setting.IncludeImage) {
                    // 表紙
                    if (book.CoverImage is not null) {
                        await doc.AddImageResource (book.CoverImage, book.CoverImageType.Split ('+') [0], true);
                    } else if (book.CoverUrls.Count > 0 && book.CoverSelection is not null) {
                        await doc.AddImageResource (HttpClient, new Uri (book.CoverUrls [book.CoverSelection.Value]), DataSet.Setting.UserAgent, true);
                    }
                }
                doc.AddChapter (null, null, "概要", book.Explanation);
                foreach (var sheet in book.Sheets) {
                    // Add image resources
                    var honbun = await ProcessHtmlForEpub (doc, sheet.SheetHonbun, sheet.Url);
                    var preface = await ProcessHtmlForEpub (doc, sheet.Preface, sheet.Url);
                    var afterword = await ProcessHtmlForEpub (doc, sheet.Afterword, sheet.Url);
                    // Add sheet
                    doc.AddChapter (sheet.ChapterTitle, sheet.ChapterSubTitle, sheet.SheetTitle, honbun, afterword, preface);
                }
                // Add the CSS file referenced in the HTML content
                using (var cssStream = new FileStream ("Services/book-style.css", FileMode.Open)) {
                    doc.AddResource ("book-style.css", EpubResourceType.CSS, cssStream);
                }
                // Export the result
                using (var fs = new FileStream (epubPath, FileMode.Create)) {
                    doc.Export (fs);
                }
                if (sendToKindle) {
                    // Send to Kindle
                    if (new FileInfo (epubPath).Length > DataSet.Setting.PersonalDocumentLimitSize) {
                        Snackbar.Add ($"『{title}』が制限サイズを超えています。", Severity.Warning);
                    }
                    if (SendToKindle (epubPath, title)) {
                        Snackbar.Add ($"『{title}』を発行しました。", Severity.Normal);
                        book.IssuedAt = DateTime.Now;
                        book.NumberOfIsshued = book.Sheets.Count;
                        var result = await UpdateBookAsync (book);
                        if (result.IsFailure) {
                            Snackbar.Add ($"{Book.TableLabel}の更新に失敗しました。", Severity.Error);
                        }
                    } else {
                        Snackbar.Add ($"『{title}』の発行に失敗しました。", Severity.Error);
                    }
                } else {
                    // download
                    Snackbar.Add ($"『{title}』を生成しました。", Severity.Normal);
                    try {
                        using (var fileStream = new FileStream (epubPath, FileMode.Open))
                        using (var streamRef = new DotNetStreamReference (stream: fileStream)) {
                            await JSRuntime.DownloadFileFromStream (title, streamRef);
                        }
                    }
                    catch (Exception e) {
                        System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                        Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
                        Snackbar.Add ($"『{title}』の取得に失敗しました。", Severity.Error);
                    }
                }
            }
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
                Snackbar.Add ($"『{title}』の生成に失敗しました。", Severity.Error);
            }
            finally {
                // delete epub
                if (File.Exists (epubPath)) {
                    File.Delete (epubPath);
                }
            }
        }
    }

    /// <summary>HTMLをEPUB用に加工し画像を加える</summary>
    /// <param name="doc">EPUB document</param>
    /// <param name="innerHtml">元のHTML</param>
    /// <param name="url">シートのURL</param>
    /// <returns>処理済みのHTML</returns>
    protected async Task<string> ProcessHtmlForEpub (Epub doc, string innerHtml, string url) {
        var parser = new HtmlParser ();
        var document = parser.ParseDocument (innerHtml);
        var images = document.QuerySelectorAll ("img");
        if (images is not null) {
            if (DataSet.Setting.IncludeImage) {
                foreach (var image in images) {
                    var src = image.GetAttribute ("src");
                    if (!string.IsNullOrEmpty (src)) {
                        try {
                            var fileName = await doc.AddImageResource (HttpClient, new Uri (new Uri (url), src), DataSet.Setting.UserAgent);
                            image.SetAttribute ("src", fileName);
                        }
                        catch (Exception ex) {
                            System.Diagnostics.Debug.WriteLine ($"Exception: {url} - {ex.Message}\n{ex.StackTrace}");
                            Snackbar.Add ($"Exception: {ex.Message}", Severity.Warning);
                        }
                    }
                }
            }
            innerHtml = (document.Body?.InnerHtml ?? "").Replace ("<br>", "<br/>");
            innerHtml = new Regex (@"(<img\b(?![^>]*?\/[>])[^>]*?)>").Replace (innerHtml, "$1/>");
        }
        return innerHtml;
    }

    /// <summary>Kindleへ送信</summary>
    protected bool SendToKindle (string epubPath, string title) {
        var setting = DataSet.Setting;
        var result = false;
        if (!string.IsNullOrEmpty (epubPath) && File.Exists (epubPath)
            && !string.IsNullOrEmpty (title)
            && !string.IsNullOrEmpty (setting.SmtpMailAddress)
            && !string.IsNullOrEmpty (setting.SmtpMailto)
            && !string.IsNullOrEmpty (setting.SmtpServer)
            && setting.SmtpPort > 0
        ) {
            try {
                using var message = new MimeMessage ();
                message.From.Add (new MailboxAddress ("", setting.SmtpMailAddress));
                message.To.Add (new MailboxAddress ("", setting.SmtpMailto));
                if (!string.IsNullOrEmpty (setting.SmtpCc)) {
                    message.Cc.Add (new MailboxAddress ("", setting.SmtpCc));
                }
                if (!string.IsNullOrEmpty (setting.SmtpBcc)) {
                    message.Bcc.Add (new MailboxAddress ("", setting.SmtpBcc));
                }
                message.Subject = setting.SmtpSubject;
                using var textPart = new TextPart (TextFormat.Plain);
                textPart.Text = setting.SmtpBody;
                using var attachment = new MimePart ();
                attachment.Content = new MimeContent (File.OpenRead (epubPath));
                attachment.ContentDisposition = new ContentDisposition ();
                attachment.ContentTransferEncoding = ContentEncoding.Base64;
                attachment.FileName = title;
                message.Body = new MimeKit.Multipart ("mixed") { textPart, attachment, };
                using var client = new SmtpClient ();
                client.Connect (setting.SmtpServer, setting.SmtpPort, SecureSocketOptions.StartTls);
                if (!string.IsNullOrEmpty (setting.SmtpUserName) && !string.IsNullOrEmpty (setting.SmtpPassword)) {
                    client.Authenticate (setting.SmtpUserName, setting.SmtpPassword);
                }
                client.Send (message);
                client.Disconnect (true);
                result = true;
            }
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
            }
        }
        return result;
    }

    /// <summary>書籍のレコードを更新する</summary>
    protected async Task<Result<int>> UpdateBookAsync (Book book) {
        var result = await DataSet.UpdateAsync (book);
        if (result.IsSuccess) {
            StartEdit (true);
        }
        return result;
    }

    /// <summary>再読み込み</summary>
    protected override async Task ReloadAndFocus (long focusedId = 0L, bool editing = true, bool force = false) {
        await base.ReloadAndFocus (focusedId != 0L ? focusedId : AppModeService.CurrentBookId, force: true);
        SetTitle ();
    }

    /// <summary>セクションタイトルを設定</summary>
    protected void SetTitle () {
        AppModeService.SetSectionTitle (SelectedItem is null ? "Issue" : $"<span style=\"font-size:80%;\">『{SelectedItem?.Title ?? ""}』 {SelectedItem?.Author ?? ""}</span>");
    }

    /// <summary>最初に着目書籍を切り替えてDataSetの再初期化を促す</summary>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        // 基底クラスで着目書籍オブジェクトを取得済み
        SetTitle ();
        StartEdit (true);
    }

    /// <summary>画像サイズ制限</summary>
    protected const int MAX_ALLOWED_IMAGE_SIZE = 1024 * 1024 * 20;

    /// <summary>画像のアップロード</summary>
    protected async Task<bool> UploadFileAsync (IBrowserFile file) {
        var success = false;
        if (UiState.IsLocked || file is null || !IsEditing) { return success; }
        await SetBusyAsync ();
        try {
            using (var fs = file.OpenReadStream (MAX_ALLOWED_IMAGE_SIZE))
            using (var ms = new MemoryStream ()) {
                await fs.CopyToAsync (ms);
                var image = ms.ToArray ();
                var type = image.DetectImageType ();
                if (type != string.Empty) {
                    SelectedItem.CoverImage = image;
                    success = true;
                    Snackbar.Add ($"画像をアップロードしました。'{file.Name}'");
                } else {
                    Snackbar.Add ($"画像の種別が適合しませんでした。'{file.Name}'", Severity.Warning);
                }
            }
        }
        catch (System.IO.IOException ex) {
            if (ex.Message.Contains ("exceeds the maximum of")) {
                Snackbar.Add ($"ファイルサイズが大きすぎます。'{file.Name}' (Max {MAX_ALLOWED_IMAGE_SIZE:#,0}byte)", Severity.Warning);
            } else {
                throw;
            }
        }
        await SetIdleAsync ();
        return success;
    }

    /// <summary>ドロップ対象の基礎</summary>
    protected const string BaseDropAreaClass = "relative rounded-lg pa-1 border-2 border-dashed d-flex align-center justify-center mud-width-full mud-height-full";
    /// <summary>ドロップ対象の通常時</summary>
    protected const string DefaultDropAreaClass = $"{BaseDropAreaClass} mud-border-lines-default";
    /// <summary>ドロップ対象の侵入中</summary>
    protected const string HoveredDropAreaClass = $"{BaseDropAreaClass} mud-border-primary";
    /// <summary>ドロップ対象クラス参照</summary>
    protected string _dropAreaClass = DefaultDropAreaClass;
    /// <summary>ファイルアップロード参照</summary>
    protected MudFileUpload<IBrowserFile>? _fileUpload;

    /// <summary>ファイルの入力があった</summary>
    protected async Task OnInputFileChanged (InputFileChangeEventArgs e) {
        ClearDropArea ();
        var files = e.GetMultipleFiles ();
        if (files is not null && files.Count > 0) {
            foreach (var file in files) {
                if (await UploadFileAsync (file)) {
                    break;
                }
            }
        }
    }

    /// <summary>ドロップ対象に侵入した</summary>
    protected void OnHover ()
        => _dropAreaClass = HoveredDropAreaClass;

    /// <summary>ドロップ対象から外れた</summary>
    protected void ClearDropArea ()
        => _dropAreaClass = DefaultDropAreaClass;

    /// <summary>画像の抹消</summary>
    protected void DeleteFile () {
        if (IsEditing) {
            SelectedItem.CoverImage = null;
            StateHasChanged ();
        }
    }

    /// <summary>前の表紙候補</summary>
    protected void PrevCover () {
        if (SelectedItem.CoverSelection is null) {
            SelectedItem.CoverSelection = SelectedItem.CoverUrls.Count - 1;
        } else if (SelectedItem.CoverSelection == 0) {
            SelectedItem.CoverSelection = null;
        } else {
            SelectedItem.CoverSelection--;
        }
    }

    /// <summary>次の表紙候補</summary>
    protected void NextCover () {
        if (SelectedItem.CoverSelection is null) {
            SelectedItem.CoverSelection = 0;
        } else if (SelectedItem.CoverSelection == SelectedItem.CoverUrls.Count - 1) {
            SelectedItem.CoverSelection = null;
        } else {
            SelectedItem.CoverSelection++;
        }
    }

    /// <summary>選択中の表紙候補をダウンロード</summary>
    protected async Task DownloadCoverAsync () {
        if (SelectedItem.CoverUrls.Count < 1 || SelectedItem.CoverSelection is null) { return; }
        await SetBusyAsync ();
        var imageUrl = SelectedItem.CoverUrls [SelectedItem.CoverSelection.Value];
        try {
            HttpClient.DefaultRequestHeaders.Add ("User-Agent", DataSet.Setting.UserAgent);
            using (var response = await HttpClient.GetAsync (imageUrl, HttpCompletionOption.ResponseHeadersRead)) {
                response.EnsureSuccessStatusCode (); // HTTPエラーコードが返された場合に例外をスロー
                using (var stream = await response.Content.ReadAsStreamAsync ())
                using (var memoryStream = new MemoryStream ()) {
                    await stream.CopyToAsync (memoryStream);
                    SelectedItem.CoverImage = memoryStream.ToArray ();
                }
            }
        }
        catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine ($"Exception: {ex.Message}\n{ex.StackTrace}");
            Snackbar.Add ("画像の取得に失敗しました。", Severity.Warning);
        }
        await SetIdleAsync ();
    }

    /// <summary>前の書籍へ</summary>
    protected virtual async Task PrevBook () {
        if (items is null) { return; }
        var index = items.IndexOf (SelectedItem);
        if (index > 0) {
            await SetBusyAsync ();
            await ChangeCurrentBookAsync (items [index - 1]);
            await ScrollToCurrentAsync ();
            await SetIdleAsync ();
        }
    }

    /// <summary>次の書籍へ</summary>
    protected virtual async Task NextBook () {
        if (items is null) { return; }
        var index = items.IndexOf (SelectedItem);
        if (index < items.Count - 1) {
            await SetBusyAsync ();
            await ChangeCurrentBookAsync (items [index + 1]);
            await ScrollToCurrentAsync ();
            await SetIdleAsync ();
        }
    }

}
