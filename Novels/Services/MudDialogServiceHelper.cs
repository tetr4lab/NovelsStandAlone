using Microsoft.AspNetCore.Components;
using MudBlazor;
using Novels.Components.Parts;
using Novels.Data;

namespace Novels.Services;

public static class MudDialogServiceHelper {

    /// <summary>アイテム追加ダイアログを開く</summary>
    public static async Task<IDialogReference> OpenAddItemDialog<TItem> (this IDialogService service, string message, string label, string value)
        where TItem : NovelsBaseModel<TItem>, INovelsBaseModel, new () {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, CloseOnEscapeKey = true, };
        var parameters = new DialogParameters {
            ["Message"] = message,
            ["TextFieldLabel"] = label,
            ["TextFieldValue"] = value,
        };
        return await service.ShowAsync<InputUrlDialog> ($"{TItem.TableLabel}生成", parameters, options);
    }

}

