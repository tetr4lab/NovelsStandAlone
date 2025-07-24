using System.ComponentModel;
using Microsoft.AspNetCore.Components;
using Novels.Data;

namespace Novels.Components.Pages;

public class SheetListBase : ItemListBase<Sheet> {
    /// <summary>着目中の書籍</summary>
    public Book? Book {
        get {
            if (_book is null && DataSet.IsReady) {
                _book = DataSet.Books.Find (s => s.Id == AppModeService.CurrentBookId);
            }
            return _book;
        }
    }
    protected Book? _book = null;

    /// <summary>着目書籍の変更によるキャッシュクリア</summary>
    protected override void OnAppModeChanged (object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName != "CurrentBookId") {
            _book = null;
        }
        base.OnAppModeChanged (sender, e);
    }
}
