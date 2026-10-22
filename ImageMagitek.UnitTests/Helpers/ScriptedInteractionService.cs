using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TileShop.Shared.Interactions;

namespace ImageMagitek.UnitTests;

/// <summary>
/// Answers prompts from a queue and records each prompt's message
/// </summary>
internal sealed class ScriptedInteractionService : IInteractionService
{
    public Queue<PromptResult> Answers { get; } = new();
    public List<string> Messages { get; } = [];
    public int PromptCount => Messages.Count;

    public Task AlertAsync(string heading, string message) => Task.CompletedTask;

    public Task<PromptResult> PromptAsync(PromptChoice choices, string heading, string message)
    {
        Messages.Add(message);
        return Task.FromResult(Answers.Dequeue());
    }

    public Task<TResult?> RequestAsync<TResult>(IRequestMediator<TResult> mediator) => throw new NotSupportedException();
}
