using Avalonia.Input;
using TileShop.Shared.Interactions;

namespace TileShop.UI.Controls;

public enum DialogKeyActionKind { None, RunOption, Dismiss }

public static class DialogKeyAction
{
    /// <summary>
    /// Escape runs an executable cancel option, or dismisses when there is no cancel option; Enter runs an executable default option
    /// </summary>
    public static (DialogKeyActionKind Kind, RequestOption? Option) Resolve(Key key, IEnumerable<RequestOption> options)
    {
        if (key == Key.Escape)
        {
            var cancelOption = options.FirstOrDefault(x => x.IsCancel);

            if (cancelOption is null)
                return (DialogKeyActionKind.Dismiss, null);

            return cancelOption.OptionCommand.CanExecute(null)
                ? (DialogKeyActionKind.RunOption, cancelOption)
                : (DialogKeyActionKind.None, null);
        }

        if (key == Key.Enter)
        {
            var defaultOption = options.FirstOrDefault(x => x.IsDefault);

            if (defaultOption is not null && defaultOption.OptionCommand.CanExecute(null))
                return (DialogKeyActionKind.RunOption, defaultOption);
        }

        return (DialogKeyActionKind.None, null);
    }
}
