using ImageMagitek.Project;

namespace TileShop.Shared.EventModels;

public enum ResourceModifyEffect { Project, File }
public enum ResourceChangeAction { Modified, Saved, Renamed, Moved }

public record ResourceChangedEvent(IProjectResource Resource, ResourceModifyEffect Effect);
