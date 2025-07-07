using PetaPoco;
using System.ComponentModel.DataAnnotations;
using MudBlazor;
using Novels.Components.Pages;
using Novels.Services;
using System.Data;
using Tetr4lab;
using AngleSharp.Html.Parser;
using AngleSharp.Html.Dom;
using System.Text.RegularExpressions;

namespace Novels.Data;

[TableName ("sheets")]
public class Sheet : NovelsBaseModel<Sheet>, INovelsBaseModel {

    /// <inheritdoc/>
    public static string TableLabel => "シート";

    /// <inheritdoc/>
    public static Dictionary<string, string> Label { get; } = new () {
        { nameof (Id), "ID" },
        { nameof (Created), "生成日時" },
        { nameof (Modified), "更新日時" },
        { nameof (Url), "URL" },
        { nameof (Html), "原稿" },
        { nameof (ChapterTitle), "章題" },
        { nameof (ChapterSubTitle), "章副題" },
        { nameof (SheetTitle), "表題" },
        { nameof (SheetHonbun), "本文" },
        { nameof (NovelNumber), "No." },
        { nameof (SheetUpdatedAt), "更新日時" },
        { nameof (Errata), "正誤" },
        { nameof (Remarks), "備考" },
    };

    /// <inheritdoc/>
    public static string BaseSelectSql => @$"select * from `sheets` where `book_id` = @BookId order by `novel_no`;";

    /// <inheritdoc/>
    public static string UniqueKeysSql => "";

    [Column ("book_id"), Required] public long BookId { get; set; } = 0;
    [Column ("url")] public string Url { get; set; } = "";
    [Column ("html")] public string? _html { get; set; } = null;
    /// <summary>公に明示された更新日時、または、シートの取り込み日時</summary>
    [Column ("sheet_update")] public DateTime? SheetUpdatedAt { get; set; } = null;
    /// <summary>シートの並び順 新規では、シート生成時に1からの連番が振られる</remarks>
    [Column ("novel_no"), Required] public int NovelNumber { get; set; } = 0;
    [Column ("errata")] public string? _errata { get; set; } = null;

    /// <summary>シートが所属する書籍</summary>
    public Book Book { get; set; } = null!;

    /// <summary>サイト</summary>
    public Site Site => Book.Site;

