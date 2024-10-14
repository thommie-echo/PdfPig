﻿namespace UglyToad.PdfPig.Graphics.Operations.General
{
    using System.IO;
    using Core;

    /// <inheritdoc />
    /// <summary>
    /// Set the line dash pattern in the graphics state.
    /// </summary>
    public class SetLineDashPattern : IGraphicsStateOperation
    {
        /// <summary>
        /// The symbol for this operation in a stream.
        /// </summary>
        public const string Symbol = "d";

        /// <inheritdoc />
        public string Operator => Symbol;

        /// <summary>
        /// The line dash pattern.
        /// </summary>
        public LineDashPattern Pattern { get; }

        /// <summary>
        /// Create a new <see cref="SetLineDashPattern"/>.
        /// </summary>
        public SetLineDashPattern(double[] array, int phase)
        {
            Pattern = new LineDashPattern(phase, array);
        }

        /// <inheritdoc />
        public void Run(IOperationContext operationContext)
        {
            operationContext.SetLineDashPattern(Pattern);
        }

        /// <inheritdoc />
        public void Write(Stream stream)
        {
            stream.WriteText("["u8);

            for (var i = 0; i < Pattern.Array.Count; i++)
            {
                var value = Pattern.Array[i];
                stream.WriteDouble(value);

                if (i < Pattern.Array.Count - 1)
                {
                    stream.WriteWhiteSpace();
                }
            }

            stream.WriteText("]"u8);

            stream.WriteWhiteSpace();

            stream.WriteDouble(Pattern.Phase);

            stream.WriteWhiteSpace();

            stream.WriteText(Symbol);

            stream.WriteNewLine();
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Pattern.Array} {Pattern.Phase} {Symbol}";
        }
    }
}