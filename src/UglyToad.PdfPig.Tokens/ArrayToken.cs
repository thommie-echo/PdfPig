﻿namespace UglyToad.PdfPig.Tokens
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Core;

    /// <summary>
    /// An array object is a one-dimensional collection of objects arranged sequentially.
    /// PDF arrays may be heterogeneous; that is, an array's elements may be any combination of numbers, strings,
    /// dictionaries, or any other objects, including other arrays.
    /// </summary>
    public sealed class ArrayToken : IDataToken<IReadOnlyList<IToken>>
    {
        /// <summary>
        /// The tokens contained in this array.
        /// </summary>
        public IReadOnlyList<IToken> Data { get; }

        /// <summary>
        /// The number of tokens in this array.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Indexer into <see cref="Data"/> for convenience.
        /// </summary>
        public IToken this[int i] => Data[i];

        /// <summary>
        /// Create a new <see cref="ArrayToken"/>.
        /// </summary>
        /// <param name="data">The tokens contained by this array.</param>
        public ArrayToken(IReadOnlyList<IToken> data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var result = new List<IToken>(data.Count);
            for (var i = 0; i < data.Count; i++)
            {
                var token = data[i];

                if (i >= 2 && ReferenceEquals(token, OperatorToken.R) && (data[i - 1] is NumericToken generation) && (data[i - 2] is NumericToken objectNumber))
                {
                    // Clear the previous 2 tokens.
                    result.RemoveRange(result.Count - 2, 2);

                    result.Add(new IndirectReferenceToken(new IndirectReference(objectNumber.Long, generation.Int)));
                }
                else
                {
                    result.Add(token);
                }
            }

            Data = result;
            Length = Data.Count;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var builder = new StringBuilder("[ ");

            for (var i = 0; i < Data.Count; i++)
            {
                var token = Data[i];

                builder.Append(token);

                if (i < Data.Count - 1)
                {
                    builder.Append(',');
                }

                builder.Append(' ');
            }

            builder.Append(']');

            return builder.ToString();
        }
        
        /// <inheritdoc />
        public bool Equals(IToken obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (!(obj is ArrayToken other))
            {
                return false;
            }

            if (other.Length != Length)
            {
                return false;
            }

            for (var index = 0; index < Length; ++index)
            {
                if (!Data[index].Equals(other[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
