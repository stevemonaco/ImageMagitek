using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;

namespace TileShop.Shared.Models;

public partial class FileColorSourceModel : ColorSourceModel
{
    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(0, long.MaxValue, ErrorMessage = "Offset cannot be negative")]
    private long _fileAddress;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(1, 256, ErrorMessage = "Entries must be 1-256")]
    private int _entries;

    [ObservableProperty] private Endian _endian;

    public FileColorSourceModel(long fileAddress, int entries, Endian endian)
    {
        _fileAddress = fileAddress;
        _entries = entries;
        _endian = endian;
        ValidateAllProperties();
    }
}