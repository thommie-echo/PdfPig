namespace UglyToad.PdfPig.DocumentLayoutAnalysis.TableExtractor
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UglyToad.PdfPig.Content;
    using UglyToad.PdfPig.Core;
    using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;
    using UglyToad.PdfPig.Geometry;
    using static UglyToad.PdfPig.Core.PdfSubpath;

    /// <summary>
    /// BaseTableExtractor
    /// </summary>
    public static class BaseTableExtractor
    {
        /*
         * From 'ANSSI NURMINEN ALGORITHMIC EXTRACTION OF DATA IN TABLES IN PDF DOCUMENTS', 4.4 Defining table structure
         * The table is defined as
         * having one of the following grid styles: full, supportive, outline, none. A full
         * grid means that the cellular structure of the table is completely defined by
         * the rectangular areas and no further processing of the table body is needed,
         * and the algorithm skips to Step 11, Finding the header. All elements outside
         * a full or an outline style grid are set as being a part of the title or the caption.
         * A Supportive grid helps to determine cell row- and column spans, but otherwise
         * the elements are processed just like the grid would not exist at all. For
         * fully gridded tables, the performance of the edge detection algorithm is critical.
         * Any mistakes, or missed borderlines, directly show up as errors in the
         * row and column definitions.
         */

        // Also see: 
        // https://www.research.manchester.ac.uk/portal/files/70405100/FULL_TEXT.PDF

        /*
         * - Need to better handle rouding when comparing points / rectangles
         * because duplicates create problems
         * 
         * - Need to consider if rectangles are filed, and the color of lines
         * - Force close filled shapes
         */


        /// <summary>
        /// GetTables
        /// </summary>
        /// <param name="page"></param>
        /// <param name="words"></param>
        /// <param name="minCellsInTable"></param>
        /// <returns></returns>
        public static IEnumerable<TableBlock> GetTables(Page page, IEnumerable<Word> words, int minCellsInTable = 4)
        {
            var candidates = GetCandidates(page, minCellsInTable).ToList();
            foreach (var candidate in candidates)
            {
                List<TableCell> cells = candidate.Select(c => new TableCell(c.Item2, null, c.Item1))
                    .OrderByDescending(x => x.BoundingBox.Top).ThenBy(x => x.BoundingBox.Left).ToList();

                // merge rows
                int[] shouldMerge = Enumerable.Repeat(-1, cells.Count).ToArray();
                for (var b = 0; b < cells.Count; b++)
                {
                    var current1 = cells[b];
                    var centroid = current1.BoundingBox.Centroid;

                    for (var c = 0; c < cells.Count; c++)
                    {
                        if (b == c) continue;
                        var current2 = cells[c];

                        if (Math.Abs(centroid.Y - current2.BoundingBox.Centroid.Y) < 1e-5 && shouldMerge[c] != b)
                        {
                            shouldMerge[b] = c;
                            break;
                        }
                    }
                }

                var merged = Clustering.GroupIndexes(shouldMerge);
                for (int row = 0; row < merged.Count; row++)
                {
                    foreach (var r in merged[row].Select(i => cells[i]))
                    {
                        r.UpdateRowSpan(row);
                    }
                }

                // merge columns
                cells = cells.OrderBy(x => x.BoundingBox.Left).ToList();
                shouldMerge = Enumerable.Repeat(-1, cells.Count).ToArray();
                for (var b = 0; b < cells.Count; b++)
                {
                    var current1 = cells[b];
                    var centroid = current1.BoundingBox.Centroid;

                    for (var c = 0; c < cells.Count; c++)
                    {
                        if (b == c) continue;
                        var current2 = cells[c];

                        if (Math.Abs(centroid.X - current2.BoundingBox.Centroid.X) < 1e-5 && shouldMerge[c] != b)
                        {
                            shouldMerge[b] = c;
                            break;
                        }
                    }
                }

                merged = Clustering.GroupIndexes(shouldMerge);
                for (int col = 0; col < merged.Count; col++)
                {
                    foreach (var c in merged[col].Select(i => cells[i]))
                    {
                        c.UpdateColumnSpan(col);
                    }
                }

                // TO DO: The final index is still wrong!

                // handle spaned rows
                for (int i = 0; i < cells.Count; i++)
                {
                    var currentCell = cells[i];
                    for (int j = 0; j < cells.Count; j++)
                    {
                        if (i == j) continue;
                        var otherCell = cells[j];
                        if (currentCell.RowSpan.Equals(otherCell.RowSpan)) continue;

                        if (currentCell.BoundingBox.Top >= otherCell.BoundingBox.Centroid.Y &&
                            currentCell.BoundingBox.Bottom <= otherCell.BoundingBox.Centroid.Y &&
                            currentCell.BoundingBox.Height > otherCell.BoundingBox.Height)
                        {
                            currentCell.UpdateRowSpan(otherCell.RowSpan.Start);
                            currentCell.UpdateRowSpan(otherCell.RowSpan.End);
                        }
                    }
                }

                // handle spaned columns
                for (int i = 0; i < cells.Count; i++)
                {
                    var currentCell = cells[i];
                    for (int j = 0; j < cells.Count; j++)
                    {
                        if (i == j) continue;
                        var otherCell = cells[j];
                        if (currentCell.ColumnSpan.Equals(otherCell.ColumnSpan)) continue;

                        if (currentCell.BoundingBox.Right >= otherCell.BoundingBox.Centroid.X &&
                            currentCell.BoundingBox.Left <= otherCell.BoundingBox.Centroid.X &&
                            currentCell.BoundingBox.Width > otherCell.BoundingBox.Width)
                        {
                            currentCell.UpdateColumnSpan(otherCell.ColumnSpan.Start);
                            currentCell.UpdateColumnSpan(otherCell.ColumnSpan.End);
                        }
                    }
                }

                foreach (var cell in cells)
                {
                    var containedWords = words.Where(w => cell.BoundingBox.Contains(w.BoundingBox));
                    if (containedWords.Any())
                    {
                        cell.SetContent(new TextBlock(containedWords.Select(w => new TextLine(new[] { w })).ToList()));
                    }
                }

                yield return new TableBlock(cells);
            }
        }

        /// <summary>
        /// GetTables
        /// </summary>
        /// <param name="page"></param>
        /// <param name="minCellsInTable"></param>
        /// <returns></returns>
        public static IEnumerable<TableBlock> GetTables(Page page, int minCellsInTable = 4)
        {
            
            var letters = page.Letters;
            var words = NearestNeighbourWordExtractor.Instance.GetWords(letters);

            return GetTables(page, words, minCellsInTable);
        }

        /// <summary>
        /// GetCandidates
        /// </summary>
        /// <param name="page"></param>
        /// <param name="minCellsInTable"></param>
        /// <returns></returns>
        public static IEnumerable<List<(int, PdfRectangle)>> GetCandidates(Page page, int minCellsInTable = 4)
        {
            var processedLines = GetProcessLines(page);
            var intersectionPoints = GetIntersections(processedLines);
            var foundRectangles = GetRectangularAreas(intersectionPoints);
            return GroupRectanglesInTable(foundRectangles).Where(c => c.Count > minCellsInTable);
        }

        /// <summary>
        /// Remove clipping paths, BezierCurve, etc.
        /// Normalise lines.
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public static IReadOnlyList<PdfLine> GetProcessLines(Page page)
        {
            double modeWidth = 1;
            double modeHeight = 1;
            var filteredLetters = page.Letters.Where(x => !string.IsNullOrWhiteSpace(x.Value)).ToList();

            if(filteredLetters.Count > 0)
            {
                modeWidth = filteredLetters.Select(x => x.GlyphRectangle.Width).Mode();
                modeHeight = filteredLetters.Select(x => x.GlyphRectangle.Height).Mode();
            }

            //Console.WriteLine("modeWidth=" + modeWidth.ToString());
            //Console.WriteLine("modeHeight=" + modeHeight.ToString());

            // See 'Configurable Table Structure Recognition in Untagged PDF Documents' by Alexey Shigarov
            // 2.1 Preprocessing
            // 1. We also split each rectangle into four rulings corresponding to its boundaries
            var processedLinesSet = new HashSet<PdfLine>();

            foreach (var pdfPath in page.ExperimentalAccess.Paths)
            {
                if(pdfPath.IsClipping || (!pdfPath.IsFilled && !pdfPath.IsStroked))
                {
                    // remove clipping and invisible paths

                    // log with warning to set ClipPaths to true
                    continue;
                }

                if(pdfPath.Any(p => p.Commands.Any(c => c is BezierCurve))) continue; // filter out any path containing a bezier curve

                foreach (var subPath in pdfPath)
                {
                    // force close filled path
                    if(pdfPath.IsFilled)
                    {
                        if(!(subPath.Commands[0] is Move move))
                        {
                            throw new Exception(); // or continue?
                        }

                        if(!subPath.Commands.Any(c => c is Close)) // does not contain a close command
                        {
                            if(!(subPath.Commands[subPath.Commands.Count -1] is Line line))
                            {
                                throw new Exception();
                                // should not happen as we filtered out bezier curve and it cannot be a 'close' command
                            }

                            if(!move.Location.Equals(line.To))
                            {
                                subPath.LineTo(move.Location.X, move.Location.Y); // force close path
                            }
                        }
                        else // contains a close command
                        {
                            if(!(subPath.Commands[subPath.Commands.Count - 2] is Line line))
                            {
                                throw new Exception();
                                // should not happen as we filtered out bezier curve and it cannot be a 'close' command
                            }

                            if(!move.Location.Equals(line.To))
                            {
                                subPath.LineTo(move.Location.X, move.Location.Y); // force close path
                            }
                        }
                    }

                    // handle filled rectangle to check if they are in fact lines
                    if(pdfPath.IsFilled && IsRectangle(subPath))
                    {
                        var rect = subPath.GetBoundingRectangle();
                        if(!rect.HasValue) continue;

                        if(rect.Equals(page.CropBox.Bounds)) continue; // ignore rectangle that are the size of the page

                        if(rect.Value.Width < modeWidth * 0.7)
                        {
                            if(rect.Value.Height < modeHeight * 0.7)
                            {
                                var centroid = rect.Value.Centroid;
                                processedLinesSet.Add(ExtendLine(Normalise(new PdfLine(centroid.X, rect.Value.Bottom, centroid.X, rect.Value.Top)), 2));
                                processedLinesSet.Add(ExtendLine(Normalise(new PdfLine(rect.Value.Left, centroid.Y, rect.Value.Right, centroid.Y)), 2));
                            }
                            else
                            {
                                var x = rect.Value.Centroid.X;
                                processedLinesSet.Add(ExtendLine(Normalise(new PdfLine(x, rect.Value.Bottom, x, rect.Value.Top)), 2));
                            }
                            continue;
                        }
                        else if(rect.Value.Height < modeHeight * 0.7)
                        {
                            if(rect.Value.Width < modeWidth * 0.7)
                            {
                                var centroid = rect.Value.Centroid;
                                processedLinesSet.Add(ExtendLine(Normalise(new PdfLine(rect.Value.Left, centroid.Y, rect.Value.Right, centroid.Y)), 2));
                                processedLinesSet.Add(ExtendLine(Normalise(new PdfLine(centroid.X, rect.Value.Bottom, centroid.X, rect.Value.Top)), 2));
                            }
                            else
                            {
                                var y = rect.Value.Centroid.Y;
                                processedLinesSet.Add(ExtendLine(Normalise(new PdfLine(rect.Value.Left, y, rect.Value.Right, y)), 2));
                            }
                            continue;
                        }
                    }

                    foreach (var command in subPath.Commands)
                    {
                        if(command is Line line)
                        {
                            line = Normalise(line);

                            // vertical and horizontal lines only  
                            if(line.From.X != line.To.X && line.From.Y != line.To.Y) continue;

                            PdfLine pdfLine = ExtendLine(new PdfLine(line.From, line.To), 2);
                            processedLinesSet.Add(pdfLine);
                        }
                    }
                }
            }



            // 2. We merge all segments of one visual line into one ruling.
            var processedLines = processedLinesSet.ToList();
            int[] shouldMerge = Enumerable.Repeat(-1, processedLinesSet.Count).ToArray();
            for (var b = 0; b < processedLinesSet.Count; b++)
            {
                var current1 = processedLines[b];

                for (var c = 0; c < processedLinesSet.Count; c++)
                {
                    if(b == c) continue;
                    var current2 = processedLines[c];

                    if(shouldMerge[c] != b && ShouldMerge(current1, current2))
                    {
                        shouldMerge[b] = c;
                        break;
                    }
                }
            }

            var merged = Clustering.GroupIndexes(shouldMerge);

            var valid = new List<PdfLine>();
            for (int a = 0; a < merged.Count; a++)
            {
                var group = merged[a].Select(i => processedLines[i]).ToList();
                if(group.Count == 1)
                {
                    valid.Add(group[0]);
                    continue;
                }

                var first = group[0];
                if(first.Point1.X == first.Point2.X) // vertical lines
                {
                    var mergedLine = new PdfLine(first.Point1.X, Math.Min(group.Min(x => x.Point1.Y), group.Min(x => x.Point2.Y)),
                                          first.Point1.X, Math.Max(group.Max(x => x.Point1.Y), group.Max(x => x.Point2.Y)));
                    if(!group.All(l => l.Length <= mergedLine.Length))
                    {
                        throw new Exception();
                    }
                    valid.Add(mergedLine);
                }
                else // horizontal lines
                {
                    var mergedLine = new PdfLine(Math.Min(group.Min(x => x.Point1.X), group.Min(x => x.Point2.X)), first.Point1.Y,
                                          Math.Max(group.Max(x => x.Point1.X), group.Max(x => x.Point2.X)), first.Point1.Y);
                    if(!group.All(l => l.Length <= mergedLine.Length))
                    {
                        throw new Exception();
                    }
                    valid.Add(mergedLine);
                }
            }
            return valid; //.Where(l => l.Length > Math.Max(modeWidth * 2, modeHeight * 2)).ToList();
        }

        private static bool IsRectangle(PdfSubpath subpath)
        {
            if(subpath.IsDrawnAsRectangle) return true;

            if(subpath.Commands.Any(c => c is BezierCurve)) return false; // redundant

            var lines = subpath.Commands.OfType<Line>().ToList();

            if(lines.Count == 3 || lines.Count == 4)
            {
                var l0 = lines[0];
                var l1 = lines[1];
                var l2 = lines[2];

                // check if contains slanted lines
                if(l0.From.X != l0.To.X && l0.From.Y != l0.To.Y) return false;
                if(l1.From.X != l1.To.X && l1.From.Y != l1.To.Y) return false;
                if(l2.From.X != l2.To.X && l2.From.Y != l2.To.Y) return false;

                // more checks...
                return true;
            }

            return false;
        }

        private static Line Normalise(Line line)
        {
            //return line;
            return new Line(new PdfPoint(Math.Round(line.From.X, 1), Math.Round(line.From.Y, 1)),
                            new PdfPoint(Math.Round(line.To.X, 1), Math.Round(line.To.Y, 1)));
        }

        private static PdfLine Normalise(PdfLine line)
        {
            //return line;
            return new PdfLine(new PdfPoint(Math.Round(line.Point1.X, 1), Math.Round(line.Point1.Y, 1)),
                               new PdfPoint(Math.Round(line.Point2.X, 1), Math.Round(line.Point2.Y, 1)));
        }

        private static bool ShouldMerge(PdfLine line1, PdfLine line2)
        {
            // might add aditional checks of same color, stroke, etc...
            bool line1Vert = line1.Point1.X == line1.Point2.X; // if false, horizontal
            bool line2Vert = line2.Point1.X == line2.Point2.X; // if false, horizontal

            if(line1Vert ^ line2Vert) return false;

            if(line1Vert) // line 1 is vertical
            {
                if(line1.Point1.X != line2.Point1.X) return false; // both lines do not share same X coord
            }
            else // line 1 is horizontal
            {
                if(line1.Point1.Y != line2.Point1.Y) return false;  // both lines do not share same Y coord
            }

            return line2.Contains(line1.Point1) ||
                   line2.Contains(line1.Point2) ||
                   line1.Contains(line2.Point1) ||
                   line1.Contains(line2.Point2);
        }

        private static PdfLine ExtendLine(PdfLine line, double pixel)
        {
            // vertical and horizontal lines only
            PdfPoint start;
            PdfPoint end;
            if(line.Point1.X == line.Point2.X)
            {
                start = (line.Point1.Y < line.Point2.Y) ? line.Point1 : line.Point2;
                end = (line.Point1.Y >= line.Point2.Y) ? line.Point1 : line.Point2;
                start = start.MoveY(-pixel);
                end = end.MoveY(pixel);
            }
            else if(line.Point1.Y == line.Point2.Y)
            {
                start = (line.Point1.X < line.Point2.X) ? line.Point1 : line.Point2;
                end = (line.Point1.X >= line.Point2.X) ? line.Point1 : line.Point2;
                start = start.MoveX(-pixel);
                end = end.MoveX(pixel);
            }
            else
            {
                throw new Exception();
            }

            return new PdfLine(start, end);
        }

        /// <summary>
        /// GetIntersections
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        public static Dictionary<PdfPoint, (PdfLine Hor, PdfLine Vert)> GetIntersections(IReadOnlyList<PdfLine> lines)
        {
            var intersectionPoints = new Dictionary<PdfPoint, (PdfLine Hor, PdfLine Vert)>();

            for (var b = 0; b < lines.Count; b++)
            {
                var current1 = lines[b];

                for (var c = 0; c < lines.Count; c++)
                {
                    var current2 = lines[c];
                    if(b == c) continue;

                    var intersection = current1.Intersect(current2);
                    if(intersection.HasValue)
                    {
                        // round coordinates to avoid duplicates
                        PdfPoint roundedIntersection = new PdfPoint(Math.Round(intersection.Value.X, 5),
                                                                    Math.Round(intersection.Value.Y, 5));

                        if(intersectionPoints.ContainsKey(roundedIntersection)) continue;

                        if(Math.Round(current1.Point1.X, 5) == Math.Round(current1.Point2.X, 5))
                        {
                            intersectionPoints[roundedIntersection] = (current2, current1);
                        }
                        else
                        {
                            intersectionPoints[roundedIntersection] = (current1, current2);
                        }
                    }
                }
            }
            return intersectionPoints;
        }

        /// <summary>
        /// Identify closed rectangular spaces within the vertical and horizontal separator lines
        /// see pseudo code in 'ANSSI NURMINEN ALGORITHMIC EXTRACTION OF DATA IN TABLES IN PDF DOCUMENTS'
        /// 4.2.4 Finding rectangular areas
        /// </summary>
        public static IReadOnlyList<PdfRectangle> GetRectangularAreas(Dictionary<PdfPoint, (PdfLine Hor, PdfLine Vert)> intersectionPoints)
        {
            List<PdfRectangle> foundRectangles = new List<PdfRectangle>();

            // All crossing-points have been sorted from up to down, and left to right in ascending order
            var crossingPointsStack = new Stack<KeyValuePair<PdfPoint, (PdfLine Hor, PdfLine Vert)>>(intersectionPoints
                .OrderByDescending(p => p.Key.X).OrderByDescending(p => p.Key.Y)); // stack inverses the order

            while (crossingPointsStack.Count > 0)
            {
                var currentCrossingPoint = crossingPointsStack.Pop();

                // Fetch all points on the same vertical and horizontal line with current crossing point
                var x_points = CrossingPointsDirectlyBelow(currentCrossingPoint, crossingPointsStack);
                var y_points = CrossingPointsDirectlyToTheRight(currentCrossingPoint, crossingPointsStack);

                foreach (var x_point in x_points)
                {
                    var verticalCandidate = intersectionPoints[x_point.Key];

                    if(!EdgeExistsBetween(currentCrossingPoint.Value, verticalCandidate, false)) goto NextCrossingPoint;

                    foreach (var y_point in y_points)
                    {
                        var horizontalCandidate = intersectionPoints[y_point.Key];

                        if(!EdgeExistsBetween(currentCrossingPoint.Value, horizontalCandidate, true)) goto NextCrossingPoint;

                        // Hypothetical bottom right point of rectangle
                        var oppositeIntersection = new PdfPoint(y_point.Key.X, x_point.Key.Y);

                        if(!intersectionPoints.ContainsKey(oppositeIntersection)) continue;
                        var oppositeCandidate = intersectionPoints[oppositeIntersection];
                        if(EdgeExistsBetween(oppositeCandidate, verticalCandidate, true) &&
                            EdgeExistsBetween(oppositeCandidate, horizontalCandidate, false))
                        {
                            // Rectangle is confirmed to have 4 sides
                            foundRectangles.Add(new PdfRectangle(currentCrossingPoint.Key.X, currentCrossingPoint.Key.Y,
                                                                 oppositeIntersection.X, oppositeIntersection.Y));

                            // Each crossing point can be the top left corner of only a single rectangle
                            goto NextCrossingPoint;
                        }
                    }
                }
                NextCrossingPoint:;
            }
            return foundRectangles;
        }

        private static bool EdgeExistsBetween((PdfLine Hor, PdfLine Vert) candidate1, (PdfLine Hor, PdfLine Vert) candidate2, bool horizontal)
        {
            if(horizontal)
            {
                return candidate1.Hor.Equals(candidate2.Hor);
            }
            else
            {
                return candidate1.Vert.Equals(candidate2.Vert);
            }
        }

        private static IReadOnlyList<KeyValuePair<PdfPoint, (PdfLine Hor, PdfLine Vert)>> CrossingPointsDirectlyBelow(KeyValuePair<PdfPoint, (PdfLine Hor, PdfLine Vert)> currentCrossingPoint, Stack<KeyValuePair<PdfPoint, (PdfLine Hor, PdfLine Vert)>> crossingPoints)
        {
            return crossingPoints.Where(p => p.Key.X == currentCrossingPoint.Key.X && p.Key.Y > currentCrossingPoint.Key.Y).ToList();
        }

        private static IReadOnlyList<KeyValuePair<PdfPoint, (PdfLine Hor, PdfLine Vert)>> CrossingPointsDirectlyToTheRight(KeyValuePair<PdfPoint, (PdfLine Hor, PdfLine Vert)> currentCrossingPoint, Stack<KeyValuePair<PdfPoint, (PdfLine Hor, PdfLine Vert)>> crossingPoints)
        {
            return crossingPoints.Where(p => p.Key.Y == currentCrossingPoint.Key.Y && p.Key.X > currentCrossingPoint.Key.X).ToList();
        }

        /// <summary>
        /// GroupRectanglesInTable
        /// </summary>
        /// <param name="rectangles"></param>
        /// <returns></returns>
        public static IEnumerable<List<(int, PdfRectangle)>> GroupRectanglesInTable(IReadOnlyList<PdfRectangle> rectangles)
        {
            if(rectangles == null || rectangles.Count == 0) yield break;

            var ordered = rectangles.OrderByDescending(x => x.Bottom).ThenByDescending(x => x.Left).ToList();

            int[][] indexGrouped = new int[rectangles.Count][];
            double threshold = 0.5;
            for (int i = 0; i < ordered.Count; i++)
            {
                // TODO: Or use a list? then convert to array
                //List<int> group = new List<int>();
                indexGrouped[i] = Enumerable.Repeat(-1, 10).ToArray(); // theor max neigh is 8
                int f = 0;
                for (int j = 0; j < ordered.Count; j++)
                {
                    if(i == j) continue;
                    if(ShareCorner(ordered[i], ordered[j], threshold))
                    {
                        indexGrouped[i][f++] = j;
                        //group.Add(j);
                    }
                    if(f > 9) break; // theor max neigh is 8
                }
                //indexGrouped[i] = group.ToArray(); //indexGrouped[i].Where(x => x != -1).ToArray();
            }

            var groupedIndexes = Clustering.GroupIndexes(indexGrouped);

            for (int a = 0; a < groupedIndexes.Count; a++)
            {
                yield return groupedIndexes[a].Select(i => (i, ordered[i])).ToList();
            }
        }

        private static bool ShareCorner(PdfRectangle pivot, PdfRectangle candidate, double distanceThreshold = 1.0)
        {
            return Distances.Euclidean(pivot.BottomLeft, candidate.BottomRight) <= distanceThreshold ||
                   Distances.Euclidean(pivot.BottomLeft, candidate.TopRight) <= distanceThreshold ||
                   Distances.Euclidean(pivot.BottomLeft, candidate.TopLeft) <= distanceThreshold ||
                   
                   Distances.Euclidean(pivot.BottomRight, candidate.BottomLeft) <= distanceThreshold ||
                   Distances.Euclidean(pivot.BottomRight, candidate.TopRight) <= distanceThreshold ||
                   Distances.Euclidean(pivot.BottomRight, candidate.TopLeft) <= distanceThreshold ||
                   
                   Distances.Euclidean(pivot.TopLeft, candidate.BottomLeft) <= distanceThreshold ||
                   Distances.Euclidean(pivot.TopLeft, candidate.BottomRight) <= distanceThreshold ||
                   Distances.Euclidean(pivot.TopLeft, candidate.TopRight) <= distanceThreshold ||
                   
                   Distances.Euclidean(pivot.TopRight, candidate.BottomRight) <= distanceThreshold ||
                   Distances.Euclidean(pivot.TopRight, candidate.BottomLeft) <= distanceThreshold ||
                   Distances.Euclidean(pivot.TopRight, candidate.TopLeft) <= distanceThreshold;
        }
    }
}
