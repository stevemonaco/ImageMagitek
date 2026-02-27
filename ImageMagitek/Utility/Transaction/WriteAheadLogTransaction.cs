using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ImageMagitek.Utility;

public sealed class WriteAheadLogTransaction
{
    private const string JournalFileName = "_transaction.json";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _journalDirectory;
    private readonly string _journalPath;
    private readonly List<(string TargetPath, string Contents)> _pendingWrites = new();

    public WriteAheadLogTransaction(string journalDirectory)
    {
        _journalDirectory = journalDirectory;
        _journalPath = Path.Combine(_journalDirectory, JournalFileName);
    }

    public void AddWriteFile(string targetPath, string contents)
    {
        _pendingWrites.Add((targetPath, contents));
    }

    public async Task<MagitekResults> ExecuteAsync()
    {
        if (_pendingWrites.Count == 0)
            return MagitekResults.SuccessResults;

        var journal = new WalJournal
        {
            CreatedUtc = DateTime.UtcNow,
            Operations = _pendingWrites.Select(w => new WalOperation
            {
                Id = Guid.NewGuid(),
                Type = WalOperationType.WriteFile,
                TargetPath = w.TargetPath,
                StagingPath = w.TargetPath + ".tmp",
                BackupPath = File.Exists(w.TargetPath) ? w.TargetPath + ".bak" : null,
                State = WalOperationState.Pending
            }).ToList()
        };

        // Phase 1: Write all content to .tmp staging files
        try
        {
            for (int i = 0; i < _pendingWrites.Count; i++)
            {
                await File.WriteAllTextAsync(journal.Operations[i].StagingPath, _pendingWrites[i].Contents);
            }
        }
        catch (Exception ex)
        {
            // Pre-journal failure: clean up .tmp files, originals untouched
            CleanupStagingFiles(journal);
            return new MagitekResults.Failed(new[] { $"Failed to write staging files: {ex.Message}" });
        }

        // Phase 2: Write journal — transaction is now active
        try
        {
            await WriteJournalAsync(journal);
        }
        catch (Exception ex)
        {
            CleanupStagingFiles(journal);
            return new MagitekResults.Failed(new[] { $"Failed to write transaction journal: {ex.Message}" });
        }

        // Phase 3: For each operation, backup original, move .tmp to target, mark completed
        try
        {
            for (int i = 0; i < journal.Operations.Count; i++)
            {
                var op = journal.Operations[i];

                // Backup original if it exists
                if (op.BackupPath is not null && File.Exists(op.TargetPath))
                {
                    File.Copy(op.TargetPath, op.BackupPath, true);
                }

                // Move staging to target
                File.Move(op.StagingPath, op.TargetPath, true);

                // Mark completed and rewrite journal
                op.State = WalOperationState.Completed;
                await WriteJournalAsync(journal);
            }
        }
        catch (Exception ex)
        {
            // Post-journal failure: rollback completed operations in reverse order
            var rollbackErrors = RollbackCompletedOperations(journal);
            CleanupStagingFiles(journal);
            TryDeleteJournal();

            var errors = new List<string> { $"Transaction failed during commit: {ex.Message}" };
            errors.AddRange(rollbackErrors);
            return new MagitekResults.Failed(errors);
        }

        // Phase 4: Cleanup — delete journal and backup files
        CleanupBackupFiles(journal);
        CleanupStagingFiles(journal);
        TryDeleteJournal();

        return MagitekResults.SuccessResults;
    }

    public static async Task<MagitekResult> RecoverAsync(string journalDirectory)
    {
        var journalPath = Path.Combine(journalDirectory, JournalFileName);

        if (!File.Exists(journalPath))
            return MagitekResult.SuccessResult;

        WalJournal? journal;
        try
        {
            var json = await File.ReadAllTextAsync(journalPath);
            journal = JsonSerializer.Deserialize<WalJournal>(json, _jsonOptions);
        }
        catch (Exception ex)
        {
            return new MagitekResult.Failed($"Failed to read transaction journal for recovery: {ex.Message}");
        }

        if (journal is null)
        {
            TryDeleteFile(journalPath);
            return MagitekResult.SuccessResult;
        }

        var allCompleted = journal.Operations.All(op => op.State == WalOperationState.Completed);

        if (allCompleted)
        {
            // All operations completed — just clean up leftover files
            CleanupBackupFiles(journal);
            CleanupStagingFiles(journal);
            TryDeleteFile(journalPath);
            return MagitekResult.SuccessResult;
        }

        // Some pending — attempt roll-forward
        var pendingOps = journal.Operations.Where(op => op.State == WalOperationState.Pending).ToList();
        bool rollForwardSucceeded = true;

        foreach (var op in pendingOps)
        {
            try
            {
                if (File.Exists(op.StagingPath))
                {
                    if (op.BackupPath is not null && File.Exists(op.TargetPath))
                    {
                        File.Copy(op.TargetPath, op.BackupPath, true);
                    }

                    File.Move(op.StagingPath, op.TargetPath, true);
                    op.State = WalOperationState.Completed;
                }
                else
                {
                    rollForwardSucceeded = false;
                    break;
                }
            }
            catch
            {
                rollForwardSucceeded = false;
                break;
            }
        }

        if (rollForwardSucceeded)
        {
            CleanupBackupFiles(journal);
            CleanupStagingFiles(journal);
            TryDeleteFile(journalPath);
            return MagitekResult.SuccessResult;
        }

        // Roll-forward failed — rollback everything
        var rollbackErrors = RollbackCompletedOperations(journal);
        CleanupStagingFiles(journal);
        TryDeleteFile(journalPath);

        if (rollbackErrors.Count > 0)
            return new MagitekResult.Failed($"Recovery rollback completed with errors: {string.Join("; ", rollbackErrors)}");

        return MagitekResult.SuccessResult;
    }

    private async Task WriteJournalAsync(WalJournal journal)
    {
        var json = JsonSerializer.Serialize(journal, _jsonOptions);
        await File.WriteAllTextAsync(_journalPath, json);
    }

    private static List<string> RollbackCompletedOperations(WalJournal journal)
    {
        var errors = new List<string>();

        foreach (var op in journal.Operations.Where(o => o.State == WalOperationState.Completed).Reverse())
        {
            try
            {
                if (op.BackupPath is not null && File.Exists(op.BackupPath))
                {
                    File.Move(op.BackupPath, op.TargetPath, true);
                }
                else if (op.BackupPath is null)
                {
                    // Original didn't exist before transaction — remove the written file
                    TryDeleteFile(op.TargetPath);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to rollback '{op.TargetPath}': {ex.Message}");
            }
        }

        return errors;
    }

    private static void CleanupBackupFiles(WalJournal journal)
    {
        foreach (var op in journal.Operations)
        {
            if (op.BackupPath is not null)
                TryDeleteFile(op.BackupPath);
        }
    }

    private static void CleanupStagingFiles(WalJournal journal)
    {
        foreach (var op in journal.Operations)
        {
            TryDeleteFile(op.StagingPath);
        }
    }

    private void TryDeleteJournal()
    {
        TryDeleteFile(_journalPath);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup
        }
    }
}
