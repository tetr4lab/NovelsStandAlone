using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using MudBlazor;
using Novels.Components.Pages;
using Novels.Services;
using PetaPoco;
using Tetr4lab;

namespace Novels.Data;

/// <summary>サイト種別</summary>
public enum Site {
    /// <summary>未設定</summary>
    NotSet = -1,
    /// <summary>不明</summary>
    Unknown = 0,
    /// <summary>小説家になろう</summary>
    Narou = 1,
    /// <summary>旧カクヨム</summary>
    KakuyomuOld = 2,
    /// <summary>ノベルアップ＋</summary>
    Novelup = 3,
    /// <summary>dy冷凍</summary>
    Dyreitou = 4,
    /// <summary>カクヨム</summary>
    Kakuyomu = 5,
    /// <summary>小説家になろう(年齢制限)</summary>
    Novel18 = 6,
    /// <summary>ハーメルン</summary>
    Hameln = 7,
    /// <summary>アルファポリス</summary>
    Alphapolis = 8,
}

/// <summary>書籍の刊行状態</summary>
public enum BookStatus {
    NotSet = 1000, // 未設定
    Completed = 0, // 完結
    PartlyCompleted = 1, // 一応完結
    NoUpdates = 2, // 更新途絶
    Updating = 3, // 更新中
    Disappeared = 4, // 消失
}

public static class BookStatusExtensions {
    /// <summary>書籍の刊行状態を文字列化</summary>
    public static string ToJString (this BookStatus status) {
        return status switch {
            BookStatus.Completed => "完結",
            BookStatus.PartlyCompleted => "一応完結",
            BookStatus.NoUpdates => "更新途絶",
            BookStatus.Updating => "更新中",
            BookStatus.Disappeared => "消失",
            _ => "未設定",
        };
    }

    /// <summary>文字列から書籍の刊行状態を得る</summary>
    public static BookStatus ToBookStatus (this string status) {
        return status switch {
            "完結" => BookStatus.Completed,
            "一応完結" => BookStatus.PartlyCompleted,
            "更新途絶" => BookStatus.NoUpdates,
            "更新中" => BookStatus.Updating,
            "消失" => BookStatus.Disappeared,
            _ => BookStatus.NotSet,
        };
    }
}

[TableName ("books")]
public class Book : NovelsBaseModel<Book>, INovelsBaseModel {

    /// <inheritdoc/>
    public static string TableLabel => "書誌";

    /// <inheritdoc/>
    public static Dictionary<string, string> Label { get; } = new () {
        { nameof (Id), "ID" },
        { nameof (Created), "生成日時" },
        { nameof (Modified), "更新日時" },
        { nameof (Url), "URL" },
        { nameof (Url1), "URL1" },
        { nameof (Url2), "URL2" },
        { nameof (Html), "原稿" },
        { nameof (Site), "掲載" },
        { nameof (Title), "書名" },
        { nameof (Author), "著者" },
        { nameof (NumberOfSheets), "シート数" },
        { nameof (NumberOfIsshued), "発行済みシート数" },
        { nameof (NumberOfRelatedSheets), "取得済みシート数" },
        { nameof (IssuedAt), "発行日時" },
        { nameof (IsUpToDateWithIssued), "既刊" },
        { nameof (Readed), "既読" },
        { nameof (ReadedMemo), "栞メモ" },
        { nameof (Status), "状態" },
        { nameof (HtmlBackup), "原稿待避" },
        { nameof (Errata), "正誤" },
        { nameof (Wish), "好評" },
        { nameof (SeriesTitle), "叢書" },
        { nameof (Remarks), "備考" },
        { nameof (LastUpdate), "最終更新" },
        { nameof (Bookmark), "栞" },
    };

    /// <inheritdoc/>
    public static string BaseSelectSql => @$"select `books`.*, count(`sheets`.`id`) as `number_of_related_sheets` from `books` left join `sheets` on `books`.`id`=`sheets`.`book_id` group by `id`;";

