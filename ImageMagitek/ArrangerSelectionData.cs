using System;
using System.Drawing;

namespace ImageMagitek
{
    // Class to store a selection of arranger data

    /// <summary>
    /// Handles the data associated with arranger selections
    /// </summary>
    public class ArrangerSelectionData
    {
        /// <summary>
        /// Arranger which holds the data to be copied
        /// </summary>
        public Arranger Arranger { get; private set; }

        /// <summary>
        /// Key to the Arranger which holds the data to be copied
        /// </summary>
        public string ArrangerKey { get; private set; }

        /// <summary>
        /// Upper left location of the selection, in element units
        /// </summary>
        public Point Location { get; private set; }

        /// <summary>
        /// Size of selection in number of elements
        /// </summary>
        public Size SelectionSize { get; private set; }

        /// <summary>
        /// State of having a finalized selection
        /// </summary>
        public bool HasSelection { get; private set; }

        /// <summary>
        /// State of currently making or resizing a selection
        /// </summary>
        public bool InSelection { get; private set; }

        /// <summary>
        /// State of currently dragging a finalized selection area
        /// </summary>
        public bool InDragState { get; private set; }

        /// <summary>
        /// State of the selection being changed or not
        /// </summary>
        public bool SelectionChanged { get; private set; }

        /// <summary>
        /// Sets the zoom level to translate coordinates appropriately between original element coordinates and a resized element coordinate system
        /// Must be greater than or equal to 1
        /// </summary>
        public int Zoom
        {
            get => _zoom;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException($"{Zoom}: Value '{value}' must be greater or equal to 1");
                _zoom = value;
            }
        }
        private int _zoom;

        /// <summary>
        /// Beginning selection point in zoomed coordinates
        /// </summary>
        public Point BeginPoint { get; private set; }

        /// <summary>
        /// Ending selection point in zoomed coordinates
        /// </summary>
        public Point EndPoint { get; private set; }

        /// <summary>
        /// Location of elements selected in the underlying arranger
        /// </summary>
        public Rectangle SelectedElements { get; private set; }

        /// <summary>
        /// Rectangle containing selected pixels in zoomed coordinates
        /// </summary>
        public Rectangle SelectedClientRect { get; private set; }

        /// <summary>
        /// List of selected elements
        /// Must call PopulateData before retrieving
        /// </summary>
        public ArrangerElement[,] ElementList { get; private set; }

        public ArrangerSelectionData(Arranger arranger)
        {
            Arranger = arranger;
            ClearSelection();
        }

        public void ClearSelection()
        {
            Location = new Point(0, 0);
            SelectionSize = new Size(0, 0);
            ElementList = null;
            HasSelection = false;
            InSelection = false;
            InDragState = false;
            SelectionChanged = false;
            SelectedElements = new Rectangle(0, 0, 0, 0);
            SelectedClientRect = new Rectangle(0, 0, 0, 0);
            BeginPoint = new Point(0, 0);
            EndPoint = new Point(0, 0);
        }

        /// <summary>
        /// Populates ElementList for retrieval
        /// </summary>
        /// <returns></returns>
        public bool PopulateData()
        {
            if (!HasSelection)
                return false;

            ElementList = new ArrangerElement[SelectionSize.Width, SelectionSize.Height];
            for (int ysrc = SelectedElements.Y, ydest = 0; ydest < SelectionSize.Height; ydest++, ysrc++)
            {
                for (int xsrc = SelectedElements.X, xdest = 0; xdest < SelectionSize.Width; xdest++, xsrc++)
                {
                    ElementList[xdest, ydest] = Arranger.GetElement(xsrc, ysrc);
                }
            }

            return true;
        }

        /// <summary>
        /// Retrieves an element from the selected elements
        /// </summary>
        /// <param name="ElementX"></param>
        /// <param name="ElementY"></param>
        /// <returns></returns>
        public ArrangerElement GetElement(int ElementX, int ElementY) => ElementList[ElementX, ElementY];

        /// <summary>
        /// Begins a new selection
        /// </summary>
        /// <param name="beginPoint"></param>
        /// <param name="endPoint"></param>
        public void BeginSelection(Point beginPoint, Point endPoint)
        {
            HasSelection = true;
            InSelection = true;
            SelectionChanged = true;
            BeginPoint = beginPoint;
            EndPoint = endPoint;
            CalculateSelectionData();
        }

        /// <summary>
        /// Updates an in-progress selection with a new end point
        /// </summary>
        /// <param name="endPoint"></param>
        /// <returns>True if the selection was changed</returns>
        public bool UpdateSelection(Point endPoint)
        {
            if (EndPoint != endPoint)
            {
                EndPoint = endPoint;
                CalculateSelectionData();
                return true;
            }
            else // No need to set as the two points are equal
                return false;
        }

