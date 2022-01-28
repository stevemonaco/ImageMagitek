namespace TileShop.Shared.Services;

public interface IFileSelectService
{
    string GetProjectFileNameByUser();
    string GetNewProjectFileNameByUser();
    string GetExistingDataFileNameByUser();
    string GetExportArrangerFileNameByUser(string defaultName);
    string GetImportArrangerFileNameByUser();
}