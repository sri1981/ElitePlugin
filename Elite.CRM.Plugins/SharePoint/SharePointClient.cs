using Elite.CRM.Plugins.ErrorHandling;
using Microsoft.SharePoint.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Elite.CRM.Plugins.SharePoint
{
    class SharePointClient : IDisposable
    {
        private bool _disposed;
        private ClientContext _spContext;

        public SharePointClient(string url, ICredentials credentials)
        {
            _spContext = new ClientContext(url);
            _spContext.Credentials = credentials;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="library">Name of the document library, into which file should be uploaded.</param>
        /// <param name="path">Relative path within library.</param>
        /// <param name="filename"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public string UploadFile(string library, string path, string filename, byte[] content)
        {
            var web = _spContext.Web;

            // get document library
            var docLibrary = web.Lists.GetByTitle(library);
            _spContext.Load(docLibrary, lib => lib.EnableFolderCreation);
            _spContext.ExecuteQuery();

            // create entire folder path
            var docFolder = CreateFolderPath(docLibrary.RootFolder, path);

            // create empty file
            var file = docFolder.Files.Add(new FileCreationInformation() { Content = new byte[] { }, Url = filename, Overwrite = true });
            _spContext.Load(file);
            _spContext.ExecuteQuery();

            // upload file content
            using (MemoryStream stream = new MemoryStream(content))
            {
                Microsoft.SharePoint.Client.File.SaveBinaryDirect(_spContext, file.ServerRelativeUrl, stream, true);
            }

            // gets a base server URL + server relative URL
            return new Uri(_spContext.Url).GetLeftPart(UriPartial.Authority) + file.ServerRelativeUrl;
        }

        private Folder CreateFolderPath(Folder root, string path)
        {
            ThrowIf.Argument.IsNull(root, "root");
            ThrowIf.Argument.IsNullOrEmpty(path, "path");

            var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

            Folder current = null;
            Folder parent = root;

            foreach (var folderName in parts)
            {
                var folderNameClean = Regex.Replace(folderName, @"[^\w\d\s]", "");
                current = parent.Folders.Add(folderNameClean);
                _spContext.Load(current);
                _spContext.ExecuteQuery();

                parent = current;
            }

            return current;
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _spContext.Dispose();
            }

            _disposed = true;
        }
    }
}
