using System;
using System.Collections.Generic;

namespace TileShop.AvaloniaUI.ViewExtenders;

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
    bool? ShowDialog(object viewModel);

    /// <summary>
    /// Display a MessageBox
    /// </summary>
    /// <param name="messageBoxText">A <see cref="System.String"/> that specifies the text to display.</param>
    /// <param name="caption">A <see cref="System.String"/> that specifies the title bar caption to display.</param>
    /// <param name="buttonLabels">A dictionary specifying the button labels, if desirable</param>
    /// <returns>The result chosen by the user</returns>
    MessageBoxResult ShowMessageBox(string messageBoxText, string caption = "", IDictionary<MessageBoxResult, string> buttonLabels = null);
}

internal class WindowManager : IWindowManager
{
    public MessageBoxResult ShowMessageBox(string messageBoxText, string caption = "", IDictionary<MessageBoxResult, string> buttonLabels = null) => throw new NotImplementedException();
    public void ShowWindow(object viewModel) => throw new NotImplementedException();
    public bool? ShowDialog(object viewModel) => throw new NotImplementedException();
}
