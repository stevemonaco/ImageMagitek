namespace TileShop.UI.ViewModels;

public enum ExitRequestAction { Start, Ignore, Proceed }

/// <summary>
/// Decides whether a window close or shutdown request may proceed, so the request can be cancelled synchronously
/// while the asynchronous exit sequence runs
/// </summary>
public sealed class ExitGate
{
    private enum State { Idle, Running, Confirmed }

    private State _state = State.Idle;

    /// <summary>
    /// Answers a close or shutdown request: <see cref="ExitRequestAction.Start"/> means cancel it and start the exit sequence,
    /// <see cref="ExitRequestAction.Ignore"/> means cancel it because the sequence is running, and
    /// <see cref="ExitRequestAction.Proceed"/> means let it through because the exit was confirmed
    /// </summary>
    public ExitRequestAction Request()
    {
        switch (_state)
        {
            case State.Confirmed:
                return ExitRequestAction.Proceed;
            case State.Running:
                return ExitRequestAction.Ignore;
            default:
                _state = State.Running;
                return ExitRequestAction.Start;
        }
    }

    /// <summary>
    /// Ends the running exit sequence, either confirming the exit or returning to idle when it was cancelled
    /// </summary>
    public void Complete(bool confirmed) => _state = confirmed ? State.Confirmed : State.Idle;
}
