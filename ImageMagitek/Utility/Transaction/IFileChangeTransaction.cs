namespace ImageMagitek.Utility
{
    public interface IFileChangeTransaction : ITransactionCommand
    {
        public string PrimaryFileName { get; set; }
    }
}
