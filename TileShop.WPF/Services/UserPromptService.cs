using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;

namespace TileShop.WPF.Services
{
    public interface IUserPromptService
    {
        UserPromptResult PromptUser(string message, string caption, UserPromptChoices action);
    }

    public enum UserPromptChoices { YesNo, OkCancel, Ok, YesNoCancel }
    public enum UserPromptResult { Yes, No, Cancel, Ok }

    public class UserPromptService : IUserPromptService
    {
        public UserPromptResult PromptUser(string message, string caption, UserPromptChoices action)
        {
            var messageButtons = action switch
            {
                UserPromptChoices.Ok => MessageBoxButton.OK,
                UserPromptChoices.OkCancel => MessageBoxButton.OKCancel,
                UserPromptChoices.YesNo => MessageBoxButton.YesNo,
                UserPromptChoices.YesNoCancel => MessageBoxButton.YesNoCancel,
                _ => throw new InvalidOperationException($"{nameof(PromptUser)} parameter '{nameof(action)}' was of unexpected value '{action.ToString()}'")
            };

            var messageResult = System.Windows.MessageBox.Show(message, caption, messageButtons);

            return messageResult switch
            {
                MessageBoxResult.Yes => UserPromptResult.Yes,
                MessageBoxResult.No => UserPromptResult.No,
                MessageBoxResult.Cancel => UserPromptResult.Cancel,
                MessageBoxResult.OK => UserPromptResult.Ok,
                _ => throw new InvalidOperationException($"{nameof(PromptUser)} returned with an unsupported response of '{messageResult.ToString()}'")
            };
        }
    }
}
