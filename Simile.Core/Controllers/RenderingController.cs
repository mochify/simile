using System;
using System.Collections.Generic;
using System.Drawing;

using Mochify.Simile.Core.Imaging;
using Mochify.Simile.Core.Utils;

using System.Threading.Tasks;

using System.IO;

namespace Mochify.Simile.Core.Controllers
{
    public class RenderingController
    {
        private IImageComparer _imageComparer;
        private IAssetManager _fileManager;

        public RenderingController(IImageComparer comparer, IAssetManager fileManager)
        {
            if (comparer == null)
            {
                throw new ArgumentNullException("comparer");
            }
            _imageComparer = comparer;
            _fileManager = fileManager;
        }

        public IEnumerable<TestResult> RunTests(IEnumerable<TestCase> testCases)
        {
            var results = new List<TestResult>();
            try
            {
                var x = Parallel.ForEach(testCases, tc =>
                {
                    results.Add(RunTest(tc));
                });
            } catch (Exception e)
            {
                // do nothing yet...
            }
            /*foreach (var tc in testCases)
            {
                yield return RunTest(tc);
            }*/
            return results;
        }

        private TestResult RunTest(TestCase tc)
        {
            var testId = tc.TestId;
            var sourceImage = tc.SourceLocation;
            var type = tc.Type;
            var refImage = tc.ReferenceLocation;

            var result = new TestResult().ForTestId(testId);
            result.OriginalReferenceLocation = tc.ReferenceLocation;
            result.OriginalSourceLocation = tc.SourceLocation;
            try
            {
                using (var preview = new Bitmap(_fileManager.Get(sourceImage)))
                using (var reference = new Bitmap(_fileManager.Get(refImage)))
                {
                    if (!_imageComparer.Matches(reference, preview))
                    {
                        result.TestPassed = false;
                        result.WithComment("Source and Reference image do not match according to threshold.");
                    }
                    else
                    {
                        result.TestPassed = true;
                        result.WithComment("Passed");
                    }

                    var diffImage = _imageComparer.GenerateDifferenceMap(reference, preview);

                    // Because Bitmaps need their streams kept open to be useful, and
                    // I'd rather not pass the streams along so that I can manage them,
                    // just create a copy of the reference and preview bitmaps here

                    result.ReferenceImage = new Bitmap(reference);
                    result.ReferenceFormatHint = reference.RawFormat;
                    result.SourceImage = new Bitmap(preview);
                    result.SourceFormatHint = preview.RawFormat;
                    result.DifferenceImage = diffImage;
                    result.DifferenceFormatHint = diffImage.RawFormat;
                }
            }
            catch (Exception e)
            {
                if (e is ArgumentException || e is IOException)
                {
                    result.TestPassed = false;
                    result.WithComment(e.Message); 
                    if (e.InnerException != null)
                    {
                        result.WithComment(e.InnerException.Message);
                    }
                    return result;
                }
                throw;
            }

            return result;
        }

    }
}
