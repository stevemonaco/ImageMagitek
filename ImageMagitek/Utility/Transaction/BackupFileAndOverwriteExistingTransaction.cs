using System;
using System.IO;

namespace ImageMagitek.Utility;

public sealed class BackupFileAndOverwriteExistingTransaction : IFileChangeTransaction
{
    public TransactionState State { get; private set; }

    public string PrimaryFileName { get; set; }
    public string Contents { get; }
    public string BackupFileName { get; }

    public Exception LastException { get; private set; }

    public BackupFileAndOverwriteExistingTransaction(string fileName, string contents)
    {
        PrimaryFileName = fileName;
        Contents = contents;

        BackupFileName = Path.ChangeExtension(PrimaryFileName, ".bak");
        State = TransactionState.NotStarted;
    }

    /// <summary>
    /// Prepares for writing by moving an optionally existing file to a backup location
    /// </summary>
    /// <returns>True if successful, false if an exception occurred</returns>
    public bool Prepare()
    {
        if (State != TransactionState.NotStarted)
            throw new InvalidOperationException($"Attempted to call {nameof(Prepare)} while {nameof(TransactionState)} was '{State}'");

        try
        {
            if (File.Exists(PrimaryFileName))
            {
                File.Move(PrimaryFileName, BackupFileName, true);
            }

            State = TransactionState.Prepared;
            return true;
        }
        catch (Exception ex)
        {
            LastException = ex;
            State = TransactionState.RollbackRequired;
            return false;
        }
    }

    /// <summary>
    /// Writes the contents to a new file
    /// </summary>
    /// <returns></returns>
    public bool Execute()
    {
        if (State != TransactionState.Prepared)
            throw new InvalidOperationException($"Attempted to call {nameof(Execute)} while {nameof(TransactionState)} was '{State}'");

        try
        {
            File.WriteAllText(PrimaryFileName, Contents);
            State = TransactionState.Executed;
            return true;
        }
        catch (Exception ex)
        {
            LastException = ex;
            State = TransactionState.RollbackRequired;
            return false;
        }
    }

    /// <summary>
    /// Rollbacks the operation by restoring from the backup file
    /// </summary>
    /// <returns></returns>
    public bool Rollback()
    {
        if (State != TransactionState.RollbackRequired)
            throw new InvalidOperationException($"Attempted to call {nameof(Rollback)} while {nameof(TransactionState)} was '{State}'");

        try
        {
            if (File.Exists(BackupFileName))
            {
                File.Move(BackupFileName, PrimaryFileName, true);
            }
            else
            {
                State = TransactionState.RollbackCompleted;
                return false;
            }

            State = TransactionState.RollbackCompleted;
            return true;
        }
        catch (Exception ex)
        {
            LastException = ex;
            State = TransactionState.RollbackFailed;
            return false;
        }
    }

    /// <summary>
    /// Completes the transaction by removing the backup file
    /// </summary>
    /// <returns></returns>
    public bool Complete()
    {
        if (State != TransactionState.Executed)
            throw new InvalidOperationException($"Attempted to call {nameof(Complete)} while {nameof(TransactionState)} was '{State}'");

        try
        {
            File.Delete(BackupFileName);
            State = TransactionState.Completed;
            return true;
        }
        catch (Exception ex)
        {
            LastException = ex;
            State = TransactionState.RollbackFailed;
            return false;
        }
    }
}
