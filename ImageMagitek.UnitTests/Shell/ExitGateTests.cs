using TileShop.UI.ViewModels;
using Xunit;

namespace ImageMagitek.UnitTests.Shell;

public class ExitGateTests
{
    private readonly ExitGate _gate = new();

    [Fact]
    public void Request_First_StartsSequence()
    {
        Assert.Equal(ExitRequestAction.Start, _gate.Request());
    }

    [Fact]
    public void Request_WhileRunning_IsIgnored()
    {
        _gate.Request();

        Assert.Equal(ExitRequestAction.Ignore, _gate.Request());
    }

    [Fact]
    public void Request_AfterCancelledSequence_StartsAgain()
    {
        _gate.Request();
        _gate.Complete(confirmed: false);

        Assert.Equal(ExitRequestAction.Start, _gate.Request());
    }

    [Fact]
    public void Request_AfterConfirmation_Proceeds()
    {
        _gate.Request();
        _gate.Complete(confirmed: true);

        Assert.Equal(ExitRequestAction.Proceed, _gate.Request());
        Assert.Equal(ExitRequestAction.Proceed, _gate.Request());
    }
}