    /// <inheritdoc/>
    public static string UniqueKeysSql => "";

    [Column ("url1"), Required] public string Url1 { get; set; } = "";
    [Column ("url2")] public string Url2 { get; set; } = "";
    [Column ("html")] public string? _html { get; set; } = null;
    [Column ("site")] public Site _site { get; set; } = Site.NotSet;
    [Column ("title")] public string? _title { get; set; } = null;
    [Column ("author")] public string? _author { get; set; } = null;
    /// <summary>Epub発行シート数</summary>
    [Column ("number_of_published")] public int? NumberOfIsshued { get; set; } = null;
    /// <summary>Epub発行日時</summary>
    [Column ("published_at")] public DateTime? IssuedAt { get; set; } = null;
    [Column ("read")] public bool Readed { get; set; } = false;
    [Column ("memorandum")] public string? ReadedMemo { get; set; } = null;
    [Column ("status")] public string _status { get; set; } = "";
    [Column ("html_backup")] public string? HtmlBackup { get; set; } = null;
    [Column ("errata")] public string? _errata { get; set; } = null;
    [Column ("wish")] public bool Wish { get; set; } = false;
    [Column ("bookmark")] public long? Bookmark { get; set; } = null;

    /// <summary>関係先シートの実数</summary>
    [Column ("number_of_related_sheets"), VirtualColumn] public int NumberOfRelatedSheets { get; set; } = 0;

    /// <summary>Urlの代表</summary>
    public string Url => Url1 ?? Url2 ?? "";

    /// <summary>書籍に所属するシート</summary>
    public List<Sheet> Sheets { get; set; } = new ();

    /// <summary>シート未取り込み</summary>
    public bool IsEmpty => NumberOfRelatedSheets <= 0;

    /// <summary>外向けの状態</summary>
    /// <remarks>結果が<see cref="_status" />に反映される。</remarks>
    public BookStatus Status {
        get => _status.ToBookStatus ();
        set {
            if (value != Status) {
                _status = value.ToJString ();
            }
        }
    }

    /// <summary>状態に応じた背景色</summary>
    public Color StatusBgColor {
        get {
            if (Status == BookStatus.NotSet) { return Color.Error; }
            if (Readed) { return Color.Dark; }
            if (IsUpToDateWithIssued) { return Color.Primary; }
            return ((Color [])[Color.Success, Color.Tertiary, Color.Info, Color.Warning, Color.Surface,]) [(int) Status];
        }
    }

    /// <summary>状態に応じた順位</summary>
    public int StatusPriority => ((int) Status) + (Readed ? 100 : 0) + (IsUpToDateWithIssued ? 10 : 0);

    /// <summary>書誌、または、シートから得られる最終更新日時</summary>
    public DateTime LastUpdate {
        get {
            var sheetDates = SheetUpdateDates;
            if (sheetDates.Count > 0) {
                return sheetDates.Max ();
            }
            if (Sheets?.Count > 0) {
                return Sheets.Max (s => s.SheetUpdatedAt ?? s.Modified);
            }
            return Modified;
        }
    }

    /// <summary>発行済みより新しい更新がない</summary>
    public bool IsUpToDateWithIssued =>
        NumberOfIsshued is not null && NumberOfIsshued >= NumberOfSheets
        && (IssuedAt is null || IssuedAt >= LastUpdate);

    /// <summary>発行したことがある</summary>
    public bool HasBeenIssued => NumberOfIsshued > 0 || IssuedAt is not null;

    /// <summary>シート数にエラーがある</summary>
    public bool IsErrorForNumberOfSheets => (NumberOfIsshued ?? 0) > NumberOfRelatedSheets || NumberOfRelatedSheets > NumberOfSheets;

    /// <summary>更新可能である</summary>
    public bool IsUpdatable => NumberOfRelatedSheets < NumberOfSheets;

    /// <summary>出版可能である</summary>
    public bool IsIssuable => (NumberOfIsshued ?? 0) < NumberOfRelatedSheets;

