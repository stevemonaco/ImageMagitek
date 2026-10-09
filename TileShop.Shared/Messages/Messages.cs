using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace TileShop.Shared.Messages;

public enum NotifyStatusDuration { Short, Indefinite, Reset }

public record AddScatteredArrangerFromCopyMessage(ElementCopy Copy, IProjectResource ProjectResource);
public record NotifyStatusMessage(string NotifyMessage, NotifyStatusDuration DisplayDuration = NotifyStatusDuration.Short);
public record PaletteColorAssignedMessage(Palette Palette, int Index, IColor Color);
public record ImportImageIntoArrangerMessage(ScatteredArranger Arranger, System.Drawing.Rectangle Bounds);
