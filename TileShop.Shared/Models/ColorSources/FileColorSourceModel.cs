using CommunityToolkit.Mvvm.ComponentModel;
using ImageMagitek;

namespace TileShop.Shared.Models;

public partial class FileColorSourceModel : ColorSourceModel
{
    [ObservableProperty] private long _fileAddress;
    [ObservableProperty] private int _entries;
    [ObservableProperty] private Endian _endian;

    public FileColorSourceModel(long fileAddress, int entries, Endian endian)
    {
        FileAddress = fileAddress;
        Entries = entries;
        Endian = endian;
    }
}