    /// <summary>シート(Url)数</summary>
    public int NumberOfSheets => SheetUrls.Count;

    /// <summary>外向けのサイト</summary>
    /// <remarks>結果が<see cref="_site" />に反映される。</remarks>
    public Site Site {
        get {
            if (_site == Site.NotSet && !string.IsNullOrEmpty (Url)) {
                var site = _site;
                if (Url.Contains ("ncode.syosetu.com")) {
                    site = Site.Narou;
                } else if (Url.Contains ("novel18.syosetu.com")) {
                    site = Site.Novel18;
                } else if (Url.Contains ("kakuyomu.jp")) {
                    if (string.IsNullOrEmpty (_html) || Document?.QuerySelector ("h1#workTitle") is null) {
                        // 新規なら旧サイトではない
                        site = Site.Kakuyomu;
                    } else {
                        site = Site.KakuyomuOld;
                    }
                } else if (Url.Contains ("novelup.plus")) {
                    site = Site.Novelup;
                } else if (Url.Contains ("dyreitou.com")) {
                    site = Site.Dyreitou;
                } else if (Url.Contains ("syosetu.org")) {
                    site = Site.Hameln;
                } else if (Url.Contains ("alphapolis.co.jp")) {
                    site = Site.Alphapolis;
                } else {
                    site = Site.Unknown;
                }
                if (site != _site) {
                    _site = site;
                }
            }
            return _site;
        }
    }

