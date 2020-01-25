namespace UglyToad.PdfPig.Writer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Content;
    using Core;
    using CrossReference;
    using Encryption;
    using Filters;
    using Logging;
    using Merging;
    using Parser;
    using Parser.FileStructure;
    using Parser.Parts;
    using Tokenization.Scanner;
    using Tokens;

    /// <summary>
    /// Merges PDF documents into each other.
    /// </summary>
    internal static class PdfMerger
    {
        private static readonly ILog Log = new NoOpLog();

        private static readonly IFilterProvider FilterProvider = new MemoryFilterProvider(new DecodeParameterResolver(Log), 
            new PngPredictor(), Log);

        /// <summary>
        /// Merge two PDF documents together with the pages from <see cref="file1"/>
        /// followed by <see cref="file2"/>.
        /// </summary>
        public static byte[] Merge(string file1, string file2)
        {
            if (file1 == null)
            {
                throw new ArgumentNullException(nameof(file1));
            }

            if (file2 == null)
            {
                throw new ArgumentNullException(nameof(file2));
            }

            return Merge(new[]
            {
                File.ReadAllBytes(file1),
                File.ReadAllBytes(file2)
            });
        }

        /// <summary>
        /// Merge the set of PDF documents.
        /// </summary>
        public static byte[] Merge(IReadOnlyList<byte[]> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }

            foreach (var file in files)
            {
                var inputBytes = new ByteArrayInputBytes(file);
                var coreScanner = new CoreTokenScanner(inputBytes);

                _ = FileHeaderParser.Parse(coreScanner, true, Log);

                var bruteForceSearcher = new BruteForceSearcher(inputBytes);
                var xrefValidator = new XrefOffsetValidator(Log);
                var objectChecker = new XrefCosOffsetChecker(Log, bruteForceSearcher);

                var crossReferenceParser = new CrossReferenceParser(Log, xrefValidator, objectChecker);

                var crossReferenceOffset = FileTrailerParser.GetFirstCrossReferenceOffset(inputBytes, coreScanner, true);

                var objectLocations = bruteForceSearcher.GetObjectLocations();

                var pdfScanner = new PdfTokenScanner(inputBytes, new BruteForcedObjectLocationProvider(objectLocations), 
                    FilterProvider, NoOpEncryptionHandler.Instance);

                var crossReference = crossReferenceParser.Parse(inputBytes, true, crossReferenceOffset, pdfScanner, coreScanner, FilterProvider);

                var (trailerRef, catalogDictionaryToken) = ParseCatalog(crossReference, pdfScanner, out var encryptionDictionary);

                var trailerDictionary = crossReference.Trailer;

                pdfScanner.UpdateEncryptionHandler(new EncryptionHandler(encryptionDictionary, trailerDictionary, new []{ string.Empty }));

                var objectsTree = new ObjectsTree(trailerDictionary, pdfScanner.Get(trailerRef),
                    CatalogFactory.Create(crossReference.Trailer.Root, catalogDictionaryToken, pdfScanner, true));

                var root = pdfScanner.Get(trailerDictionary.Root);
                
                var tokens = new List<IToken>();

                while (pdfScanner.MoveNext())
                {
                    tokens.Add(pdfScanner.CurrentToken);
                }

                var isFull = tokens.Count == objectLocations.Count;
            }

            return null;
        }


        private static (IndirectReference, DictionaryToken) ParseCatalog(CrossReferenceTable crossReferenceTable,
            IPdfTokenScanner pdfTokenScanner,
            out EncryptionDictionary encryptionDictionary)
        {
            encryptionDictionary = null;

            if (crossReferenceTable.Trailer.EncryptionToken != null)
            {
                if (!DirectObjectFinder.TryGet(crossReferenceTable.Trailer.EncryptionToken, pdfTokenScanner, 
                    out DictionaryToken encryptionDictionaryToken))
                {
                    throw new PdfDocumentFormatException($"Unrecognized encryption token in trailer: {crossReferenceTable.Trailer.EncryptionToken}.");
                }

                encryptionDictionary = EncryptionDictionaryFactory.Read(encryptionDictionaryToken, pdfTokenScanner);
            }

            var rootDictionary = DirectObjectFinder.Get<DictionaryToken>(crossReferenceTable.Trailer.Root, pdfTokenScanner);

            if (!rootDictionary.ContainsKey(NameToken.Type))
            {
                rootDictionary = rootDictionary.With(NameToken.Type, NameToken.Catalog);
            }

            return (crossReferenceTable.Trailer.Root, rootDictionary);
        }

        private class BruteForcedObjectLocationProvider : IObjectLocationProvider
        {
            private readonly Dictionary<IndirectReference, long> objectLocations;
            private readonly Dictionary<IndirectReference, ObjectToken> cache = new Dictionary<IndirectReference, ObjectToken>();

            public BruteForcedObjectLocationProvider(IReadOnlyDictionary<IndirectReference, long> objectLocations)
            {
                this.objectLocations = objectLocations.ToDictionary(x => x.Key, x => x.Value);
            }

            public bool TryGetOffset(IndirectReference reference, out long offset)
            {
                return objectLocations.TryGetValue(reference, out offset);
            }

            public void UpdateOffset(IndirectReference reference, long offset)
            {
                objectLocations[reference] = offset;
            }

            public bool TryGetCached(IndirectReference reference, out ObjectToken objectToken)
            {
                return cache.TryGetValue(reference, out objectToken);
            }

            public void Cache(ObjectToken objectToken, bool force = false)
            {
                if (!TryGetOffset(objectToken.Number, out var offsetExpected) || force)
                {
                    cache[objectToken.Number] = objectToken;
                }

                if (offsetExpected != objectToken.Position)
                {
                    return;
                }

                cache[objectToken.Number] = objectToken;
            }
        }
    }
}
