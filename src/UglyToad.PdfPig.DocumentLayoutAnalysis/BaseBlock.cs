namespace UglyToad.PdfPig.DocumentLayoutAnalysis
{
    using System;
    using System.Collections.Generic;
    using UglyToad.PdfPig.Core;

    /// <summary>
    /// Abstract class that represents a block with its bounding box and reading order.
    /// </summary>
    public abstract class BaseBlock
    {
        /// <summary>
        /// Children
        /// </summary>
        public IReadOnlyList<TextBlock> Children { get; protected set; }

        /// <summary>
        /// The rectangle completely containing the block.
        /// </summary>
        public virtual PdfRectangle BoundingBox { get; protected set; }

        /// <summary>
        /// The reading order index. Starts at 0. A value of -1 means the block is not ordered.
        /// </summary>
        public int ReadingOrder { get; protected set; } = -1;

        /// <summary>
        /// Sets the <see cref="TextBlock"/>'s reading order.
        /// </summary>
        /// <param name="readingOrder"></param>
        public void SetReadingOrder(int readingOrder)
        {
            if (readingOrder < -1)
            {
                throw new ArgumentException("The reading order should be greater or equal to -1. A value of -1 means the block is not ordered.", nameof(readingOrder));
            }
            ReadingOrder = readingOrder;
        }
    }
}
