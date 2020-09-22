namespace ImageMagitek.Project
{
    public class ArrangerNode : ResourceNode<Arranger>
    {
        public Arranger Arranger { get; }

        public ArrangerNode(string name, Arranger arranger) : base(name, arranger)
        {
            Arranger = arranger;
        }
    }
}
