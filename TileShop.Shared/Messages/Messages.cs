using ImageMagitek;
using ImageMagitek.Colors;
using ImageMagitek.Project;

namespace TileShop.Shared.Messages;

public enum ArrangerChange { Elements, Pixels, All }
public enum NotifyStatusDuration { Short, Indefinite, Reset }
public enum ResourceModifyEffect { Project, File }
public enum ResourceChangeAction { Modified, Saved, Renamed, Moved }
public enum ToolWindowKind { ProjectExplorer, PixelEditor }

public record AddScatteredArrangerFromCopyMessage(ElementCopy Copy, IProjectResource ProjectResource);
public record ArrangerChangedMessage(Arranger Arranger, ArrangerChange Change);
public record EditArrangerPixelsMessage(Arranger Arranger, Arranger ProjectArranger, int X, int Y, int Width, int Height);
public record NotifyStatusMessage(string NotifyMessage, NotifyStatusDuration DisplayDuration = NotifyStatusDuration.Short);
public record PaletteChangedMessage(Palette Palette);
public record ProjectLoadedMessage(string ProjectFileName);
public record ProjectUnloadedMessage();
public record ResourceChangedMessage(IProjectResource Resource, ResourceModifyEffect Effect);
public record ResourceRenamedMessage(IProjectResource Resource, string NewName, string OldName);
public record ShowToolWindowMessage(ToolWindowKind ToolWindow);
