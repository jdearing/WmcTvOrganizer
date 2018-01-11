using System;
using System.IO;

using log4net;

namespace WmcTvOrganizer.Process
{
    public class FolderCleaner
    {
        private readonly ILog _logger;
        
        public FolderCleaner(ILog logger)
        {
            _logger = logger;
        }
        
        public void Process(string rootFolder)
        {
            DirectoryInfo root = null;

            _logger.InfoFormat("Folder cleaner started for {0}", rootFolder);

            try
            {
                root = new DirectoryInfo(rootFolder);
            }
            catch (Exception ex)
            {
                _logger.Error($"Unable to access directory: {rootFolder}", ex);
            }

            if (root != null && root.Exists)
            {
                CrawlDirectory(root, 0);
            }
        }

        private void CrawlDirectory(DirectoryInfo root, int depth)
        {
            if (root.Exists)
            {
                FileSystemInfo[] infos = root.GetFileSystemInfos();
                
                if (infos.Length == 0 && depth != 0)
                {
                    _logger.Info($"Deleting folder {root.FullName}");
                    //directoryInfo.Delete();
                }
                else
                {
                    foreach (var info in infos)
                    {
                        if (info.Attributes == FileAttributes.Directory)
                        {
                            DirectoryInfo dir = new DirectoryInfo(info.FullName);
                            CrawlDirectory(dir, depth + 1);
                            FileSystemInfo[] infos2 = dir.GetFileSystemInfos();
                            if (infos2.Length == 0)
                            {
                                _logger.Info($"Deleting folder {dir.FullName}");
                                //dir.Delete();
                            }
                        }
                    }
                }
                

                //if (subFolders != null && subFolders.Length > 0)
                //{
                //    foreach (DirectoryInfo directoryInfo in subFolders)
                //    {
                //        CrawlDirectory(directoryInfo);

                //        try
                //        {
                //            FileSystemInfo[] infos = directoryInfo.GetFileSystemInfos();
                //            if (infos.Length == 0)
                //            {
                //                _logger.Info($"Deleting folder {directoryInfo.FullName}");
                //                //directoryInfo.Delete();
                //            }
                //        }
                //        catch (Exception ex)
                //        {
                //            _logger.Error($"Unable to delete directory: {directoryInfo.FullName}", ex);
                //        }
                //    }
                //}


            }
        }
    }
}