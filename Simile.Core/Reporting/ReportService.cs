using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using System.Drawing;
using System.Drawing.Imaging;

using Mochify.Simile.Core.Utils;

namespace Mochify.Simile.Core.Reporting
{
    public class ReportService : IReportService
    {
        /// <summary>
        /// A lightweight class to hold basically the same stuff as Simile.Core.TestResult,
        /// but without the image references. 
        /// </summary>
        class ReportResult
        {
            private IList<string> _comments = new List<string>();
            public int TestId { get; set; }
            public bool TestPassed { get; set; }
            public string ReferencePath { get; set; }
            public string SourcePath { get; set; }
            public string DifferencePath { get; set; }

            // This is set to the actual URI the image was retrieved from
            // (not the local file on disk that was downloaded from it)
            public string OriginalSourceUri { get; set; }

            public IList<string> Comments
            {
                get { return _comments; }
                set { _comments = value; }
            }
        }

        IList<ReportResult> _results;
        private int _failedTests;
        private int _passedTests;

        IAssetManager _assetManager;
        private readonly ReportServiceConfiguration _configuration;

        public ReportServiceConfiguration Configuration
        {
            get { return _configuration; }
        }

        public ReportService(IAssetManager assetManager, ReportServiceConfiguration config)
        {
            _assetManager = assetManager;
            _configuration = config;
            _results = new List<ReportResult>();

            // We have to create the location where the report and its assets will be stored
            CreateReportLayout();
        }


        public ReportService(IAssetManager assetManager)
            : this(assetManager, new ReportServiceConfiguration())
        {
        
        }

        /// <summary>
        /// Add a test result to use for the final report.
        /// Note that passing in a TestResult does not transfer ownership
        /// of the internal bitmaps to me.
        /// </summary>
        /// <param name="tr"></param>
        public void AddResult(TestResult tr)
        {
            if (tr.TestPassed) 
            {
                ++_passedTests;
            }
            else
            {
                ++_failedTests;
            }

            if ((tr.TestPassed && Configuration.DisplaySuccesses)
                || !tr.TestPassed)
            {
                dynamic locations = PersistAssets(tr);
                var reportResult = new ReportResult
                {
                    TestId = tr.TestId,
                    TestPassed = tr.TestPassed,
                    Comments = new List<string>(tr.Comments),
                    SourcePath = MakeUri(locations.Source),
                    ReferencePath = MakeUri(locations.Reference),
                    DifferencePath = MakeUri(locations.Difference),
                    OriginalSourceUri = MakeUri(tr.OriginalSourceLocation)
                };
                _results.Add(reportResult);
            }
        }

        private string MakeUri(string path)
        {
            Uri uri;
            bool created = Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out uri);

            if (created)
            {
                return uri.ToString();
            }
            return "";
        }

        private void CreateReportLayout()
        {
            _assetManager.CreateFolder(Path.Combine(Configuration.OutputDirectory,
                Configuration.ReportName, "images"));
        }

        private string GetImageExtension(Image image)
        {
            if (image == null || image.RawFormat == null)
            {
                return "jpg";
            }

            if (ImageFormat.Png.Equals(image.RawFormat))
            {
                return "png";
            }
            else if (ImageFormat.Jpeg.Equals(image.RawFormat))
            {
                return "jpg";
            }
            else if (ImageFormat.Gif.Equals(image.RawFormat))
            {
                return "gif";
            }

            // default to jpg 
            return "jpg";
        }

        private ImageFormat GetImageFormatForSave(Image image)
        {
            if (image.RawFormat == null) { return ImageFormat.Jpeg; }
            if (ImageFormat.Png.Equals(image.RawFormat) || ImageFormat.Jpeg.Equals(image.RawFormat) || ImageFormat.Gif.Equals(image.RawFormat))
            {
                return image.RawFormat;
            }
            // default to jpg 
            return ImageFormat.Jpeg;
        }

        /// <summary>
        /// This will save images to (somewhere). It returns an anonymous
        /// type which will tell you where the files end up.
        /// </summary>
        /// <param name="tr"></param>
        /// <returns></returns>
        private dynamic PersistAssets(TestResult tr)
        {

            var images = new List<Tuple<Image, string, ImageFormat>>();

            var uuid = Guid.NewGuid().ToString("N");
            var sourcePath = string.Format("images/{0}-{1}-generated.{2}", tr.TestId, uuid, GetImageExtension(tr.SourceImage));
            images.Add(Tuple.Create(tr.SourceImage, sourcePath, tr.SourceFormatHint));

            var diffPath = string.Format("images/{0}-{1}-diff.{2}", tr.TestId, uuid, GetImageExtension(tr.DifferenceImage));
            images.Add(Tuple.Create(tr.DifferenceImage, diffPath, tr.DifferenceFormatHint));

            var referencePath = string.Format("images/{0}-{1}-reference.{2}", tr.TestId, uuid, GetImageExtension(tr.ReferenceImage));

            if (Configuration.CopyReferenceImages)
            {
                images.Add(Tuple.Create(tr.ReferenceImage, referencePath, tr.ReferenceFormatHint));
            }
            else
            {
                referencePath = tr.OriginalReferenceLocation;
            }

            foreach (var image in images)
            {
                if (image.Item1 != null && !string.IsNullOrWhiteSpace(image.Item2))
                {
                    image.Item1.Save(Path.Combine(Configuration.OutputDirectory, Configuration.ReportName, image.Item2), image.Item3);
                }
            }

            // yes I am evil - returning an anonymous type via dynamic.
            // This is localized to just inside this class, and I did tradeoff typesafety/autocomplete
            // for ease of coding, since otherwise I'd have to make a new class
            // or return a nameless Tuple
            return new { Reference = referencePath, Source = sourcePath, Difference = diffPath };
        }

        private string GenerateFileName(string subdir, object identifier, object extension)
        {
            var uuid = Guid.NewGuid().ToString("N");
            return string.Format("{0}/{1}-{2}.{3}", subdir, identifier, uuid, extension);
        }

        public bool GenerateReport()
        {
            var defaultTemplate = "template.rend";
            using (var s = _assetManager.Get(Path.Combine(Configuration.TemplateDirectory, defaultTemplate)))
            using (var sr = new StreamReader(s))
            {
                var failedIter = _results.Where(x => !x.TestPassed).Select(x => x.TestId);
                var failedString = string.Join(",", failedIter);

                var reportDict = new Dictionary<string, object>();
                reportDict.Add("reportname", Configuration.ReportName);
                reportDict.Add("result", _results);
                reportDict.Add("passed", _passedTests);
                reportDict.Add("failed", _failedTests);
                reportDict.Add("failures", failedString);
                var template = sr.ReadToEnd();
                var output = Configuration.ReportView.GenerateTemplate(template, reportDict);
                var outFile = Path.Combine(Configuration.OutputDirectory, Configuration.ReportName, "report.html");
                _assetManager.Save(output, outFile);
            }

            return true;
        }
    }
}
