using System;
using System.Threading.Tasks;

namespace TileShop.Shared.Services;
public interface IAsyncFileSelectService
{
    Task<Uri?> GetProjectFileNameByUserAsync();
    Task<Uri?> GetNewProjectFileNameByUserAsync();
    Task<Uri?> GetExistingDataFileNameByUserAsync();
    Task<Uri?> GetExportArrangerFileNameByUserAsync(string defaultName);
    Task<Uri?> GetImportArrangerFileNameByUserAsync();
}