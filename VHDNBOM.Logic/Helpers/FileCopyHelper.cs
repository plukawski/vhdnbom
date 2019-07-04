//Copyright(c) 2019 Przemysław Łukawski

//Permission is hereby granted, free of charge, to any person obtaining a copy
//of this software and associated documentation files (the "Software"), to deal
//in the Software without restriction, including without limitation the rights
//to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//copies of the Software, and to permit persons to whom the Software is
//furnished to do so, subject to the following conditions:

//The above copyright notice and this permission notice shall be included in all
//copies or substantial portions of the Software.

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//SOFTWARE.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Alphaleonis.Win32.Filesystem;
using System.Security.AccessControl;

namespace VHDNBOM.Logic.Helpers
{
    public class FileCopyHelper : IFileCopyHelper
    {
        private readonly ILogger<FileCopyHelper> logger;
        private readonly IFileSystemHelper fileSystemHelper;
        private long totalFilesSizeToCopy = 0;
        private long filesSizeCopied = 0;
        private long errorsDuringCopy = 0;
        private long currentPercentage = 0;
        private const int FILE_COPY_CHUNK_SIZE = 16384;

        private class JunctionInfo
        {
            public DirectoryInfo originalJunctionInfo { get; set; }
            public string destinationJunctionLocation { get; set; }
        }

        List<JunctionInfo> junctions = new List<JunctionInfo>();

        public FileCopyHelper(ILogger<FileCopyHelper> logger, IFileSystemHelper fileSystemHelper)
        {
            this.logger = logger;
            this.fileSystemHelper = fileSystemHelper;
        }

        /// <summary>
        /// Copy directory recursive with permissions, overwrite existing
        /// </summary>
        /// <param name="sourceFolder"></param>
        /// <param name="destinationFolder"></param>
        /// <param name="displayProgress">action to call to notify about the progress</param>
        public long CopyDirectory(string sourceFolder, string destinationFolder, long sourceFolderSize = 0, Action<int> displayProgress = null)
        {
            this.totalFilesSizeToCopy = this.filesSizeCopied = this.errorsDuringCopy = this.currentPercentage = 0;
            this.junctions = new List<JunctionInfo>();
            var sourceDirectory = new DirectoryInfo(sourceFolder);
            if (!sourceDirectory.Exists) throw new System.IO.DirectoryNotFoundException("Source folder not found: " + sourceFolder);

            var destinationDirectory = !Directory.Exists(destinationFolder) ? Directory.CreateDirectory(destinationFolder) : new DirectoryInfo(destinationFolder);

            if (displayProgress != null)
            {
                this.totalFilesSizeToCopy = sourceFolderSize;
            }

            CopyDirectory(sourceDirectory, destinationDirectory, displayProgress);
            CopyJunctions(sourceDirectory, destinationDirectory);

            return this.errorsDuringCopy;
        }

        private void CopyDirectory(DirectoryInfo sourceDirectory, DirectoryInfo destinationDirectory, Action<int> displayProgress = null)
        {
            try
            {
                if (sourceDirectory == null) throw new ArgumentException("sourceDirectory");
                if (destinationDirectory == null) throw new ArgumentException("destinationDirectory");

                DirectorySecurity security = sourceDirectory.GetAccessControl();
                security.SetAccessRuleProtection(true, true);

                var dirsToCopy = sourceDirectory.GetDirectories();
                foreach (var dirToCopy in dirsToCopy)
                {
                    var destSubDirPath = Path.Combine(destinationDirectory.FullName, dirToCopy.Name);
                    if (dirToCopy.EntryInfo.IsSymbolicLink)  //we have a symbolic link - we must copy only link, not its target
                    {
                        CopyDirectorySymbolicLink(dirToCopy, destSubDirPath);
                    }
                    else if (dirToCopy.EntryInfo.IsMountPoint)
                    {
                        this.junctions.Add(new JunctionInfo()
                        {
                            originalJunctionInfo = dirToCopy,
                            destinationJunctionLocation = destSubDirPath
                        });
                    }
                    else
                    {
                        var destinationSubDir = !Directory.Exists(destSubDirPath) ? Directory.CreateDirectory(destSubDirPath) : new DirectoryInfo(destSubDirPath);
                        destinationSubDir.Attributes = dirToCopy.Attributes;
                        CopyDirectory(dirToCopy, destinationSubDir, displayProgress);
                    }
                }

                var filesToCopy = sourceDirectory.GetFiles();

                foreach (var file in filesToCopy)
                {
                    if (file.EntryInfo.IsSymbolicLink)
                    {
                        CopyFileSymbolicLink(file, destinationDirectory.FullName);
                    }
                    else
                    {
                        CopyFile(file, destinationDirectory.FullName);

                        this.filesSizeCopied += file.Length;

                        if (displayProgress != null && this.totalFilesSizeToCopy > 0)
                        {
                            long onePercentRange = this.totalFilesSizeToCopy / 100;
                            if (onePercentRange == 0 || this.filesSizeCopied >= (currentPercentage + 1) * onePercentRange)
                            {
                                this.currentPercentage = this.filesSizeCopied / onePercentRange;
                                this.currentPercentage = this.currentPercentage > 100 ? 100 : this.currentPercentage;
                                if (this.currentPercentage < 100)
                                {
                                    displayProgress((int)this.currentPercentage);
                                }
                            }
                        }
                    }
                }

                destinationDirectory.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                this.errorsDuringCopy++;
                logger.LogError(ex, $"Error occured during copying directory '{sourceDirectory.FullName}'");
            }
        }

