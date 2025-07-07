using Tetr4lab;

namespace Novels.Services;

/// <summary>アプリのモード</summary>
public enum AppMode {
    None = NovelsAppModeService.NoneMode,
    Boot = NovelsAppModeService.DefaultMode,
    Books,
    Issue,
    Contents,
    Read,
    Settings,
}

/// <summary>アプリモードサービス</summary>
public class NovelsAppModeService : AppModeService<AppMode> {
    /// <summary>セクションタイトル</summary>
    public string SectionTitle {
        get => _secionTitle;
        protected set {
            if (!_secionTitle.Equals (value)) {
                _secionTitle = value;
                OnPropertyChanged ();
            }
        }
    }
    protected string _secionTitle = string.Empty;

    /// <summary>検索文字列</summary>
    public string FilterText {
        get => _filterText;
        protected set {
            if (!_filterText.Equals (value)) {
                _filterText = value;
                OnPropertyChanged ();
            }
        }
    }
    protected string _filterText = string.Empty;

    /// <summary>着目中の書籍</summary>
    public long CurrentBookId {
        get => _currentBookId;
        protected set {
            if (!_currentBookId.Equals (value)) {
                _currentBookId = value;
                OnPropertyChanged ();
            }
        }
    }
    protected long _currentBookId = 0;

    /// <summary>着目中のシート</summary>
    public int CurrentSheetIndex {
        get => _currentSheetIndex;
        protected set {
            if (!_currentSheetIndex.Equals (value)) {
                _currentSheetIndex = value;
                OnPropertyChanged ();
            }
        }
    }
    protected int _currentSheetIndex = 0;

    /// <summary>セクションタイトルの変更</summary>
    public void SetSectionTitle (string title) {
        SectionTitle = string.Join ("<br />", title.Split ('\n'));
    }

    /// <summary>検索テキストの変更</summary>
    public void SetFilterText (string text) {
        FilterText = text;
    }

    /// <summary>着目中の書籍とシートの変更</summary>
    public void SetCurrentBookId (long bookId, int index) {
        CurrentBookId = bookId;
        CurrentSheetIndex = index;
    }

    /// <summary>着目中の書籍の変更</summary>
    public void SetCurrentBookId (long bookId) {
        CurrentBookId = bookId;
    }

    /// <summary>着目中のシートの変更</summary>
    public void SetCurrentSheetIndex (int index) {
        CurrentSheetIndex = index;
    }
}
