namespace ImageMagitek.Utility;

public interface ITransactionCommand
{
    TransactionState State { get; }

    bool Prepare();
    bool Execute();
    bool Rollback();
    bool Complete();
}
