using System;
using System.Collections.Generic;

namespace ImageMagitek.Utility;

public enum WalOperationType
{
    WriteFile,
    RenameFile
}

public enum WalOperationState
{
    Pending,
    Completed
}

public sealed class WalOperation
{
    public Guid Id { get; set; }
    public WalOperationType Type { get; set; }
    public string TargetPath { get; set; } = "";
    public string StagingPath { get; set; } = "";
    public string? BackupPath { get; set; }
    public WalOperationState State { get; set; }
}

public sealed class WalJournal
{
    public int Version { get; set; } = 1;
    public DateTime CreatedUtc { get; set; }
    public List<WalOperation> Operations { get; set; } = new();
}