    /// <summary>シートのタイトル</summary>
    public string SheetTitle {
        get {
            if (__sheetTitle is null) {
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            __sheetTitle = Document.QuerySelector ("p.novel_subtitle")?.TextContent
                                ?? Document.QuerySelector ("h1.p-novel__title")?.TextContent ?? "";
                            break;
                        case Site.KakuyomuOld:
                        case Site.Kakuyomu:
                            __sheetTitle = Document.QuerySelector ("p.widget-episodeTitle.js-vertical-composition-item")?.TextContent ?? "";
                            break;
                        case Site.Novelup:
                            __sheetTitle = Document.QuerySelector ("div.episode_title")?.TextContent ?? "";
                            break;
                        case Site.Dyreitou:
                            var tmp = Document.QuerySelector ("article");
                            __sheetTitle = tmp?.QuerySelector ("h1")?.TextContent ?? "";
                            break;
                        default:
                            __sheetTitle = "";
                            break;
                    }
                    __sheetTitle = Correct (__sheetTitle);
                }
            }
            return __sheetTitle ?? "";
        }
    }
    protected string? __sheetTitle = null;

    /// <summary>シートの序文</summary>
    public string Preface {
        get {
            if (__preface is null) {
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            __preface = Document.QuerySelector ("div.js-novel-text.p-novel__text.p-novel__text--preface")?.InnerHtml ?? "";
                            break;
                        case Site.KakuyomuOld:
                        case Site.Kakuyomu:
                        case Site.Novelup:
                        case Site.Dyreitou:
                        default:
                            __preface = "";
                            break;
                    }
                    __preface = Correct (__preface);
                }
            }
            return __preface ?? "";
        }
    }
    protected string? __preface = null;

    /// <summary>シートの後書き</summary>
    public string Afterword {
        get {
            if (__afterword is null) {
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            __afterword = Document.QuerySelector ("div.js-novel-text.p-novel__text.p-novel__text--afterword")?.InnerHtml ?? "";
                            break;
                        case Site.KakuyomuOld:
                        case Site.Kakuyomu:
                        case Site.Novelup:
                        case Site.Dyreitou:
                        default:
                            __afterword = "";
                            break;
                    }
                    __afterword = Correct (__afterword);
                }
            }
            return __afterword ?? "";
        }
    }
    protected string? __afterword = null;

    /// <summary>シートの本文</summary>
    // AngleSharpは`<br />`を`<br>`に変換するが、HTML5ではそれが推奨されている
    public string SheetHonbun {
        get {
            if (__sheetHonbun is null) {
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            __sheetHonbun = Document.QuerySelector ("div#novel_honbun.novel_view")?.InnerHtml
                                ?? Document.QuerySelector ("div.js-novel-text.p-novel__text:not(.p-novel__text--preface)")?.InnerHtml ?? "";
                            break;
                        case Site.KakuyomuOld:
                        case Site.Kakuyomu:
                            __sheetHonbun = Document.QuerySelector ("div.widget-episodeBody.js-episode-body")?.InnerHtml ?? "";
                            break;
                        case Site.Novelup:
                            __sheetHonbun = Document.QuerySelector ("div.content")?.InnerHtml ?? "";
                            break;
                        case Site.Dyreitou:
                            var tmp = Document.QuerySelector ("article")?.QuerySelectorAll ("p");
                            __sheetHonbun = tmp is null ? "" : string.Join ("\n", Array.ConvertAll (tmp.ToArray (), x => x.InnerHtml));
                            break;
                        default:
                            var body = Document?.QuerySelector ("body");
                            if (body is not null && body.TextContent != _html) {
                                __sheetHonbun = body.InnerHtml;
                            } else {
                                __sheetHonbun = _html.Replace ("\n", "<br>\n");
                            }
                            break;
                    }
                    __sheetHonbun = Correct (__sheetHonbun);
                }
            }
            return __sheetHonbun ?? "";
        }
    }
    protected string? __sheetHonbun = null;

    /// <summary>シートから抽出された章題</summary>
    public string OriginalChapterTitle {
        get {
            if (__originalChapterTitle is null && !string.IsNullOrEmpty (_html) && Document is not null) {
                var title = (string?) null;
                switch (Site) {
                    case Site.Narou:
                    case Site.Novel18:
                        title = Document.QuerySelector ("p.chapter_title")?.TextContent
                            ?? Document.QuerySelector ("div.c-announce:not(.c-announce--note)")?.QuerySelector ("span:not(.c-announce__emphasis)")?.TextContent ?? "";
                        break;
                    case Site.KakuyomuOld:
                    case Site.Kakuyomu:
                        title = Document.QuerySelector ("p.chapterTitle.level1")?.TextContent ?? "";
                        break;
                    case Site.Novelup:
                        title = Document.QuerySelector ("div.episode_chapter")?.TextContent ?? "";
                        break;
                }
                __originalChapterTitle = Correct (title);
            }
            return __originalChapterTitle ?? "";
        }
    }
    protected string? __originalChapterTitle = null;

    /// <summary>重複を排除した章題</summary>
    // 自身がBook::Sheetsの最初のシートなら_chapterTitleをを返す。最初でなければ、_chapterTitleが一つ前のシートと異なる場合にそれを返す。同じなら""を返す。
    public string ChapterTitle {
        get {
            if (__chapterTitle is null && OriginalChapterTitle is not null) {
                var index = Book.Sheets.FindIndex (s => s.Id == Id);
                __chapterTitle = (index <= 0 || Book.Sheets [index - 1].OriginalChapterTitle != OriginalChapterTitle) ? OriginalChapterTitle : "";
            }
            return __chapterTitle ?? "";
        }
    }
    protected string? __chapterTitle = null;

    /// <summary>シートから抽出された副章題</summary>
    public string ChapterSubTitle {
        get {
            if (__chapterSubTitle is null && !string.IsNullOrEmpty (_html) && Document is not null) {
                var title = (string?) null;
                switch (Site) {
                    case Site.KakuyomuOld:
                    case Site.Kakuyomu:
                        title = Document.QuerySelector ("p.chapterTitle.level2")?.TextContent ?? "";
                        break;
                }
                __chapterSubTitle = Correct (title);
            }
            return __chapterSubTitle ?? "";
        }
    }
    protected string? __chapterSubTitle = null;

    /// <summary>文字校正</summary>
    public string? Correct (string? text) {
        text = text?.Replace ("<br>", "<br/>");
        if (string.IsNullOrEmpty (text) || string.IsNullOrEmpty (Errata)) {
            return text;
        }
        (string errr, string crct) [] errata = Array.ConvertAll (Errata.Split (Terminator, StringSplitOptions.RemoveEmptyEntries), s => {
            var v = s.Split (Separator);
            return (v.Length > 0 ? v [0] : "", v.Length > 1 ? v [1] : "");
        });
        foreach (var e in errata) {
            text = text.Replace (e.errr, e.crct);
        }
        return text;
    }

    /// <summary>外向けのHTML</summary>
    /// <remarks>結果が<see cref="_html" />に反映される。</remarks>
    public string? Html {
        get => _html;
        set {
            if (value != _html) {
                _html = value;
                Flash ();
            }
        }
    }

    /// <summary>外向けの正誤表</summary>
    /// <remarks>結果が<see cref="_errata" />に反映される。</remarks>
    public string? Errata {
        get => _errata;
        set {
            if (value != _errata) {
                _errata = value;
                Flash ();
            }
        }
    }

    /// <summary>キャッシュをクリア</summary>
    public void Flash () {
        __htmlDocument = null;
        __sheetTitle = null;
        __sheetHonbun = null;
        __originalChapterTitle = null;
        __chapterTitle = null;
        __chapterSubTitle = null;
        __preface = null;
        __afterword = null;
        // 再パース
        _ = SheetTitle;
        _ = SheetHonbun;
        _ = OriginalChapterTitle;
        _ = ChapterTitle;
        _ = ChapterSubTitle;
        _ = Preface;
        _ = Afterword;
    }

    /// <summary>パース結果</summary>
    public IHtmlDocument? Document {
        get {
            if (__htmlDocument is null && _html is not null) {
                var parser = new HtmlParser ();
                __htmlDocument = parser.ParseDocument (_html);
            }
            return __htmlDocument;
        }
    }
    protected IHtmlDocument? __htmlDocument = null;

    /// <inheritdoc/>
    public override string? [] SearchTargets => [
        $"#{BookId}.",
        $"@{NovelNumber}.",
        ChapterTitle,
        ChapterSubTitle,
        SheetTitle,
        SheetHonbun,
        Remarks,
    ];

    /// <summary>ノーマルコンストラクタ</summary>
    public Sheet () { }

    /// <inheritdoc/>
    public override Sheet Clone () {
        var item = base.Clone ();
        item.Book = Book;
        item.BookId = BookId;
        item.Url = Url;
        item._html = _html;
        item.NovelNumber = NovelNumber;
        item.SheetUpdatedAt = SheetUpdatedAt;
        item._errata = _errata;
        return item;
    }

    /// <inheritdoc/>
    public override Sheet CopyTo (Sheet destination) {
        destination.Book = Book;
        destination.BookId = BookId;
        destination.Url = Url;
        destination._html = _html;
        destination.NovelNumber = NovelNumber;
        destination.SheetUpdatedAt = SheetUpdatedAt;
        destination._errata = _errata;
        return base.CopyTo (destination);
    }

    /// <inheritdoc/>
    public override bool Equals (Sheet? other) =>
        other != null
        && Id == other.Id
        && BookId == other.BookId
        && Url == other.Url
        && _html == other._html
        && NovelNumber == other.NovelNumber
        && SheetUpdatedAt == other.SheetUpdatedAt
        && _errata == other._errata
        && Remarks == other.Remarks
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (
        HashCode.Combine (Url, _html, NovelNumber, SheetUpdatedAt, _errata, BookId, Remarks),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Url}, {NovelNumber}, {SheetUpdatedAt}, {(Errata is null ? "" : string.Join (',', Errata.Split ('\n')) + ", ")}\"{Remarks}\"";
}
