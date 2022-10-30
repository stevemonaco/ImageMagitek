using System.Threading.Tasks;

namespace TileShop.Shared.Interactions;

public interface IInteractionService
{
    /// <summary>
    /// Displays a message to the user
    /// </summary>
    /// <param name="heading">Heading text</param>
    /// <param name="message">Main body of text</param>
    Task AlertAsync(string heading, string message);

    /// <summary>
    /// Prompts the user to make a choice
    /// </summary>
    /// <param name="choices">Choices to present to the user</param>
    /// <param name="heading">Heading text</param>
    /// <param name="message">Main body of text</param>
    /// <returns>The result of the user choice</returns>
    Task<PromptResult> PromptAsync(PromptChoice choices, string heading, string? message = default);

    /// <summary>
    /// Requests an interaction with the user
    /// </summary>
    /// <typeparam name="TResult">Result of the interaction</typeparam>
    /// <param name="mediator">The mediation object to interact with</param>
    /// <returns>The result of the interaction</returns>
    Task<TResult?> RequestAsync<TResult>(IRequestMediator<TResult> mediator);
}