        private void CopyFile(FileInfo file, string destinationDirectory)
        {
            try
            {
                var path = Path.Combine(destinationDirectory, file.Name);
                FileSecurity fileSecurity = null;

                fileSecurity = file.GetAccessControl();
                fileSecurity.SetAccessRuleProtection(true, true);

                if (file.EntryInfo.IsSparseFile)
                {
                    this.fileSystemHelper.CreateSparseFile(path, file.Length);
                }
                else
                {
                    try
                    {
                        if (file.FullName.Length < 260 && path.Length < 260)    //FileInfo from System.IO is much faster when copying files. As a result we should use it whenever possible.
                        {
                            System.IO.FileInfo stdFile = new System.IO.FileInfo(file.FullName);
                            stdFile.CopyTo(path, true);
                        }
                        else
                        {
                            file.CopyTo(path, true);
                        }
                    }
                    catch (UnauthorizedAccessException) //if failed due to access denied try to copy file using backup semantics
                    {
                        using (var fs = Alphaleonis.Win32.Filesystem.File.OpenBackupRead(file.FullName))
                        {
                            using (var dfs = Alphaleonis.Win32.Filesystem.File.Open(path, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None, Alphaleonis.Win32.Filesystem.ExtendedFileAttributes.BackupSemantics, Alphaleonis.Win32.Filesystem.PathFormat.FullPath))
                            {
                                fs.CopyTo(dfs);
                            }
                        }
                    }
                }

                var copiedFile = new FileInfo(path);

                copiedFile.SetAccessControl(fileSecurity);
                copiedFile.Attributes = file.Attributes;
            }
            catch (Exception ex)
            {
                this.errorsDuringCopy++;
                logger.LogError(ex, $"Error occured during copying file '{file.FullName}'");
            }
        }

        private void CopyDirectorySymbolicLink(DirectoryInfo symbolicLinkToCopy, string destinationSymLinkName)
        {
            try
            {
                DirectorySecurity symLinkSecurity = symbolicLinkToCopy.GetAccessControl();
                symLinkSecurity.SetAccessRuleProtection(true, true);

                if (Directory.Exists(destinationSymLinkName))
                {
                    Directory.Delete(destinationSymLinkName);
                }

                Alphaleonis.Win32.Filesystem.Directory.Copy(symbolicLinkToCopy.FullName, destinationSymLinkName, Alphaleonis.Win32.Filesystem.CopyOptions.CopySymbolicLink);

                var copiedSymLink = new DirectoryInfo(destinationSymLinkName);
                copiedSymLink.SetAccessControl(symLinkSecurity);
            }
            catch (Exception ex)
            {
                if ((uint)ex.HResult == 0x80071128) //The data present in the reparse point buffer is invalid
                {
                    logger.LogWarning(ex, $"Error occured during directory symbolic link copy '{symbolicLinkToCopy.FullName}'. Trying regular directory copy routine.");
                    CopyDirectory(symbolicLinkToCopy, Directory.CreateDirectory(destinationSymLinkName));
                }
                else
                {
                    this.errorsDuringCopy++;
                    logger.LogError(ex, $"Error occured during copying directory symbolic link '{symbolicLinkToCopy.FullName}'");
                }
            }
        }

        private void CopyJunctions(DirectoryInfo sourceDirectory, DirectoryInfo targetDirectory)
        {
            foreach (var junctionInfo in this.junctions)
            {
                try
                {
                    CopyDirectorySymbolicLink(junctionInfo.originalJunctionInfo, junctionInfo.destinationJunctionLocation);
                    string originalJunctionTarget = this.fileSystemHelper.GetJunctionTargetPath(junctionInfo.destinationJunctionLocation);
                    if (originalJunctionTarget != null)
                    {
                        if (originalJunctionTarget.ToLower().Contains(sourceDirectory.FullName.ToLower()))
                        {

                            var junctionSecurity = junctionInfo.originalJunctionInfo.GetAccessControl();
                            junctionSecurity.SetAccessRuleProtection(true, true);

                            string newJunctionTarget = originalJunctionTarget.ToLower().Replace(sourceDirectory.FullName.ToLower(), targetDirectory.FullName.ToLower());
                            Directory.CreateJunction(junctionInfo.destinationJunctionLocation, newJunctionTarget, true);

                            var newJunction = new DirectoryInfo(junctionInfo.destinationJunctionLocation);
                            newJunction.SetAccessControl(junctionSecurity);

                        }
                    }
                }
                catch (Exception ex)
                {
                    this.errorsDuringCopy++;
                    logger.LogError(ex, $"Error occured during copying junction '{junctionInfo.originalJunctionInfo.FullName}'");
                }
            }
        }

        private void CopyFileSymbolicLink(FileInfo symbolicLinkToCopy, string destinationSymLinkName)
        {
            try
            {
                var path = Path.Combine(destinationSymLinkName, symbolicLinkToCopy.Name);
                FileSecurity symLinkSecurity = symbolicLinkToCopy.GetAccessControl();
                symLinkSecurity.SetAccessRuleProtection(true, true);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                Alphaleonis.Win32.Filesystem.File.Copy(symbolicLinkToCopy.FullName, path, Alphaleonis.Win32.Filesystem.CopyOptions.CopySymbolicLink);

                var copiedSymLink = new FileInfo(path);
                copiedSymLink.SetAccessControl(symLinkSecurity);
                copiedSymLink.Attributes = symbolicLinkToCopy.Attributes;
            }
            catch (Exception ex)
            {
                //0x80070780 hresult The file cannot be accessed by the system
                this.errorsDuringCopy++;
                logger.LogError(ex, $"Error occured during copying file symbolic link '{symbolicLinkToCopy.FullName}'");
            }
        }
    }
}
