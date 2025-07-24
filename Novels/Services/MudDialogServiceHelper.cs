using Microsoft.AspNetCore.Components;
using MudBlazor;
using Novels.Components.Parts;
using Novels.Data;
using Tetr4lab;

namespace Novels.Services;

public static class MudDialogServiceHelper {

    /// <summary>アイテム追加ダイアログを開く</summary>
    public static async Task<DialogResult?> OpenAddItemDialog<TItem> (this IDialogService service, string message, string label, string value, Func<Task>? onOpend = null)
        where TItem : NovelsBaseModel<TItem>, INovelsBaseModel, new() {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, CloseOnEscapeKey = true, };
        var parameters = new DialogParameters {
            ["Message"] = message,
            ["TextFieldLabel"] = label,
            ["TextFieldValue"] = value,
        };
        var dialog = await service.ShowAsync<InputUrlDialog> ($"{TItem.TableLabel}生成", parameters, options);
        if (onOpend is not null) {
            // 開いた時点で必要な処理
            await onOpend ();
        }
        return await dialog.Result;
    }

    /// <summary>汎用の確認</summary>
    public static async Task<DialogResult?> Confirmation (this IDialogService dialogService, IEnumerable<string?> message, string? title = null, MaxWidth width = MaxWidth.Small, DialogPosition position = DialogPosition.Center, string acceptionLabel = "Ok", Color acceptionColor = Color.Success, string? acceptionIcon = Icons.Material.Filled.Check, string cancellationLabel = "Cancel", Color cancellationColor = Color.Default, string? cancellationIcon = Icons.Material.Filled.Cancel, Func<Task>? onOpend = null) {
        var options = new DialogOptions { MaxWidth = width, FullWidth = true, Position = position, BackdropClick = false, };
        var parameters = new DialogParameters {
            ["Contents"] = message,
            ["AcceptionLabel"] = acceptionLabel,
            ["AcceptionColor"] = acceptionColor,
            ["AcceptionIcon"] = acceptionIcon,
            ["CancellationLabel"] = cancellationLabel,
            ["CancellationColor"] = cancellationColor,
            ["CancellationIcon"] = cancellationIcon,
        };
        var dialog = await dialogService.ShowAsync<ConfirmationDialog> (title ?? "確認", parameters, options);
        if (onOpend is not null) {
            // 開いた時点で必要な処理
            await onOpend ();
        }
        return await dialog.Result;
    }

}

