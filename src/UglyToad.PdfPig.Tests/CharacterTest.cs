namespace UglyToad.PdfPig.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using UglyToad.PdfPig.Core;
    using UglyToad.PdfPig.Logging;
    using Xunit;

    public class CharacterTest
    {

        [Fact]
        public void CorrectlyWritesOperations()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Schiphol HY.pdf");

            using (PdfDocument document = PdfDocument.Open(path))
            {
                foreach (Content.Page page in document.GetPages())
                {
                    foreach(Content.Letter letter in page.Letters)
                    {
                        if (letter.Value.Contains("fi"))
                        {
                            Debug.WriteLine(letter.Value);
                        }
                    }
                }
            }
        }
    }
}
