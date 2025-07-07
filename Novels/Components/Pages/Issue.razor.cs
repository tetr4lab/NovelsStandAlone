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

    /// <summary>URI���͂̌���</summary>
    protected string ValidateUri (string uri) => uri != "" && IsInvalidUri (uri) ? "bad uri" : "";

    /// <summary>������URI</summary>
    protected bool IsInvalidUri (string? url) => !Uri.IsWellFormedUriString (url, UriKind.Absolute);

    /// <summary>���Ђ̍폜 (�z�[���֑J��)</summary>
    protected async Task DeleteBook (MouseEventArgs eventArgs) {
        if (Book is not null) {
            var complete = !eventArgs.CtrlKey;
            if (!complete && Book.IsEmpty) {
                Snackbar.Add ($"�폜���ׂ��V�[�g������܂���B", Severity.Warning);
                return;
            }
            var target = complete ? $"{Book.TableLabel}��{Sheet.TableLabel}" : $"{Sheet.TableLabel}�̂�";
            var dialogResult = await DialogService.Confirmation ([
                $"�ȉ���{target}�����S�ɍ폜���܂��B",
                Book.ToString (),
            ], title: $"{target}�̍폜", position: DialogPosition.BottomCenter, acceptionLabel: complete ? "���S�폜" : "�V�[�g�̂ݍ폜", acceptionColor: complete ? Color.Error : Color.Secondary, acceptionIcon: Icons.Material.Filled.Delete);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                await SetBusyAsync ();
                if (complete) {
                    var result = await DataSet.RemoveAsync (Book);
                    if (result.IsSuccess) {
                        if (AppModeService.CurrentBookId == Book.Id) {
                            AppModeService.SetCurrentBookId (0, 1);
                        }
                        StateHasChanged ();
                        Snackbar.Add ($"{target}���폜���܂����B", Severity.Normal);
                        await SetAppMode (AppMode.Books);
                    } else {
                        Snackbar.Add ($"{target}�̍폜�Ɏ��s���܂����B", Severity.Error);
                    }
                } else {
                    try {
                        // �����X�g�͗v�f���폜�����̂ŕ����Ń��[�v����
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
                            Snackbar.Add ($"{target}���폜���܂����B", Severity.Normal);
                        } else {
                            Snackbar.Add ($"{target}�̈ꕔ({success}/{sheets.Count})���폜���܂����B", Severity.Error);
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

    /// <summary>�擾�ƍX�V�̊m�F</summary>
    protected async Task<bool> ConfirmUpdateBookAsync (MouseEventArgs eventArgs) {
        if (Book is not null && !IsDirty) {
            var withSheets = !eventArgs.CtrlKey;
            var operation =  Book.IsEmpty ? "�擾" : "�X�V";
            var target = $"{Book.TableLabel}{(withSheets ? $"��{Sheet.TableLabel}" : "�̂�")}";
            var dialogResult = await DialogService.Confirmation ([$"�w{Book.Title}�x��{target}��{Book.Site}����{operation}���܂��B", withSheets ? $"{Book.TableLabel}��{Sheet.TableLabel}�S�Ă��X�V���܂��B" : $"{Book.TableLabel}�݂̂��X�V���A{Sheet.TableLabel}�͍X�V���܂���B"], title: $"{target}��{operation}", position: DialogPosition.BottomCenter, acceptionLabel: operation, acceptionColor: withSheets ? Color.Success : Color.Primary, acceptionIcon: Icons.Material.Filled.Download);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                // �I�[�o�[���C
                await SetBusyAsync ();
                Snackbar.Add ($"{target}��{operation}���J�n���܂����B", Severity.Normal);
                if (await UpdateBookFromSiteAsync (withSheets)) {
                    Snackbar.Add ($"{target}��{operation}���܂����B", Severity.Normal);
                } else {
                    Snackbar.Add ($"{target}��{operation}�Ɏ��s���܂����B", Severity.Error);
                }
                await SetIdleAsync ();
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>���s�̊m�F</summary>
    protected async Task<bool> ConfirmIssueBookAsync (MouseEventArgs eventArgs) {
        if (Book is not null && !IsDirty) {
            var issue = !eventArgs.CtrlKey;
            var operation = issue ? "���s" : "����";
            var dialogResult = await DialogService.Confirmation ([
                $"�w{Book.MainTitle}.epub�x��{(issue ? $"<{DataSet.Setting.SmtpMailto}>�֔��s": "�������ă_�E�����[�h")}���܂��B",
            ], title: $"�w{Book.MainTitle}.epub�x{operation}", position: DialogPosition.BottomCenter, acceptionLabel: operation, acceptionColor: issue ? Color.Success : Color.Primary, acceptionIcon: issue ? Icons.Material.Filled.Publish : Icons.Material.Filled.FileDownload);
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

    /// <summary>���s�����̊m�F</summary>
    protected async Task<bool> ConfirmUnIssueBookAsync () {
        if (Book is not null && Book.IsUpToDateWithIssued && !IsDirty) {
            var dialogResult = await DialogService.Confirmation ([$"{Book.TableLabel}�̔��s�L�^�𖕏����܂��B",], title: $"���s����", position: DialogPosition.BottomCenter, acceptionLabel: "����", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                await SetBusyAsync ();
                Book.NumberOfIsshued = null;
                Book.IssuedAt = null;
                await SetIdleAsync ();
                Snackbar.Add ($"{Book.TableLabel}�̔��s�L�^�𖕏����܂����B", Severity.Normal);
                if ((await UpdateBookAsync (Book)).IsFailure) {
                    Snackbar.Add ($"{Book.TableLabel}�̕ۑ��Ɏ��s���܂����B", Severity.Normal);
                }
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>�擾�E�X�V</summary>
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

    /// <summary>���s</summary>
    protected async Task IssueBookAsync (Book book, bool sendToKindle = true) {
        if (book is not null) {
            var title = $"{book.MainTitle}.epub";
            Snackbar.Add ($"�w{title}�x�̐������J�n���܂����B", Severity.Normal);
            var epubPath = Path.GetTempFileName ();
            try {
                // Create an Epub instance
                var doc = new Epub (book.Title, book.Author);
                doc.Language = "ja";
                // �E�Ԃ��ɂ��邽�߂�"content.opf"��`<spine toc="ncx">`��`page-progression-direction="rtl"`���܂߂�K�v������
                doc.IsLeftToRight = false;
                // Adding sections of HTML content
                doc.AddTitle ();
                doc.AddChapter (null, null, "�T�v", book.Explanation);
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
                                            response.EnsureSuccessStatusCode (); // HTTP�G���[�R�[�h���Ԃ��ꂽ�ꍇ�ɗ�O���X���[
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
                                            var fileName = $"image_{Guid.NewGuid ().ToString ("N")}"; // ���j�[�N�Ȗ��O�𐶐�
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
                        Snackbar.Add ($"�w{title}�x�������T�C�Y�𒴂��Ă��܂��B", Severity.Warning);
                    }
                    if (SendToKindle (epubPath, title)) {
                        Snackbar.Add ($"�w{title}�x�𔭍s���܂����B", Severity.Normal);
                        book.IssuedAt = DateTime.Now;
                        book.NumberOfIsshued = book.Sheets.Count;
                        var result = await UpdateBookAsync (book);
                        if (result.IsFailure) {
                            Snackbar.Add ($"{Book.TableLabel}�̍X�V�Ɏ��s���܂����B", Severity.Error);
                        }
                    } else {
                        Snackbar.Add ($"�w{title}�x�̔��s�Ɏ��s���܂����B", Severity.Error);
                    }
                } else {
                    // download
                    Snackbar.Add ($"�w{title}�x�𐶐����܂����B", Severity.Normal);
                    try {
                        using (var fileStream = new FileStream (epubPath, FileMode.Open))
                        using (var streamRef = new DotNetStreamReference (stream: fileStream)) {
                            await JSRuntime.DownloadFileFromStream (title, streamRef);
                        }
                    }
                    catch (Exception e) {
                        System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                        Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
                        Snackbar.Add ($"�w{title}�x�̎擾�Ɏ��s���܂����B", Severity.Error);
                    }
                }
            }
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine ($"Exception: {e.Message}\n{e.StackTrace}");
                Snackbar.Add ($"Exception: {e.Message}", Severity.Error);
                Snackbar.Add ($"�w{title}�x�̐����Ɏ��s���܂����B", Severity.Error);
            }
            finally {
                // delete epub
                if (File.Exists (epubPath)) {
                    File.Delete (epubPath);
                }
            }
        }
    }

    /// <summary>Kindle�֑��M</summary>
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

    /// <summary>���Ђ̃��R�[�h���X�V����</summary>
    protected async Task<Result<int>> UpdateBookAsync (Book book) {
        var result = await DataSet.UpdateAsync (book);
        if (result.IsSuccess) {
            editingItem = null;
            StartEdit ();
        }
        return result;
    }

    /// <summary>�ēǂݍ���</summary>
    protected new async Task ReLoadAsync () {
        var oldBook = Book;
        await base.ReLoadAsync.InvokeAsync ();
        await TaskEx.DelayUntil (() => oldBook != Book);
        if (Book is not null) {
            selectedItem = Book;
        }
        SetAndEdit ();
    }

    /// <summary>�^�C�g����ݒ肵�ĕҏW���J�n</summary>
    protected void SetAndEdit () {
        AppModeService.SetSectionTitle (Book is null ? "Issue" : $"<span style=\"font-size:80%;\">�w{Book?.Title ?? ""}�x {Book?.Author ?? ""}</span>");
        // ����
        editingItem = null;
        StartEdit ();
        StateHasChanged ();
    }

    /// <summary>�ŏ��ɒ��ڏ��Ђ�؂�ւ���DataSet�̍ď������𑣂�</summary>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        // ���N���X�Œ��ڏ��ЃI�u�W�F�N�g���擾�ς�
        SetAndEdit ();
    }

}
