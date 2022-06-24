using System.Threading.Tasks;

namespace TileShop.Shared.Dialogs;

public enum PromptChoice { Ok, YesNo, YesNoCancel, OkCancel };
public enum PromptResult { Ok, Yes, No, Cancel };

/// <summary>
/// Manager capable of taking a ViewModel instance, instantiating its View and showing it as a dialog or window
/// </summary>
public interface IWindowManager
{
    /// <summary>
    /// Given a ViewModel, show its corresponding View as a window
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    void ShowWindow(object viewModel);

    /// <summary>
    /// Given a ViewModel, show its corresponding View as a Dialog
    /// </summary>
    /// <param name="viewModel">ViewModel to show the View for</param>
    /// <returns>DialogResult of the View</returns>
    Task<TResult> ShowDialog<TResult>(IDialogMediator<TResult> mediator);

    /// <summary>
    /// Displays a MessageBox to the user
    /// </summary>
    /// <param name="contentMessage">A <see cref="System.String"/> that specifies the content text to display.</param>
    /// <param name="title">A <see cref="System.String"/> that specifies the title to display.</param>
    /// <returns>The result chosen by the user</returns>
    Task ShowMessageBox(string contentMessage, string title = "");

    /// <summary>
    /// Displays a MessageBox to the user
    /// </summary>
    /// <param name="contentMessage">A <see cref="System.String"/> that specifies the text to display.</param>
    /// <param name="userChoices">The choices to be presented to the user</param>
    /// <param name="title">A <see cref="System.String"/> that specifies the title to display.</param>
    /// <returns>The result chosen by the user</returns>
    Task<PromptResult> ShowMessageBox(string contentMessage, PromptChoice userChoices, string title = "");

    /// <summary>
    /// Displays a MessageBox to the user synchronously
    /// </summary>
    /// <param name="contentMessage">A <see cref="System.String"/> that specifies the text to display.</param>
    /// <param name="userChoices">The choices to be presented to the user</param>
    /// <param name="title">A <see cref="System.String"/> that specifies the title to display.</param>
    /// <returns>The result chosen by the user</returns>
    PromptResult ShowMessageBoxSync(string contentMessage, PromptChoice userChoices, string title = "");
}