        /// <summary>
        /// Ends the selection and moves the selection into a finalized state
        /// </summary>
        public void EndSelection()
        {
            InSelection = false;
            Rectangle testBounds = new Rectangle(new Point(0, 0), Arranger.ArrangerElementSize);

            if (!SelectedElements.IntersectsWith(testBounds)) // No intersection means no selection
            {
                ClearSelection();
                HasSelection = false;
            }
        }

        /// <summary>
        /// Begins the drag and drop state for the current finalized selection
        /// </summary>
        public void BeginDragDrop() => InDragState = true;

        /// <summary>
        /// Ends the drag and drop state for the current finalized selection
        /// </summary>
        public void EndDragDrop() => InDragState = false;

        /// <summary>
        /// Calculates a resized selection rectangle in zoomed coordinates (to fully cover tiles that are half-moused over) and populates
        /// selected elements and selection size for retrieval
        /// </summary>
        private void CalculateSelectionData()
        {
            Rectangle zoomed = PointsToRectangle(BeginPoint, EndPoint); // Rectangle in zoomed coordinates
            Rectangle unzoomed = ViewerToArrangerRectangle(zoomed);
            Rectangle unzoomedfull = GetExpandedSelectionPixelRect(unzoomed);

            SelectedClientRect = new Rectangle(unzoomedfull.X * Zoom, unzoomedfull.Y * Zoom, unzoomedfull.Width * Zoom, unzoomedfull.Height * Zoom);

            SelectedElements = new Rectangle(unzoomedfull.X / Arranger.ElementPixelSize.Width, unzoomedfull.Y / Arranger.ElementPixelSize.Height,
                unzoomedfull.Width / Arranger.ElementPixelSize.Width, unzoomedfull.Height / Arranger.ElementPixelSize.Height);

            SelectionSize = new Size(SelectedElements.Width, SelectedElements.Height);
        }

        /// <summary>
        /// Expands the given selection rectangle to fully contain all pixels of selected Elements
        /// </summary>
        /// <param name="partialRectangle">Selection rectangle in unzoomed pixels containing partially selected Elements</param>
        /// <returns></returns>
        private Rectangle GetExpandedSelectionPixelRect(Rectangle partialRectangle)
        {
            int x1 = partialRectangle.Left;
            int x2 = partialRectangle.Right;
            int y1 = partialRectangle.Top;
            int y2 = partialRectangle.Bottom;

            // Expands rectangle to include the entirety of partially selected tiles
            foreach (ArrangerElement el in Arranger.ElementGrid)
            {
                if (x1 > el.X1 && x1 <= el.X2)
                    x1 = el.X1;
                if (y1 > el.Y1 && y1 <= el.Y2)
                    y1 = el.Y1;
                if (x2 < el.X2 && x2 >= el.X1)
                    x2 = el.X2;
                if (y2 < el.Y2 && y2 >= el.Y1)
                    y2 = el.Y2;
            }

            x2++; // Fix edges
            y2++;

            // Clamp selection rectangle to max bounds of the arranger
            if (x1 < 0)
                x1 = 0;
            if (y1 < 0)
                y1 = 0;
            if (x2 >= Arranger.ArrangerPixelSize.Width)
                x2 = Arranger.ArrangerPixelSize.Width;
            if (y2 >= Arranger.ArrangerPixelSize.Height)
                y2 = Arranger.ArrangerPixelSize.Height;

            return new Rectangle(x1, y1, x2 - x1, y2 - y1);
        }

        private Rectangle ViewerToArrangerRectangle(Rectangle ClientRect)
        {
            int pleft = ClientRect.Left / Zoom;
            int ptop = ClientRect.Top / Zoom;
            int pright = (int)(ClientRect.Left / (float)Zoom + (ClientRect.Right - ClientRect.Left) / (float)Zoom);
            int pbottom = (int)(ClientRect.Top / (float)Zoom + (ClientRect.Bottom - ClientRect.Top) / (float)Zoom);

            Rectangle UnzoomedRect = new Rectangle(pleft, ptop, pright - pleft, pbottom - ptop);

            return UnzoomedRect;
        }

        private Rectangle PointsToRectangle(Point beginPoint, Point endPoint)
        {
            int top = beginPoint.Y < endPoint.Y ? beginPoint.Y : endPoint.Y;
            int bottom = beginPoint.Y > endPoint.Y ? beginPoint.Y : endPoint.Y;
            int left = beginPoint.X < endPoint.X ? beginPoint.X : endPoint.X;
            int right = beginPoint.X > endPoint.X ? beginPoint.X : endPoint.X;

            Rectangle rect = new Rectangle(left, top, (right - left), (bottom - top));
            return rect;
        }
    }
}
