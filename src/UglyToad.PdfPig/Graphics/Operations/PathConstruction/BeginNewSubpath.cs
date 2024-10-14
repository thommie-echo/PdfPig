﻿namespace UglyToad.PdfPig.Graphics.Operations.PathConstruction
{
    using System.IO;

    /// <inheritdoc />
    /// <summary>
    /// Begin a new subpath by moving the current point to coordinates (x, y), omitting any connecting line segment.
    /// </summary>
    public class BeginNewSubpath : IGraphicsStateOperation
    {
        /// <summary>
        /// The symbol for this operation in a stream.
        /// </summary>
        public const string Symbol = "m";

        /// <inheritdoc />
        public string Operator => Symbol;

        /// <summary>
        /// The x coordinate for the subpath to begin at.
        /// </summary>
        public double X { get; }

        /// <summary>
        /// The y coordinate for the subpath to begin at.
        /// </summary>
        public double Y { get; }

        /// <summary>
        /// Create a new <see cref="BeginNewSubpath"/>.
        /// </summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        public BeginNewSubpath(double x, double y)
        {
            X = x;
            Y = y;
        }

        /// <inheritdoc />
        public void Run(IOperationContext operationContext)
        {
            operationContext.MoveTo(X, Y);
        }

        /// <inheritdoc />
        public void Write(Stream stream)
        {
            stream.WriteDouble(X);
            stream.WriteWhiteSpace();
            stream.WriteDouble(Y);
            stream.WriteWhiteSpace();
            stream.WriteText(Symbol);
            stream.WriteNewLine();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{X} {Y} {Symbol}";
        }
    }
}