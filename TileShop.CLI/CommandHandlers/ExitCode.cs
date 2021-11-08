namespace TileShop.CLI.Commands;

public enum ExitCode
{
    Success = 0, Unset = -1, Exception = -2, InvalidCommandArguments = -3,
    EnvironmentError = -4, ProjectOpenError = -5, ImportOperationFailed = -6, ExportOperationFailed = -7
}
