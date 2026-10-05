using System;
using System.Threading.Tasks;

namespace TileShop.Shared.Interactions;
public interface IAsyncFileRequestService
{
    Task<Uri?> RequestProjectFileName();
    Task<Uri?> RequestNewProjectFileName();
    Task<Uri?> RequestExistingDataFileName();
    Task<Uri?> RequestExportArrangerFileName(string defaultName);
    Task<Uri?> RequestImportArrangerFileName();
    Task<Uri?> RequestExportPaletteFileName(string defaultName);
    Task<Uri?> RequestImportPaletteFileName();
}