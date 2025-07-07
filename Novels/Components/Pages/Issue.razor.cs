using System.Text.RegularExpressions;
using System.Net.Http;
using System.IO;
using AngleSharp.Html.Parser;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Components;
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

public partial class Issue : ItemListBase<Book> {

    /// <summary>URI入力の検証</summary>
    protected string ValidateUri (string uri) => uri != "" && IsInvalidUri (uri) ? "bad uri" : "";

    /// <summary>無効なURI</summary>
    protected bool IsInvalidUri (string? url) => !Uri.IsWellFormedUriString (url, UriKind.Absolute);

    /// <summary>書籍の削除 (ホームへ遷移)</summary>
    protected async Task DeleteBook (MouseEventArgs eventArgs) {
        if (Book is not null) {
            var complete = !eventArgs.CtrlKey;
            if (!complete && Book.IsEmpty) {
                Snackbar.Add ($"削除すべきシートがありません。", Severity.Warning);
                return;
            }
            var target = complete ? $"{Book.TableLabel}と{Sheet.TableLabel}" : $"{Sheet.TableLabel}のみ";
            var dialogResult = await DialogService.Confirmation ([
                $"以下の{target}を完全に削除します。",
                Book.ToString (),
            ], title: $"{target}の削除", position: DialogPosition.BottomCenter, acceptionLabel: complete ? "完全削除" : "シートのみ削除", acceptionColor: complete ? Color.Error : Color.Secondary, acceptionIcon: Icons.Material.Filled.Delete);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                await SetBusyAsync ();
                if (complete) {
                    var result = await DataSet.RemoveAsync (Book);
                    if (result.IsSuccess) {
                        if (AppModeService.CurrentBookId == Book.Id) {
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
                        var sheets = new List<Sheet> (Book.Sheets);
                        var success = 0;
                        var count = 0;
                        UiState.Lock (sheets.Count);
                        foreach (var sheet in sheets) {
                            UiState.UpdateProgress (++count);
                            if ((await DataSet.RemoveAsync (sheet)).IsSuccess) {
                                success++;
                            }
                        }
                        await ReLoadAsync ();
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
    }

    /// <summary>取得と更新の確認</summary>
    protected async Task<bool> ConfirmUpdateBookAsync (MouseEventArgs eventArgs) {
        if (Book is not null && !IsDirty) {
            var withSheets = !eventArgs.CtrlKey;
            var operation =  Book.IsEmpty ? "取得" : "更新";
            var target = $"{Book.TableLabel}{(withSheets ? $"と{Sheet.TableLabel}" : "のみ")}";
            var dialogResult = await DialogService.Confirmation ([$"『{Book.Title}』の{target}を{Book.Site}から{operation}します。", withSheets ? $"{Book.TableLabel}と{Sheet.TableLabel}全てを更新します。" : $"{Book.TableLabel}のみを更新し、{Sheet.TableLabel}は更新しません。"], title: $"{target}の{operation}", position: DialogPosition.BottomCenter, acceptionLabel: operation, acceptionColor: withSheets ? Color.Success : Color.Primary, acceptionIcon: Icons.Material.Filled.Download);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                // オーバーレイ
                await SetBusyAsync ();
                Snackbar.Add ($"{target}の{operation}を開始しました。", Severity.Normal);
                if (await UpdateBookFromSiteAsync (withSheets)) {
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
        if (Book is not null && !IsDirty) {
            var issue = !eventArgs.CtrlKey;
            var operation = issue ? "発行" : "生成";
            var dialogResult = await DialogService.Confirmation ([
                $"『{Book.MainTitle}.epub』を{(issue ? $"<{DataSet.Setting.SmtpMailto}>へ発行": "生成してダウンロード")}します。",
            ], title: $"『{Book.MainTitle}.epub』{operation}", position: DialogPosition.BottomCenter, acceptionLabel: operation, acceptionColor: issue ? Color.Success : Color.Primary, acceptionIcon: issue ? Icons.Material.Filled.Publish : Icons.Material.Filled.FileDownload);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                await SetBusyAsync ();
                await IssueBookAsync (Book, issue);
                await SetIdleAsync ();
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>発行抹消の確認</summary>
    protected async Task<bool> ConfirmUnIssueBookAsync () {
        if (Book is not null && Book.IsUpToDateWithIssued && !IsDirty) {
            var dialogResult = await DialogService.Confirmation ([$"{Book.TableLabel}の発行記録を抹消します。",], title: $"発行抹消", position: DialogPosition.BottomCenter, acceptionLabel: "抹消", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                await SetBusyAsync ();
                Book.NumberOfIsshued = null;
                Book.IssuedAt = null;
                await SetIdleAsync ();
                Snackbar.Add ($"{Book.TableLabel}の発行記録を抹消しました。", Severity.Normal);
                if ((await UpdateBookAsync (Book)).IsFailure) {
                    Snackbar.Add ($"{Book.TableLabel}の保存に失敗しました。", Severity.Normal);
                }
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>取得・更新</summary>
    protected async Task<bool> UpdateBookFromSiteAsync (bool withSheets) {
        if (Book is not null) {
            var result = await DataSet.UpdateBookFromSiteAsync (HttpClient, Book.Url, UserIdentifier, withSheets, 
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
            UiState.Unlock ();
            if (result.IsSuccess) {
                if (Book.Id != result.Value.book.Id) { throw new InvalidOperationException ($"id mismatch {Book.Id} -> {result.Value.book.Id}"); }
                await ReLoadAsync ();
                if (Book is not null) {
                    ChangeCurrentBook (Book);
                }
                return true;
            }
        }
        return false;
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
                doc.AddChapter (null, null, "概要", book.Explanation);
                foreach (var sheet in book.Sheets) {
                    var honbun = sheet.SheetHonbun;
                    // Add image resources
                    var parser = new HtmlParser ();
                    var document = parser.ParseDocument (sheet.SheetHonbun);
                    var images = document.QuerySelectorAll ("img");
                    if (images is not null) {
                        if (DataSet.Setting.IncludeImage) {
                            foreach (var image in images) {
                                var src = image.GetAttribute ("src");
                                if (!string.IsNullOrEmpty (src)) {
                                    var imageUrl = new Uri (new Uri (sheet.Url), src);
                                    try {
                                        HttpClient.DefaultRequestHeaders.Add ("User-Agent", DataSet.Setting.UserAgent);
                                        using (var response = await HttpClient.GetAsync (imageUrl, HttpCompletionOption.ResponseHeadersRead)) {
                                            response.EnsureSuccessStatusCode (); // HTTPエラーコードが返された場合に例外をスロー
                                            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                                            var resourceType = contentType switch {
                                                "image/jpeg" => EpubResourceType.JPEG,
                                                "image/png" => EpubResourceType.PNG,
                                                "image/gif" => EpubResourceType.GIF,
                                                "image/ttf" => EpubResourceType.TTF,
                                                "image/otf" => EpubResourceType.OTF,
                                                "image/svg+xml" => EpubResourceType.SVG,
                                                _ => EpubResourceType.JPEG,
                                            };
                                            var fileName = $"image_{Guid.NewGuid ().ToString ("N")}"; // ユニークな名前を生成
                                            using (var stream = await response.Content.ReadAsStreamAsync ()) {
                                                doc.AddResource (fileName, resourceType, stream, true);
                                                image.SetAttribute ("src", fileName);
                                            }
                                        }
                                    }
                                    catch (Exception ex) {
                                        System.Diagnostics.Debug.WriteLine ($"Exception: {imageUrl} - {ex.Message}\n{ex.StackTrace}");
                                        Snackbar.Add ($"Exception: {ex.Message}", Severity.Warning);
                                    }
                                }
                            }
                        }
                        honbun = (document.Body?.InnerHtml ?? "").Replace ("<br>", "<br/>");
                        honbun = new Regex (@"(<img\b(?![^>]*?\/[>])[^>]*?)>").Replace (honbun, "$1/>");
                    }
                    // Add sheet
                    doc.AddChapter (sheet.ChapterTitle, sheet.ChapterSubTitle, sheet.SheetTitle, honbun, sheet.Afterword, sheet.Preface);
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
            editingItem = null;
            StartEdit ();
        }
        return result;
    }

    /// <summary>再読み込み</summary>
    protected new async Task ReLoadAsync () {
        var oldBook = Book;
        await base.ReLoadAsync.InvokeAsync ();
        await TaskEx.DelayUntil (() => oldBook != Book);
        if (Book is not null) {
            selectedItem = Book;
        }
        SetAndEdit ();
    }

    /// <summary>タイトルを設定して編集を開始</summary>
    protected void SetAndEdit () {
        AppModeService.SetSectionTitle (Book is null ? "Issue" : $"<span style=\"font-size:80%;\">『{Book?.Title ?? ""}』 {Book?.Author ?? ""}</span>");
        // 強制
        editingItem = null;
        StartEdit ();
        StateHasChanged ();
    }

    /// <summary>最初に着目書籍を切り替えてDataSetの再初期化を促す</summary>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        // 基底クラスで着目書籍オブジェクトを取得済み
        SetAndEdit ();
    }

}
