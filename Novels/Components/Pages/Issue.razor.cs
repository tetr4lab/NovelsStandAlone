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

    /// <summary>URI���͂̌���</summary>
    protected string ValidateUri (string uri) => uri != "" && IsInvalidUri (uri) ? "bad uri" : "";

    /// <summary>������URI</summary>
    protected bool IsInvalidUri (string? url) => !Uri.IsWellFormedUriString (url, UriKind.Absolute);

    //// <summary>���ڏ��Ђ̕ύX</summary>
    protected override async Task ChangeCurrentBookAsync (Book book) {
        await base.ChangeCurrentBookAsync (book);
        SetTitle ();
        StartEdit (true);
    }

    /// <summary>���Ђ̍폜 (�z�[���֑J��)</summary>
    protected async Task DeleteBook (MouseEventArgs eventArgs) {
        var complete = !eventArgs.CtrlKey;
        if (!complete && SelectedItem.IsEmpty) {
            Snackbar.Add ($"�폜���ׂ��V�[�g������܂���B", Severity.Warning);
            return;
        }
        await SetBusyAsync ();
        var target = complete ? $"{Book.TableLabel}��{Sheet.TableLabel}" : $"{Sheet.TableLabel}�̂�";
        var dialogResult = await DialogService.Confirmation ([
            $"�ȉ���{target}�����S�ɍ폜���܂��B",
            SelectedItem.ToString (),
        ], title: $"{target}�̍폜", position: DialogPosition.BottomCenter, acceptionLabel: complete ? "���S�폜" : "�V�[�g�̂ݍ폜", acceptionColor: complete ? Color.Error : Color.Secondary, acceptionIcon: Icons.Material.Filled.Delete, onOpend: SetIdleAsync);
        if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
            await SetBusyAsync ();
            if (complete) {
                var result = await DataSet.RemoveAsync (SelectedItem);
                if (result.IsSuccess) {
                    if (AppModeService.CurrentBookId == SelectedItem.Id) {
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

    /// <summary>�擾�ƍX�V�̊m�F</summary>
    protected async Task<bool> ConfirmUpdateBookAsync (MouseEventArgs eventArgs) {
        if (!IsDirty) {
            await SetBusyAsync ();
            var withSheets = !eventArgs.CtrlKey;
            var fullUpdate = eventArgs.ShiftKey || SelectedItem.IsEmpty;
            var operation = SelectedItem.IsEmpty ? "�擾" : $"{(withSheets && fullUpdate ? "���S" : "")}�X�V";
            var target = $"{Book.TableLabel}{(withSheets ? $"��{Sheet.TableLabel}" : "�̂�")}";
            var dialogResult = await DialogService.Confirmation ([$"�w{SelectedItem.Title}�x��{target}��{SelectedItem.Site}����{operation}���܂��B", withSheets ? $"{Book.TableLabel}��{(fullUpdate ? "�S�Ă�" : "�V����")}{Sheet.TableLabel}���X�V���܂��B" : $"{Book.TableLabel}�݂̂��X�V���A{Sheet.TableLabel}�͍X�V���܂���B"], title: $"{target}��{operation}", position: DialogPosition.BottomCenter, acceptionLabel: operation, acceptionColor: withSheets ? Color.Success : Color.Primary, acceptionIcon: Icons.Material.Filled.Download, onOpend: SetIdleAsync);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                // �I�[�o�[���C
                await SetBusyAsync ();
                Snackbar.Add ($"{target}��{operation}���J�n���܂����B", Severity.Normal);
                if (await UpdateBookFromSiteAsync (withSheets, fullUpdate)) {
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
        if (!IsDirty) {
            await SetBusyAsync ();
            var issue = !eventArgs.CtrlKey;
            var operation = issue ? "���s" : "����";
            var dialogResult = await DialogService.Confirmation ([
                $"�w{SelectedItem.MainTitle}.epub�x��{(issue ? $"<{DataSet.Setting.SmtpMailto}>�֔��s": "�������ă_�E�����[�h")}���܂��B",
            ], title: $"�w{SelectedItem.MainTitle}.epub�x{operation}", position: DialogPosition.BottomCenter, acceptionLabel: operation, acceptionColor: issue ? Color.Success : Color.Primary, acceptionIcon: issue ? Icons.Material.Filled.Publish : Icons.Material.Filled.FileDownload, onOpend: SetIdleAsync);
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

    /// <summary>���s�����̊m�F</summary>
    protected async Task<bool> ConfirmUnIssueBookAsync () {
        if (SelectedItem.IsUpToDateWithIssued && !IsDirty) {
            await SetBusyAsync ();
            var dialogResult = await DialogService.Confirmation ([$"{Book.TableLabel}�̔��s�L�^�𖕏����܂��B",], title: $"���s����", position: DialogPosition.BottomCenter, acceptionLabel: "����", acceptionColor: Color.Error, acceptionIcon: Icons.Material.Filled.Delete, onOpend: SetIdleAsync);
            if (dialogResult != null && !dialogResult.Canceled && dialogResult.Data is bool ok && ok) {
                await SetBusyAsync ();
                SelectedItem.NumberOfIsshued = null;
                SelectedItem.IssuedAt = null;
                await SetIdleAsync ();
                Snackbar.Add ($"{Book.TableLabel}�̔��s�L�^�𖕏����܂����B", Severity.Normal);
                if ((await UpdateBookAsync (SelectedItem)).IsFailure) {
                    Snackbar.Add ($"{Book.TableLabel}�̕ۑ��Ɏ��s���܂����B", Severity.Normal);
                }
            } else {
                return false;
            }
        }
        return true;
    }

    /// <summary>�擾�E�X�V</summary>
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
                if (DataSet.Setting.IncludeImage) {
                    // �\��
                    if (book.CoverImage is not null) {
                        await doc.AddImageResource (book.CoverImage, book.CoverImageType.Split ('+') [0], true);
                    } else if (book.CoverUrls.Count > 0 && book.CoverSelection is not null) {
                        await doc.AddImageResource (HttpClient, new Uri (book.CoverUrls [book.CoverSelection.Value]), DataSet.Setting.UserAgent, true);
                    }
                }
                doc.AddChapter (null, null, "�T�v", book.Explanation);
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

    /// <summary>HTML��EPUB�p�ɉ��H���摜��������</summary>
    /// <param name="doc">EPUB document</param>
    /// <param name="innerHtml">����HTML</param>
    /// <param name="url">�V�[�g��URL</param>
    /// <returns>�����ς݂�HTML</returns>
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
            StartEdit (true);
        }
        return result;
    }

    /// <summary>�ēǂݍ���</summary>
    protected override async Task ReloadAndFocus (long focusedId = 0L, bool editing = true, bool force = false) {
        await base.ReloadAndFocus (focusedId != 0L ? focusedId : AppModeService.CurrentBookId, force: true);
        SetTitle ();
    }

    /// <summary>�Z�N�V�����^�C�g����ݒ�</summary>
    protected void SetTitle () {
        AppModeService.SetSectionTitle (SelectedItem is null ? "Issue" : $"<span style=\"font-size:80%;\">�w{SelectedItem?.Title ?? ""}�x {SelectedItem?.Author ?? ""}</span>");
    }

    /// <summary>�ŏ��ɒ��ڏ��Ђ�؂�ւ���DataSet�̍ď������𑣂�</summary>
    protected override async Task OnInitializedAsync () {
        await base.OnInitializedAsync ();
        // ���N���X�Œ��ڏ��ЃI�u�W�F�N�g���擾�ς�
        SetTitle ();
        StartEdit (true);
    }

    /// <summary>�摜�T�C�Y����</summary>
    protected const int MAX_ALLOWED_IMAGE_SIZE = 1024 * 1024 * 20;

    /// <summary>�摜�̃A�b�v���[�h</summary>
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
                    Snackbar.Add ($"�摜���A�b�v���[�h���܂����B'{file.Name}'");
                } else {
                    Snackbar.Add ($"�摜�̎�ʂ��K�����܂���ł����B'{file.Name}'", Severity.Warning);
                }
            }
        }
        catch (System.IO.IOException ex) {
            if (ex.Message.Contains ("exceeds the maximum of")) {
                Snackbar.Add ($"�t�@�C���T�C�Y���傫�����܂��B'{file.Name}' (Max {MAX_ALLOWED_IMAGE_SIZE:#,0}byte)", Severity.Warning);
            } else {
                throw;
            }
        }
        await SetIdleAsync ();
        return success;
    }

    /// <summary>�h���b�v�Ώۂ̊�b</summary>
    protected const string BaseDropAreaClass = "relative rounded-lg pa-1 border-2 border-dashed d-flex align-center justify-center mud-width-full mud-height-full";
    /// <summary>�h���b�v�Ώۂ̒ʏ펞</summary>
    protected const string DefaultDropAreaClass = $"{BaseDropAreaClass} mud-border-lines-default";
    /// <summary>�h���b�v�Ώۂ̐N����</summary>
    protected const string HoveredDropAreaClass = $"{BaseDropAreaClass} mud-border-primary";
    /// <summary>�h���b�v�ΏۃN���X�Q��</summary>
    protected string _dropAreaClass = DefaultDropAreaClass;
    /// <summary>�t�@�C���A�b�v���[�h�Q��</summary>
    protected MudFileUpload<IBrowserFile>? _fileUpload;

    /// <summary>�t�@�C���̓��͂�������</summary>
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

    /// <summary>�h���b�v�ΏۂɐN������</summary>
    protected void OnHover ()
        => _dropAreaClass = HoveredDropAreaClass;

    /// <summary>�h���b�v�Ώۂ���O�ꂽ</summary>
    protected void ClearDropArea ()
        => _dropAreaClass = DefaultDropAreaClass;

    /// <summary>�摜�̖���</summary>
    protected void DeleteFile () {
        if (IsEditing) {
            SelectedItem.CoverImage = null;
            StateHasChanged ();
        }
    }

    /// <summary>�O�̕\�����</summary>
    protected void PrevCover () {
        if (SelectedItem.CoverSelection is null) {
            SelectedItem.CoverSelection = SelectedItem.CoverUrls.Count - 1;
        } else if (SelectedItem.CoverSelection == 0) {
            SelectedItem.CoverSelection = null;
        } else {
            SelectedItem.CoverSelection--;
        }
    }

    /// <summary>���̕\�����</summary>
    protected void NextCover () {
        if (SelectedItem.CoverSelection is null) {
            SelectedItem.CoverSelection = 0;
        } else if (SelectedItem.CoverSelection == SelectedItem.CoverUrls.Count - 1) {
            SelectedItem.CoverSelection = null;
        } else {
            SelectedItem.CoverSelection++;
        }
    }

    /// <summary>�I�𒆂̕\�������_�E�����[�h</summary>
    protected async Task DownloadCoverAsync () {
        if (SelectedItem.CoverUrls.Count < 1 || SelectedItem.CoverSelection is null) { return; }
        await SetBusyAsync ();
        var imageUrl = SelectedItem.CoverUrls [SelectedItem.CoverSelection.Value];
        try {
            HttpClient.DefaultRequestHeaders.Add ("User-Agent", DataSet.Setting.UserAgent);
            using (var response = await HttpClient.GetAsync (imageUrl, HttpCompletionOption.ResponseHeadersRead)) {
                response.EnsureSuccessStatusCode (); // HTTP�G���[�R�[�h���Ԃ��ꂽ�ꍇ�ɗ�O���X���[
                using (var stream = await response.Content.ReadAsStreamAsync ())
                using (var memoryStream = new MemoryStream ()) {
                    await stream.CopyToAsync (memoryStream);
                    SelectedItem.CoverImage = memoryStream.ToArray ();
                }
            }
        }
        catch (Exception ex) {
            System.Diagnostics.Debug.WriteLine ($"Exception: {ex.Message}\n{ex.StackTrace}");
            Snackbar.Add ("�摜�̎擾�Ɏ��s���܂����B", Severity.Warning);
        }
        await SetIdleAsync ();
    }

    /// <summary>�O�̏��Ђ�</summary>
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

    /// <summary>���̏��Ђ�</summary>
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
