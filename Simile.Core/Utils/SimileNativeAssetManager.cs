﻿using System;
using System.IO;

using System.Net;
using Microsoft.Win32;

using Common.Logging;

namespace Mochify.Simile.Core.Utils
{
    /// <summary>
    /// This class interacts with the file system/native system to get files.
    /// </summary>
    public class SimileNativeAssetManager : IAssetManager
    {
        private static readonly ILog _log = LogManager.GetCurrentClassLogger();
        public Stream Get(string path)
        {
            _log.DebugFormat("Getting {0}", path);
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Null or blank string passed to Get.");
            }

            try
            {
                var uri = new Uri(path);
                var wc = new WebClient();
                return wc.OpenRead(uri);
            }
            catch (WebException e)
            {
                // most likely couldn't retrieve the URI due to timeouts or 404 or something.
                throw new IOException("Problem encountered retrieving path.", e);
            }
        }

        public bool Delete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                return false;
            }

            return true;
        }

        public void Fetch(string fetchPath, string savePath)
        {
            var uri = new Uri(fetchPath);
            var wc = new WebClient();
            try
            {
                wc.DownloadFile(fetchPath, savePath);
            }
            catch (WebException we)
            {
                _log.Info(string.Format("Exception encountered when retrieving URI {0}.", fetchPath), we);
                if (we.Status == WebExceptionStatus.ProtocolError && we.Response != null)
                {
                    var rsp = we.Response as HttpWebResponse;
                    _log.InfoFormat("Status code was {0}", rsp.StatusCode);
                }
            }
        }

        public string FetchToRandomFilename(string fetchUri, string outputDirectory)
        {
            CreateFolder(outputDirectory);
            Uri uri = new Uri(fetchUri);

            string randomName = Guid.NewGuid().ToString("N");

            if (uri.IsFile || uri.IsUnc)
            {
                randomName = string.Format("{0}{1}", randomName, Path.GetExtension(uri.AbsoluteUri));
                File.Copy(uri.LocalPath, Path.Combine(outputDirectory, randomName));
            }
            else
            {
                // I'm assuming that you're not going to be giving me non file/non-HTTP stuff here,
                // since I'm going to use a download now.
                HttpWebRequest request = WebRequest.Create(uri) as HttpWebRequest;
                try
                {
                    using (var response = request.GetResponse())
                    {
                        var contentType = response.ContentType;
                        randomName = string.Format("{0}{1}", randomName, GetRegisteredExtension(contentType));
                        Save(response.GetResponseStream(), Path.Combine(outputDirectory, randomName));
                    }
                }
                catch (WebException we)
                {
                    _log.Info(string.Format("Exception encountered when retrieving URI {0}.", fetchUri), we);
                }
            }

            return randomName;
            
        }

        public void Save(Stream file, string savePath)
        {
            using(var ostream = new FileStream(savePath, FileMode.Create, FileAccess.Write))
            {
                file.CopyTo(ostream);
            }
        }

        public void Save(string contents, string savePath)
        {
            File.WriteAllText(savePath, contents);
        }

        public void CreateFolder(string path)
        {
            Directory.CreateDirectory(path);
        }

        private string GetRegisteredExtension(string type)
        {
            RegistryKey key = Registry.ClassesRoot.OpenSubKey(@"MIME\Database\Content Type\" + type, false);
            var value = key != null ? key.GetValue("Extension", null) : null;
            return value != null ? value.ToString() : "";
        }
    }
}
