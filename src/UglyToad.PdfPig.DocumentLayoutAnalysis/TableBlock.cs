namespace UglyToad.PdfPig.DocumentLayoutAnalysis
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UglyToad.PdfPig.Core;

    /// <summary>
    /// TableBlock
    /// </summary>
    public class TableBlock : BaseBlock
    {
        /// <summary>
        /// Cells
        /// </summary>
        public IReadOnlyList<TableCell> Cells { get; }

        /// <summary>
        /// Gets the number of rows in the table.
        /// </summary>
        public int Rows { get; }

        /// <summary>
        /// Gets the number of columns in the table.
        /// </summary>
        public int Columns { get; }

        /// <summary>
        /// From left to right and top to bottom.
        /// </summary>
        /// <param name="r">The row index, starting at 0.</param>
        /// <param name="c">The column index, starting at 0.</param>
        public TableCell this[int r, int c]
        {
            get
            {
                if (r >= Rows || c >= Columns)
                {
                    throw new ArgumentOutOfRangeException();
                }

                var candidates = Cells.Where(cell => cell.RowSpan.IsIn(r) && cell.ColumnSpan.IsIn(c));
                if (candidates.Count() > 1)
                {
                    throw new ArgumentException();
                }

                return candidates.FirstOrDefault();
            }
        }

        /// <summary>
        /// TableBlock
        /// </summary>
        /// <param name="cells"></param>
        public TableBlock(IEnumerable<TableCell> cells)
        {
            Cells = cells.ToList();
            BoundingBox = new PdfRectangle(cells.Min(c => c.BoundingBox.BottomLeft.X), cells.Min(c => c.BoundingBox.BottomLeft.Y),
                                           cells.Max(c => c.BoundingBox.TopRight.X), cells.Max(c => c.BoundingBox.TopRight.Y));
            Rows = cells.Select(c => c.RowSpan.End).Max();
            Columns = cells.Select(c => c.ColumnSpan.End).Max();
        }
    }

    /// <summary>
    /// Table Cell
    /// </summary>
    public class TableCell : BaseBlock
    {
        /// <summary>
        /// Index
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// TableCellType
        /// </summary>
        public TableCellType Type { get; }

        /// <summary>
        /// IsMerged
        /// </summary>
        public bool IsMerged => RowSpan.Length > 0 || ColumnSpan.Length > 0;

        /// <summary>
        /// RowSpan
        /// </summary>
        public CellSpan RowSpan { get; }

        /// <summary>
        /// ColumnSpan
        /// </summary>
        public CellSpan ColumnSpan { get; }

        /// <summary>
        /// UpdateRowSpan
        /// </summary>
        /// <param name="row"></param>
        public void UpdateRowSpan(int row)
        {
            if (RowSpan.Start == -1)
            {
                RowSpan.Start = row;
            }
            else if (row < RowSpan.Start)
            {
                RowSpan.Start = row;
            }

            if (RowSpan.End == -1)
            {
                RowSpan.End = row;
            }
            else if (row > RowSpan.End)
            {
                RowSpan.End = row;
            }
        }

        /// <summary>
        /// SetContent
        /// </summary>
        /// <param name="content"></param>
        public void SetContent(TextBlock content)
        {
            Children = new List<TextBlock>() { content };
        }

        /// <summary>
        /// UpdateColumnSpan
        /// </summary>
        /// <param name="column"></param>
        public void UpdateColumnSpan(int column)
        {
            if (ColumnSpan.Start == -1)
            {
                ColumnSpan.Start = column;
            }
            else if (column < ColumnSpan.Start)
            {
                ColumnSpan.Start = column;
            }

            if (ColumnSpan.End == -1)
            {
                ColumnSpan.End = column;
            }
            else if (column > ColumnSpan.End)
            {
                ColumnSpan.End = column;
            }
        }

        /// <summary>
        /// Table Cell
        /// </summary>
        /// <param name="boundingBox"></param>
        /// <param name="content"></param>
        /// <param name="index"></param>
        /// <param name="rowSpan"></param>
        /// <param name="columnSpan"></param>
        public TableCell(PdfRectangle boundingBox, TextBlock content, int index, CellSpan rowSpan, CellSpan columnSpan)
        {
            BoundingBox = boundingBox;
            Children = new[] { content };
            Index = index;
            RowSpan = rowSpan;
            ColumnSpan = columnSpan;
        }

        /// <summary>
        /// Table Cell
        /// </summary>
        /// <param name="boundingBox"></param>
        /// <param name="content"></param>
        /// <param name="index"></param>
        public TableCell(PdfRectangle boundingBox, TextBlock content, int index)
            : this(boundingBox, content, index, new CellSpan(), new CellSpan())
        {

        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Index + ", r=" + RowSpan.ToString() + ", c=" + ColumnSpan.ToString();
        }

        /// <summary>
        /// Table Cell Types
        /// </summary>
        public enum TableCellType
        {
            /// <summary>
            /// Unknown Table Cell Type
            /// </summary>
            Unknown,

            /// <summary>
            /// Header (column header) is usually top-most row (or set of multiple top-most rows) of a table and 
            /// defines the columns’ data. In some cases, header does not have to be in the top-most rows, however, 
            /// it still defines and categorizes columns’ data bellow it (e.g. in multi-tables).
            /// </summary>
            Header,

            /// <summary>
            /// Sub-header or super-row creates an additional dimension of the table and additionally, describes table 
            /// data. The sub-header row is usually placed between data rows, separating them by some dimension or 
            /// concept.
            /// </summary>
            SuperRow,

            /// <summary>
            /// The stub (row header) is typically the left-most column of the table, usually containing the list 
            /// of subjects or instances to which the values in the table body apply.
            /// </summary>
            Stub,

            /// <summary>
            /// Table body (data cells) contains the table’s data. Data cells are placed in the body of the table.
            /// Cells in the body represent the value of things (variables) orthe value of relationship defined in 
            /// headers, sub-headers and stub.
            /// </summary>
            Body
        }

        /// <summary>
        /// Cell Span
        /// </summary>
        public class CellSpan
        {
            /// <summary>
            /// Cell Span
            /// </summary>
            /// <param name="start"></param>
            /// <param name="end"></param>
            public CellSpan(int start, int end)
            {
                if (start > end)
                {
                    throw new ArgumentException();
                }

                Start = start;
                End = end;
            }

            /// <summary>
            /// Cell Span
            /// </summary>
            public CellSpan()
            {
                Start = -1;
                End = -1;
            }

            /// <summary>
            ///Start
            /// </summary>
            public int Start { get; internal set; }

            /// <summary>
            /// End
            /// </summary>
            public int End { get; internal set; }

            /// <summary>
            /// Length
            /// </summary>
            public int Length => End - Start + 1;

            /// <summary>
            /// Decrease
            /// </summary>
            public void Decrease()
            {
                Start--;
                End--;
            }

            /// <summary>
            /// Return true if the cell is present at the given index.
            /// </summary>
            /// <param name="cellIndex"></param>
            /// <returns></returns>
            public bool IsIn(int cellIndex)
            {
                return cellIndex >= Start && cellIndex <= End;
            }

            /// <summary>
            /// Contains
            /// </summary>
            /// <param name="cellSpan"></param>
            /// <returns></returns>
            public bool Contains(CellSpan cellSpan)
            {
                return this.End >= cellSpan.End && this.Start <= cellSpan.Start;
            }

            /// <summary>
            /// Equals
            /// </summary>
            /// <param name="obj"></param>
            /// <returns></returns>
            public override bool Equals(object obj)
            {
                if (obj is CellSpan span)
                {
                    return Start == span.Start && End == span.End;
                }
                return false;
            }

            /// <inheritdoc />
            public override int GetHashCode()
            {
                return (Start, End).GetHashCode();
            }

            /// <inheritdoc />
            public override string ToString()
            {
                if (Start == End) return "[" + Start + "]";
                return "[" + Start + "," + End + "]";
            }
        }
    }
}
