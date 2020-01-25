namespace UglyToad.PdfPig.Parser.FileStructure
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Core;
    using CrossReference;
    using Logging;
    using Parts;

    internal class XrefCosOffsetChecker
    {
        private static readonly long MINIMUM_SEARCH_OFFSET = 6;

        private readonly ILog log;
        private readonly BruteForceSearcher bruteForceSearcher;

        private IReadOnlyDictionary<IndirectReference, long> objectKeyOffsets;

        public XrefCosOffsetChecker(ILog log, BruteForceSearcher bruteForceSearcher)
        {
            this.log = log;
            this.bruteForceSearcher = bruteForceSearcher;
        }

        private bool ValidateXrefOffsets(IInputBytes bytes, Dictionary<IndirectReference, long> xrefOffset)
        {
            if (xrefOffset == null)
            {
                return true;
            }

            foreach (var objectEntry in xrefOffset)
            {
                IndirectReference objectKey = objectEntry.Key;
                long objectOffset = objectEntry.Value;

                // a negative offset number represents a object number itself
                // see type 2 entry in xref stream
                if (objectOffset >= 0 && !CheckObjectKeys(bytes, objectKey, objectOffset))
                {
                    log.Debug($"Stop checking xref offsets as at least one ({objectKey}) couldn't be dereferenced");

                    return false;
                }
            }
            return true;
        }

        private bool CheckObjectKeys(IInputBytes bytes, IndirectReference objectKey, long offset)
        {
            // there can't be any object at the very beginning of a pdf
            if (offset < MINIMUM_SEARCH_OFFSET)
            {
                return false;
            }

            long objectNr = objectKey.ObjectNumber;
            long objectGen = objectKey.Generation;
            long originOffset = bytes.CurrentOffset;

            string objectString = ObjectHelper.CreateObjectString(objectNr, objectGen);

            try
            {
                bytes.Seek(offset);

                if (ReadHelper.IsWhitespace(bytes.CurrentByte))
                {
                    bytes.MoveNext();
                }
                
                if (ReadHelper.IsString(bytes, objectString))
                {
                    // everything is ok, return origin object key
                    bytes.Seek(originOffset);
                    return true;
                }
            }
            catch (Exception)
            {
                // Swallow the exception, obviously there isn't any valid object number
            }
            finally
            {
                bytes.Seek(originOffset);
            }

            // no valid object number found
            return false;
        }


        private IReadOnlyDictionary<IndirectReference, long> getBFCosObjectOffsets()
        {
            if (objectKeyOffsets == null)
            {
                var offsets = bruteForceSearcher.GetObjectLocations();

                objectKeyOffsets = offsets;
            }

            return objectKeyOffsets;
        }
        
        /// <summary>
        /// Check that the offsets in the cross reference are correct.
        /// </summary>
        public void CheckCrossReferenceOffsets(IInputBytes bytes, CrossReferenceTable xrefTrailerResolver, bool isLenientParsing)
        {
            // repair mode isn't available in non-lenient mode
            if (!isLenientParsing)
            {
                return;
            }

            Dictionary<IndirectReference, long> xrefOffset = xrefTrailerResolver.ObjectOffsets.ToDictionary(x => x.Key, x => x.Value);
            if (ValidateXrefOffsets(bytes, xrefOffset))
            {
                return;
            }

            IReadOnlyDictionary<IndirectReference, long> bfCOSObjectKeyOffsets = getBFCosObjectOffsets();
            if (bfCOSObjectKeyOffsets.Count > 0)
            {
                List<IndirectReference> objStreams = new List<IndirectReference>();
                // find all object streams
                foreach (var entry in xrefOffset)
                {
                    long offset = entry.Value;
                    if (offset < 0)
                    {
                        IndirectReference objStream = new IndirectReference(-offset, 0);
                        if (!objStreams.Contains(objStream))
                        {
                            objStreams.Add(new IndirectReference(-offset, 0));
                        }
                    }
                }
                // remove all found object streams
                if (objStreams.Count > 0)
                {
                    foreach (IndirectReference key in objStreams)
                    {
                        if (bfCOSObjectKeyOffsets.ContainsKey(key))
                        {
                            // remove all parsed objects which are part of an object stream
                            //ISet<long> objects = xrefTrailerResolver
                            //    .getContainedObjectNumbers((int)(key.Number));
                            //foreach (long objNr in objects)
                            //{
                            //    CosObjectKey streamObjectKey = new CosObjectKey(objNr, 0);

                            //    if (bfCOSObjectKeyOffsets.TryGetValue(streamObjectKey, out long streamObjectOffset) && streamObjectOffset > 0)
                            //    {
                            //        bfCOSObjectKeyOffsets.Remove(streamObjectKey);
                            //    }
                            //}
                        }
                        else
                        {
                            // remove all objects which are part of an object stream which wasn't found
                            //ISet<long> objects = xrefTrailerResolver
                            //    .getContainedObjectNumbers((int)(key.Number));
                            //foreach (long objNr in objects)
                            //{
                            //    xrefOffset.Remove(new CosObjectKey(objNr, 0));
                            //}
                        }
                    }
                }

                foreach (var item in bfCOSObjectKeyOffsets)
                {
                    xrefOffset[item.Key] = item.Value;
                }

            }
        }
    }
}
