﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Renderly.Imaging;
using Renderly.Utils;

namespace Renderly.Controllers
{
    public class RenderingController
    {
        private IImageComparer _imageComparer;
        private IRenderlyAssetManager _fileManager;

        public RenderingController(IImageComparer comparer, IRenderlyAssetManager fileManager)
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
            foreach (var tc in testCases)
            {
                yield return RunTest(tc);
            }
        }

        private TestResult RunTest(TestCase tc)
        {
            var testId = tc.TestId;
            var sourceImage = tc.SourceLocation;
            var type = tc.Type;
            var refImage = tc.ReferenceLocation;

            var result = new TestResult().ForTestId(testId);
            result.OriginalReferenceLocation = tc.ReferenceLocation;

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

                result.ReferenceImage = ImageUtils.CopyBitmap(reference, reference.PixelFormat);
                result.SourceImage = ImageUtils.CopyBitmap(preview, preview.PixelFormat);
                result.DifferenceImage = diffImage;
            }

            return result;
        }

    }
}