    /// <summary>シリーズタイトル</summary>
    public string SeriesTitle {
        get {
            // 存在しないことはよくあるので空文字を許容し、nullの場合のみ解析する
            if (__seriesTitle is null) {
                var seriesTitle = "";
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            seriesTitle = Document.QuerySelector ("p.series_title")?.TextContent ?? "";
                            break;
                        case Site.Dyreitou:
                            seriesTitle = Document.QuerySelector ("div.cat-title")?.TextContent ?? "";
                            var s = seriesTitle.IndexOf ('『');
                            var e = seriesTitle.IndexOf ('』');
                            seriesTitle = s > 0 && e > s ? seriesTitle.Substring (s + 1, e - s - 1) : Title;
                            break;
                    }
                }
                __seriesTitle = (Correct (seriesTitle) ?? "").Replace ('　', ' ').Trim ();
            }
            return __seriesTitle;
        }
    }
    protected string? __seriesTitle = null;

    /// <summary>外向けのタイトル</summary>
    /// <remarks>検出結果が<see cref="_title" />に反映される。</remarks>
    public string Title {
        get {
            if (string.IsNullOrEmpty (_title)) {
                var title = "";
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            title = (Document.QuerySelector ("p.novel_title")?.TextContent
                                ?? Document.QuerySelector ("h1.p-novel__title")?.TextContent
                                ?? "").Replace ("&quot;", "\"");
                            break;
                        case Site.KakuyomuOld:
                            title = Document.QuerySelector ("h1#workTitle")?.TextContent ?? "";
                            break;
                        case Site.Novelup:
                            title = (Document.QuerySelector ("div.novel_title")?.TextContent ?? "");
                            break;
                        case Site.Dyreitou:
                            title = Document.QuerySelector ("div.cat-title")?.TextContent ?? "";
                            break;
                        case Site.Kakuyomu:
                            title = Document.QuerySelector ("h1[class^='Heading_heading'] a")?.GetAttribute ("title") ?? "";
                            break;
                    }
                }
                if (string.IsNullOrEmpty (title)) {
                    title = Url;
                }
                title = (title ?? "").Replace ('　', ' ').Trim ();
                if (!string.IsNullOrEmpty (title) && title != _title) {
                    _title = title;
                }
            }
            return Correct (_title) ?? "";
        }
    }

    /// <summary>メインタイトルとサブタイトルを分ける文字</summary>
    protected char [] TitleSeparator = { '～', '〜', '－', '（' };

    /// <summary>メインタイトル</summary>
    public string MainTitle {
        get {
            if (string.IsNullOrEmpty (__mainTitle)) {
                var title = Title;
                if (!string.IsNullOrEmpty (title)) {
                    foreach (var separatior in TitleSeparator) {
                        var s = title.IndexOf (separatior);
                        var e = title.LastIndexOf (separatior);
                        if (s > 0 && e > s) {
                            title = title.Substring (0, s);
                            break;
                        }
                    }
                    __mainTitle = GetNormalizedName (title, monadic: false, brackets: true);
                }
            }
            return __mainTitle ?? $"novel_{Id}";
        }
    }
    protected string? __mainTitle = null;

    /// <summary>サブタイトル</summary>
    public string SubTitle {
        get {
            if (string.IsNullOrEmpty (__subTitle)) {
                var title = Title;
                if (!string.IsNullOrEmpty (title)) {
                    var subTitle = "";
                    foreach (var separatior in TitleSeparator) {
                        var s = title.IndexOf (separatior);
                        var e = title.LastIndexOf (separatior);
                        if (s > 0 && e > s) {
                            subTitle = title.Substring (s);
                            break;
                        }
                    }
                    __subTitle = GetNormalizedName (subTitle, monadic: false, brackets: true);
                }
            }
            return __subTitle ?? "";
        }
    }
    protected string? __subTitle = null;

    /// <summary>副章題を持つ</summary>
    public bool HasChapterSubTitle => Site == Site.Kakuyomu || Site == Site.KakuyomuOld;

    /// <summary>外向けの著者</summary>
    /// <remarks>検出結果が<see cref="_author" />に反映される。</remarks>
    public string Author {
        get {
            if (string.IsNullOrEmpty (_author)) {
                var author = "";
                if (!string.IsNullOrEmpty (_html) && Document is not null) {
                    switch (Site) {
                        case Site.Narou:
                        case Site.Novel18:
                            author = (Document.QuerySelector ("div.novel_writername")?.TextContent
                                ?? Document.QuerySelector ("div.p-novel__author")?.TextContent
                                ?? "").Replace ("作者：", "");
                            break;
                        case Site.KakuyomuOld:
                            author = Document.QuerySelector ("span#workAuthor-activityName")?.TextContent ?? "";
                            break;
                        case Site.Novelup:
                            author = (Document.QuerySelector ("div.novel_author")?.TextContent ?? "");
                            break;
                        case Site.Dyreitou:
                            author = "dy冷凍";
                            break;
                        case Site.Kakuyomu:
                            author = Document.QuerySelector ("a[href^='/users']")?.TextContent ?? "";
                            break;
                    }
                }
                if (string.IsNullOrEmpty (author)) {
                    author = Url;
                }
                author = GetNormalizedName (author ?? "");
                if (!string.IsNullOrEmpty (author) && author != _author) {
                    _author = author;
                }
            }
            return Correct (_author) ?? "";
        }
    }

    /// <summary>説明</summary>
    public string Explanation {
        get {
            // 存在しないことはよくあるので空文字を許容し、nullの場合のみ解析する
            if (__explanation is null && !string.IsNullOrEmpty (_html) && Document is not null) {
                var explanation = (string?) null;
                switch (Site) {
                    case Site.Narou:
                    case Site.Novel18:
                        explanation = (Document.QuerySelector ("div#novel_ex")?.InnerHtml
                            ?? Document.QuerySelector ("div#novel_ex.p-novel__summary")?.InnerHtml
                            ?? "").Trim ();
                        break;
                    case Site.KakuyomuOld:
                        explanation = Document.QuerySelector ("p#introduction")?.InnerHtml ?? "";
                        break;
                    case Site.Novelup:
                        explanation = (Document.QuerySelector ("div.novel_synopsis")?.InnerHtml ?? "");
                        break;
                    case Site.Dyreitou:
                        explanation = "";
                        break;
                    case Site.Kakuyomu:
                        explanation = Document.QuerySelector ("div[class^=CollapseTextWithKakuyomuLinks_collapseText__]")?.InnerHtml ?? "";
                        break;
                }
                __explanation = Correct (explanation);
            }
            return __explanation ?? "";
        }
    }
    protected string? __explanation = null;

    /// <summary>書誌に記された各シートのURL</summary>
    public List<string> SheetUrls {
        get {
            if (__sheetUrls is null && !string.IsNullOrEmpty (_html) && Document is not null) {
                var bookUri = new Uri (Url);
                var sheetUrls = new List<string> ();
                var tags = (AngleSharp.Dom.IHtmlCollection<AngleSharp.Dom.IElement>?) null;
                switch (Site) {
                    case Site.Narou:
                    case Site.Novel18:
                        tags = Document.QuerySelectorAll ("dl.novel_sublist2 a");
                        if (tags.Length == 0) {
                            tags = Document.QuerySelectorAll ("div.p-eplist__sublist a");
                        }
                        break;
                    case Site.KakuyomuOld:
                        tags = Document.QuerySelectorAll ("li.widget-toc-episode a");
                        break;
                    case Site.Novelup:
                        tags = Document.QuerySelectorAll ("div.episode_link a");
                        break;
                    case Site.Dyreitou:
                        tags = Document.QuerySelectorAll ("div.mokuji a");
                        break;
                    case Site.Kakuyomu:
                        var regex = new Regex ("(?<=\"__typename\":\"Episode\",\"id\":\")\\d+(?=\")");
                        foreach (Match match in regex.Matches (_html)) {
                            if (match.Success) {
                                sheetUrls.Add ($"{Url}/episodes/{match.Value}");
                            }
                        }
                        break;
                }
                if (sheetUrls.Count <= 0 && tags?.Count () > 0) {
                    foreach (var atag in tags) {
                        var url = atag.GetAttribute ("href");
                        if (!string.IsNullOrEmpty (url)) {
                            sheetUrls.Add (new Uri (bookUri, url).AbsoluteUri);
                        }
                    }
                }
                if (sheetUrls.Count > 0) {
                    __sheetUrls = sheetUrls;
                }
            }
            return __sheetUrls ?? new ();
        }
    }
    protected List<string>? __sheetUrls = null;

    /// <summary>書誌に記された各シートの更新日時</summary>
    public List<DateTime> SheetUpdateDates {
        get {
            if (__sheetUpdateDates is null && !string.IsNullOrEmpty (_html) && Document is not null) {
                var sheetDates = new List<DateTime> ();
                switch (Site) {
                    case Site.Narou:
                    case Site.Novel18:
                        var tags = Document.QuerySelectorAll ("dl.novel_sublist2 dt.long_update");
                        if (tags.Length == 0) {
                            tags = Document.QuerySelectorAll ("div.p-eplist__sublist div.p-eplist__update");
                        }
                        foreach (var tag in tags) {
                            var date = tag.TextContent;
                            var update = tag.QuerySelector ("span[title]");
                            if (update is not null) {
                                date = update.GetAttribute ("title")?.Replace ("改稿", "");
                            }
                            if (!string.IsNullOrEmpty (date)) {
                                if (DateTime.TryParse (date, out var dt)) {
                                    sheetDates.Add (dt);
                                }
                            }
                        }
                        break;
                    case Site.KakuyomuOld:
                        tags = Document.QuerySelectorAll ("li.widget-toc-episode time.widget-toc-episode-datePublished");
                        foreach (var tag in tags) {
                            var date = tag.GetAttribute ("datetime");
                            if (!string.IsNullOrEmpty (date)) {
                                if (DateTime.TryParse (date, out var dt)) {
                                    sheetDates.Add (dt);
                                }
                            }
                        }
                        break;
                    case Site.Novelup:
                        tags = Document.QuerySelectorAll ("div.update_date span span");
                        foreach (var tag in tags) {
                            var date = tag.TextContent;
                            if (!string.IsNullOrEmpty (date)) {
                                if (DateTime.TryParse (date, out var dt)) {
                                    sheetDates.Add (dt);
                                }
                            }
                        }
                        break;
                    case Site.Kakuyomu:
                        var regex = new Regex ("(?<=\"publishedAt\":\")[^\"]+(?=\")");
                        var match = regex.Match (_html);
                        if (match.Success) {
                            if (DateTime.TryParse (match.Value, out var dt)) {
                                for (var i = 0; i < NumberOfSheets; i++) {
                                    sheetDates.Add (dt);
                                }
                            }
                        }
                        break;
                }
                if (sheetDates.Count > 0) {
                    __sheetUpdateDates = sheetDates;
                }
            }
            return __sheetUpdateDates ?? new ();
        }
    }
    protected List<DateTime>? __sheetUpdateDates = null;

    /// <summary>最終ページ</summary>
    public int LastPage {
        get {
            if ((Site == Site.Narou || Site == Site.Novel18) && __lastPage <= 0 && !string.IsNullOrEmpty (_html) && Document is not null) {
                var pager = Document.QuerySelector ("a.novelview_pager-last")?.GetAttribute ("href")
                    ?? Document.QuerySelector ("a.c-pager__item.c-pager__item--last")?.GetAttribute ("href");
                if (!string.IsNullOrEmpty (pager)) {
                    var maxpage = pager.Split ("?p=").LastOrDefault ();
                    if (int.TryParse (maxpage, out var page)) {
                        __lastPage = page;
                    }
                }
            }
            return __lastPage;
        }
    }
    protected int __lastPage = 0;

    /// <summary>名前の標準化</summary>
    /// <param name="name">名前</param>
    /// <param name="monadic">単独記号以降を削除するか</param>
    /// <param name="binary">対合記号を削除するか</param>
    /// <param name="brackets">丸括弧を削除するか</param>
    public string GetNormalizedName (string name, bool monadic = true, bool binary = true, bool brackets = false) {
        if (string.IsNullOrEmpty (name)) {
            return "";
        }
        int s, e;
        var len = name.Length;
        if (binary) {
            s = name.IndexOf ('【');
            e = name.IndexOf ('】');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('【');
            e = name.IndexOf ('】');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('【');
            e = name.IndexOf ('】');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('[');
            e = name.IndexOf (']');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('{');
            e = name.IndexOf ('}');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('<');
            e = name.IndexOf ('>');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('［');
            e = name.IndexOf ('］');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('｛');
            e = name.IndexOf ('｝');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('〔');
            e = name.IndexOf ('〕');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            s = name.IndexOf ('＜');
            e = name.IndexOf ('＞');
            if (s >= 0 && e > s && e - s + 1 < len) {
                name = name.Remove (s, e - s + 1);
            }
            if (brackets) {
                s = name.IndexOf ('(');
                e = name.IndexOf (')');
                if (s >= 0 && e > s && e - s + 1 < len) {
                    name = name.Remove (s, e - s + 1);
                }
                s = name.IndexOf ('（');
                e = name.IndexOf ('）');
                if (s >= 0 && e > s && e - s + 1 < len) {
                    name = name.Remove (s, e - s + 1);
                }
            }
        }
        if (monadic) {
            s = name.IndexOf ('@');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('＠');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('～');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('〜');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('─');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('…');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('、');
            if (s > 0) {
                name = name.Remove (s);
            }
            s = name.IndexOf ('。');
            if (s > 0) {
                name = name.Remove (s);
            }
        }
        return name.Replace ('　', ' ').Trim ();
    }

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

    /// <summary>内容のクリア</summary>
    public void Clear () {
        _site = Site.Unknown;
        _title = null;
        _author = null;
        NumberOfIsshued = null;
        IssuedAt = null;
        Readed = false;
        ReadedMemo = null;
        _status = string.Empty;
        Errata = null;
        Wish = false;
        Bookmark = null;
        HtmlBackup = null; // 次で必ずバックアップされる
        Html = null; // Flushを伴う
    }

    /// <summary>外向けのHTML</summary>
    /// <remarks>結果が<see cref="_html" />に反映される。</remarks>
    public string? Html {
        get => _html;
        set {
            if (value != _html) {
                HtmlBackup = _html;
                _html = value;
                _site = Site.NotSet;
                _title = null;
                _author = null;
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

    /// <summary>パース結果のキャッシュをクリア</summary>
    public void Flash () {
        __htmlDocument = null;
        __sheetUrls = null;
        __sheetUpdateDates = null;
        __seriesTitle = null;
        __mainTitle = null;
        __subTitle = null;
        __explanation = null;
        // 再パース
        _ = SheetUrls;
        _ = SheetUpdateDates;
        _ = SeriesTitle;
        _ = MainTitle;
        _ = SubTitle;
        _ = Explanation;
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
        $"#{Id}.",
        $":{Site}.",
        IsUpToDateWithIssued ? "_is_issued_" : "_not_issued_",
        Readed ? "_is_readed_" : "_not_readed_",
        $"_{Status.ToJString ()}_",
        Wish ? "_is_wished_" : "_not_wished_",
        $"%{NumberOfRelatedSheets}.",
        SeriesTitle,
        Title,
        Author,
        Remarks,
    ];

    /// <summary>ノーマルコンストラクタ</summary>
    public Book () { }

    /// <inheritdoc/>
    public override Book Clone () {
        var item = base.Clone ();
        item.Url1 = Url1;
        item.Url2 = Url2;
        item._html = _html;
        item._site = _site;
        item._title = _title;
        item._author = _author;
        item.NumberOfIsshued = NumberOfIsshued;
        item.IssuedAt = IssuedAt;
        item.Readed = Readed;
        item.ReadedMemo = ReadedMemo;
        item._status = _status;
        item.HtmlBackup = HtmlBackup;
        item._errata = _errata;
        item.Wish = Wish;
        item.Bookmark = Bookmark;
        return item;
    }

    /// <inheritdoc/>
    public override Book CopyTo (Book destination) {
        destination.Url1 = Url1;
        destination.Url2 = Url2;
        destination._html = _html;
        destination._site = _site;
        destination._title = _title;
        destination._author = _author;
        destination.NumberOfIsshued = NumberOfIsshued;
        destination.IssuedAt = IssuedAt;
        destination.Readed = Readed;
        destination.ReadedMemo = ReadedMemo;
        destination._status = _status;
        destination.HtmlBackup = HtmlBackup;
        destination._errata = _errata;
        destination.Wish = Wish;
        destination.Bookmark = Bookmark;
        return base.CopyTo (destination);
    }

    /// <inheritdoc/>
    public override bool Equals (Book? other) =>
        other != null
        && Id == other.Id
        && Url1 == other.Url1
        && Url2 == other.Url2
        && _html == other._html
        && _site == other._site
        && _title == other._title
        && _author == other._author
        && NumberOfIsshued == other.NumberOfIsshued
        && IssuedAt == other.IssuedAt
        && Readed == other.Readed
        && ReadedMemo == other.ReadedMemo
        && _status == other._status
        && HtmlBackup == other.HtmlBackup
        && _errata == other._errata
        && Wish == other.Wish
        && Bookmark == other.Bookmark
        && Remarks == other.Remarks
    ;

    /// <inheritdoc/>
    public override int GetHashCode () => HashCode.Combine (
        HashCode.Combine (Url1, Url2, _html, _site, _title, _author, NumberOfIsshued, IssuedAt),
        HashCode.Combine (Readed, ReadedMemo, _status, HtmlBackup, _errata, Wish, Bookmark, Remarks),
        base.GetHashCode ());

    /// <inheritdoc/>
    public override string ToString () => $"{TableLabel} {Id}: {Url1}, {Url2}, {_site}, {_title}, {_author}, {_status}, {(Readed ? "Readed, " : "")}\"{ReadedMemo}\", {(Wish? "Wish, " : "")}{NumberOfIsshued}/{NumberOfSheets}, {IssuedAt}, {(Errata is null ? "" : string.Join (',', Errata.Split ('\n')) + ", ")}\"{Remarks}\"";
}
