using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using ImageMagitek.Utility;
using Xunit;

namespace ImageMagitek.UnitTests.WriteAheadLogTransactionTests;

public class WriteAheadLogTransactionTests : IDisposable
{
    private readonly string _testDir;

    public WriteAheadLogTransactionTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "WalTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private string CreateTestFilePath(string name) => Path.Combine(_testDir, name);

    #region ExecuteAsync

    [Fact]
    public async Task ExecuteAsync_NoPendingWrites_ReturnsSuccess()
    {
        var tx = new WriteAheadLogTransaction(_testDir);

        var result = await tx.ExecuteAsync();

        Assert.True(result.HasSucceeded);
    }

    [Fact]
    public async Task ExecuteAsync_SingleNewFile_WritesContent()
    {
        var tx = new WriteAheadLogTransaction(_testDir);
        var targetPath = CreateTestFilePath("output.txt");
        tx.AddWriteFile(targetPath, "hello world");

        var result = await tx.ExecuteAsync();

        Assert.True(result.HasSucceeded);
        Assert.True(File.Exists(targetPath));
        Assert.Equal("hello world", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task ExecuteAsync_MultipleNewFiles_WritesAllContent()
    {
        var tx = new WriteAheadLogTransaction(_testDir);
        var path1 = CreateTestFilePath("file1.txt");
        var path2 = CreateTestFilePath("file2.txt");
        var path3 = CreateTestFilePath("file3.txt");

        tx.AddWriteFile(path1, "content1");
        tx.AddWriteFile(path2, "content2");
        tx.AddWriteFile(path3, "content3");

        var result = await tx.ExecuteAsync();

        Assert.True(result.HasSucceeded);
        Assert.Equal("content1", await File.ReadAllTextAsync(path1));
        Assert.Equal("content2", await File.ReadAllTextAsync(path2));
        Assert.Equal("content3", await File.ReadAllTextAsync(path3));
    }

    [Fact]
    public async Task ExecuteAsync_OverwritesExistingFile_WithNewContent()
    {
        var targetPath = CreateTestFilePath("existing.txt");
        await File.WriteAllTextAsync(targetPath, "original content");

        var tx = new WriteAheadLogTransaction(_testDir);
        tx.AddWriteFile(targetPath, "new content");

        var result = await tx.ExecuteAsync();

        Assert.True(result.HasSucceeded);
        Assert.Equal("new content", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task ExecuteAsync_CleansUpStagingAndBackupFiles()
    {
        var targetPath = CreateTestFilePath("existing.txt");
        await File.WriteAllTextAsync(targetPath, "original");

        var tx = new WriteAheadLogTransaction(_testDir);
        tx.AddWriteFile(targetPath, "updated");

        await tx.ExecuteAsync();

        Assert.False(File.Exists(targetPath + ".tmp"));
        Assert.False(File.Exists(targetPath + ".bak"));
        Assert.False(File.Exists(Path.Combine(_testDir, "_transaction.json")));
    }

    [Fact]
    public async Task ExecuteAsync_CleansUpJournalOnSuccess()
    {
        var tx = new WriteAheadLogTransaction(_testDir);
        tx.AddWriteFile(CreateTestFilePath("file.txt"), "data");

        await tx.ExecuteAsync();

        Assert.False(File.Exists(Path.Combine(_testDir, "_transaction.json")));
    }

    [Fact]
    public async Task ExecuteAsync_StagingFailure_DoesNotModifyOriginal()
    {
        var targetPath = CreateTestFilePath("existing.txt");
        await File.WriteAllTextAsync(targetPath, "original");

        // Create a subdirectory to use as a staging path that will fail
        // (writing to a path where a directory exists instead of a file)
        var badPath = CreateTestFilePath("baddir");
        Directory.CreateDirectory(badPath);
        Directory.CreateDirectory(badPath + ".tmp");

        var tx = new WriteAheadLogTransaction(_testDir);
        tx.AddWriteFile(badPath, "will fail");

        var result = await tx.ExecuteAsync();

        Assert.True(result.HasFailed);
        Assert.Equal("original", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task ExecuteAsync_MixOfNewAndExistingFiles_WritesAll()
    {
        var existingPath = CreateTestFilePath("existing.txt");
        await File.WriteAllTextAsync(existingPath, "old data");

        var newPath = CreateTestFilePath("new.txt");

        var tx = new WriteAheadLogTransaction(_testDir);
        tx.AddWriteFile(existingPath, "updated data");
        tx.AddWriteFile(newPath, "fresh data");

        var result = await tx.ExecuteAsync();

        Assert.True(result.HasSucceeded);
        Assert.Equal("updated data", await File.ReadAllTextAsync(existingPath));
        Assert.Equal("fresh data", await File.ReadAllTextAsync(newPath));
    }

    #endregion

    #region RecoverAsync

    [Fact]
    public async Task RecoverAsync_NoJournal_ReturnsSuccess()
    {
        var result = await WriteAheadLogTransaction.RecoverAsync(_testDir);

        Assert.True(result.HasSucceeded);
    }

    [Fact]
    public async Task RecoverAsync_AllOperationsCompleted_CleansUpAndSucceeds()
    {
        var targetPath = CreateTestFilePath("target.txt");
        await File.WriteAllTextAsync(targetPath, "committed data");

        // Simulate leftover journal where all operations completed
        var journal = new WalJournal
        {
            CreatedUtc = DateTime.UtcNow,
            Operations =
            [
                new WalOperation
                {
                    Id = Guid.NewGuid(),
                    Type = WalOperationType.WriteFile,
                    TargetPath = targetPath,
                    StagingPath = targetPath + ".tmp",
                    BackupPath = targetPath + ".bak",
                    State = WalOperationState.Completed
                }
            ]
        };

        // Create leftover files
        await File.WriteAllTextAsync(targetPath + ".tmp", "staging");
        await File.WriteAllTextAsync(targetPath + ".bak", "backup");
        await WriteJournalAsync(journal);

        var result = await WriteAheadLogTransaction.RecoverAsync(_testDir);

        Assert.True(result.HasSucceeded);
        Assert.Equal("committed data", await File.ReadAllTextAsync(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
        Assert.False(File.Exists(targetPath + ".bak"));
        Assert.False(File.Exists(Path.Combine(_testDir, "_transaction.json")));
    }

    [Fact]
    public async Task RecoverAsync_PendingOperationWithStagingFile_RollsForward()
    {
        var targetPath = CreateTestFilePath("target.txt");
        await File.WriteAllTextAsync(targetPath, "old content");

        var journal = new WalJournal
        {
            CreatedUtc = DateTime.UtcNow,
            Operations =
            [
                new WalOperation
                {
                    Id = Guid.NewGuid(),
                    Type = WalOperationType.WriteFile,
                    TargetPath = targetPath,
                    StagingPath = targetPath + ".tmp",
                    BackupPath = targetPath + ".bak",
                    State = WalOperationState.Pending
                }
            ]
        };

        await File.WriteAllTextAsync(targetPath + ".tmp", "new content");
        await WriteJournalAsync(journal);

        var result = await WriteAheadLogTransaction.RecoverAsync(_testDir);

        Assert.True(result.HasSucceeded);
        Assert.Equal("new content", await File.ReadAllTextAsync(targetPath));
        Assert.False(File.Exists(targetPath + ".tmp"));
        Assert.False(File.Exists(targetPath + ".bak"));
    }

    [Fact]
    public async Task RecoverAsync_PendingOperationWithoutStagingFile_RollsBack()
    {
        var completedPath = CreateTestFilePath("completed.txt");
        var pendingPath = CreateTestFilePath("pending.txt");

        // Simulate: first op completed, second op pending but staging file missing
        await File.WriteAllTextAsync(completedPath, "committed value");
        await File.WriteAllTextAsync(completedPath + ".bak", "original value");

        var journal = new WalJournal
        {
            CreatedUtc = DateTime.UtcNow,
            Operations =
            [
                new WalOperation
                {
                    Id = Guid.NewGuid(),
                    Type = WalOperationType.WriteFile,
                    TargetPath = completedPath,
                    StagingPath = completedPath + ".tmp",
                    BackupPath = completedPath + ".bak",
                    State = WalOperationState.Completed
                },
                new WalOperation
                {
                    Id = Guid.NewGuid(),
                    Type = WalOperationType.WriteFile,
                    TargetPath = pendingPath,
                    StagingPath = pendingPath + ".tmp",
                    BackupPath = null,
                    State = WalOperationState.Pending
                }
            ]
        };

        await WriteJournalAsync(journal);

        var result = await WriteAheadLogTransaction.RecoverAsync(_testDir);

        // Roll-forward fails (no staging file), so rollback occurs
        // The completed operation should be rolled back to its backup
        Assert.True(result.HasSucceeded);
        Assert.Equal("original value", await File.ReadAllTextAsync(completedPath));
        Assert.False(File.Exists(pendingPath));
    }

    [Fact]
    public async Task RecoverAsync_PendingNewFileWithStagingFile_RollsForwardNewFile()
    {
        var targetPath = CreateTestFilePath("newfile.txt");

        var journal = new WalJournal
        {
            CreatedUtc = DateTime.UtcNow,
            Operations =
            [
                new WalOperation
                {
                    Id = Guid.NewGuid(),
                    Type = WalOperationType.WriteFile,
                    TargetPath = targetPath,
                    StagingPath = targetPath + ".tmp",
                    BackupPath = null,
                    State = WalOperationState.Pending
                }
            ]
        };

        await File.WriteAllTextAsync(targetPath + ".tmp", "new file content");
        await WriteJournalAsync(journal);

        var result = await WriteAheadLogTransaction.RecoverAsync(_testDir);

        Assert.True(result.HasSucceeded);
        Assert.Equal("new file content", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task RecoverAsync_CorruptJournal_ReturnsFailed()
    {
        var journalPath = Path.Combine(_testDir, "_transaction.json");
        await File.WriteAllTextAsync(journalPath, "not valid json {{{");

        var result = await WriteAheadLogTransaction.RecoverAsync(_testDir);

        Assert.True(result.HasFailed);
    }

    [Fact]
    public async Task RecoverAsync_MultipleOperations_MixedStates_RollsForwardPending()
    {
        var path1 = CreateTestFilePath("file1.txt");
        var path2 = CreateTestFilePath("file2.txt");

        await File.WriteAllTextAsync(path1, "already committed");

        var journal = new WalJournal
        {
            CreatedUtc = DateTime.UtcNow,
            Operations =
            [
                new WalOperation
                {
                    Id = Guid.NewGuid(),
                    Type = WalOperationType.WriteFile,
                    TargetPath = path1,
                    StagingPath = path1 + ".tmp",
                    BackupPath = path1 + ".bak",
                    State = WalOperationState.Completed
                },
                new WalOperation
                {
                    Id = Guid.NewGuid(),
                    Type = WalOperationType.WriteFile,
                    TargetPath = path2,
                    StagingPath = path2 + ".tmp",
                    BackupPath = null,
                    State = WalOperationState.Pending
                }
            ]
        };

        await File.WriteAllTextAsync(path2 + ".tmp", "pending content");
        await WriteJournalAsync(journal);

        var result = await WriteAheadLogTransaction.RecoverAsync(_testDir);

        Assert.True(result.HasSucceeded);
        Assert.Equal("already committed", await File.ReadAllTextAsync(path1));
        Assert.Equal("pending content", await File.ReadAllTextAsync(path2));
    }

    #endregion

    #region End-to-end scenarios

    [Fact]
    public async Task EndToEnd_SequentialTransactions_EachSucceeds()
    {
        var targetPath = CreateTestFilePath("sequential.txt");

        // First transaction creates the file
        var tx1 = new WriteAheadLogTransaction(_testDir);
        tx1.AddWriteFile(targetPath, "version 1");
        var result1 = await tx1.ExecuteAsync();
        Assert.True(result1.HasSucceeded);
        Assert.Equal("version 1", await File.ReadAllTextAsync(targetPath));

        // Second transaction updates it
        var tx2 = new WriteAheadLogTransaction(_testDir);
        tx2.AddWriteFile(targetPath, "version 2");
        var result2 = await tx2.ExecuteAsync();
        Assert.True(result2.HasSucceeded);
        Assert.Equal("version 2", await File.ReadAllTextAsync(targetPath));
    }

    [Fact]
    public async Task EndToEnd_LargeContent_WritesSuccessfully()
    {
        var targetPath = CreateTestFilePath("large.txt");
        var largeContent = new string('X', 1_000_000);

        var tx = new WriteAheadLogTransaction(_testDir);
        tx.AddWriteFile(targetPath, largeContent);

        var result = await tx.ExecuteAsync();

        Assert.True(result.HasSucceeded);
        Assert.Equal(largeContent, await File.ReadAllTextAsync(targetPath));
    }

    #endregion

    private async Task WriteJournalAsync(WalJournal journal)
    {
        var json = JsonSerializer.Serialize(journal, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(_testDir, "_transaction.json"), json);
    }
}
