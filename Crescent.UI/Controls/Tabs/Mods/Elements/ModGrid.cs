using Avalonia;
using Avalonia.Controls;

namespace Crescent.UI.Controls.Tabs.Elements
{
    public class ModGrid : Panel
    {
        public double MinColumnWidth { get; set; } = 265;
        public int FixedColumns { get; set; }

        private int GetColumnCount(double availableWidth)
        {
            if (FixedColumns > 0) return FixedColumns;
            if (double.IsInfinity(availableWidth) || availableWidth <= 0) return 3;
            return Math.Max(1, (int)(availableWidth / MinColumnWidth));
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            int count = Children.Count;
            if (count == 0) return default;

            int columns = GetColumnCount(availableSize.Width);
            double columnWidth = (double.IsInfinity(availableSize.Width) || availableSize.Width <= 0) ? MinColumnWidth : availableSize.Width / columns;

            Size cellConstraint = new(columnWidth, double.PositiveInfinity);
            double totalHeight = 0;

            for (int rowStart = 0; rowStart < count; rowStart += columns)
            {
                double rowHeight = 0;
                int rowEnd = Math.Min(rowStart + columns, count);
                for (int index = rowStart; index < rowEnd; index++)
                {
                    Control child = Children[index];
                    child.Measure(cellConstraint);
                    if (child.DesiredSize.Height > rowHeight) rowHeight = child.DesiredSize.Height;
                }

                totalHeight += rowHeight;
            }

            double finalWidth = double.IsInfinity(availableSize.Width) || availableSize.Width <= 0 ? columnWidth * columns : availableSize.Width;
            return new Size(finalWidth, totalHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            int count = Children.Count;
            if (count == 0) return finalSize;

            int columns = GetColumnCount(finalSize.Width);
            double columnWidth = finalSize.Width > 0 ? finalSize.Width / columns : MinColumnWidth;
            double currentY = 0;

            for (int rowStart = 0; rowStart < count; rowStart += columns)
            {
                double rowHeight = 0;
                int rowEnd = Math.Min(rowStart + columns, count);
                for (int index = rowStart; index < rowEnd; index++)
                {
                    double desiredHeight = Children[index].DesiredSize.Height;
                    if (desiredHeight > rowHeight) rowHeight = desiredHeight;
                }

                for (int index = rowStart; index < rowEnd; index++)
                {
                    int column = index - rowStart;
                    Control child = Children[index];
                    double childHeight = child.DesiredSize.Height > 0 ? child.DesiredSize.Height : rowHeight;
                    child.Arrange(new Rect(column * columnWidth, currentY, columnWidth, childHeight));
                }

                currentY += rowHeight;
            }

            return new Size(finalSize.Width, currentY);
        }
    }
}