namespace ImageMagitek.Image.Import;

/// <summary>
/// An import staged against an arranger: the current and resulting images plus a report of the differences.
/// Nothing is written until <see cref="Commit"/>.
/// </summary>
public sealed class ImageImportPreview
{
    public Arranger Arranger { get; }
    public DecodedImage Source { get; }
    public ImportReport Report { get; }

    public IndexedImage? CurrentIndexed { get; }
    public IndexedImage? ResultIndexed { get; }
    public DirectImage? CurrentDirect { get; }
    public DirectImage? ResultDirect { get; }

    public bool CanCommit => Report.CanCommit;

    public ImageImportPreview(Arranger arranger, DecodedImage source, ImportReport report, IndexedImage current, IndexedImage result)
    {
        Arranger = arranger;
        Source = source;
        Report = report;
        CurrentIndexed = current;
        ResultIndexed = result;
    }

    public ImageImportPreview(Arranger arranger, DecodedImage source, ImportReport report, DirectImage current, DirectImage result)
    {
        Arranger = arranger;
        Source = source;
        Report = report;
        CurrentDirect = current;
        ResultDirect = result;
    }

    /// <summary>
    /// Encodes the result into the arranger's data sources and flushes them
    /// </summary>
    public void Commit()
    {
        ResultIndexed?.SaveImage();
        ResultDirect?.SaveImage();
    }
}
