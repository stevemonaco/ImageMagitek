namespace ImageMagitek.Colors
{
    public class ProjectForeignColorSource : IColorSource
    {
        public IColor Value { get; set; }

        public ProjectForeignColorSource(IColor value)
        {
            Value = value;
        }
    }
}
