namespace TileShop.WPF.Models
{
    public class FileColorSourceModel : ColorSourceModel
    {
        private long _fileAddress;
        public long FileAddress
        {
            get => _fileAddress;
            set => SetAndNotify(ref _fileAddress, value);
        }

        private int _entries;
        public int Entries
        {
            get => _entries;
            set => SetAndNotify(ref _entries, value);
        }

        public FileColorSourceModel(long fileAddress, int entries)
        {
            FileAddress = fileAddress;
            Entries = entries;
        }
    }
}
