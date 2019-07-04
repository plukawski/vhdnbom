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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using System.Threading;
using System.ComponentModel;
using DiscUtils;
using DiscUtils.Ntfs;
using RawDiskLib;
using DiscUtils.Vhdx;
using DiscUtils.Registry;
using System.IO.Abstractions;
using VHDNBOM.Logic.Helpers.Decorators;

namespace VHDNBOM.Logic.Helpers
{
    public class FileSystemHelper : IFileSystemHelper
    {
        private readonly System.IO.Abstractions.IFileSystem fileSystem;

        private enum SymbolicLink
        {
            File = 0,
            Directory = 1
        }

        [DllImport("kernel32.dll")]
        private static extern bool CreateSymbolicLink(string lpSymlinkFileName, string lpTargetFileName, SymbolicLink dwFlags);

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        int dwIoControlCode,
        IntPtr InBuffer,
        int nInBufferSize,
        IntPtr OutBuffer,
        int nOutBufferSize,
        ref int pBytesReturned,
        [In] ref NativeOverlapped lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int GetFinalPathNameByHandle(IntPtr handle, [In, Out] StringBuilder path, int bufLen, int flags);

        public FileSystemHelper(System.IO.Abstractions.IFileSystem fileSystem)
        {
            this.fileSystem = fileSystem;
        }

        public char GetFirstAvailableDriveLetter()
        {
            IEnumerable<string> availableDriveLetters = Enumerable.Range('A', 'Z' - 'A' + 1).Select(i => (Char)i + ":")
                .Except(fileSystem.DriveInfo.GetDrives().Select(s => s.Name.Replace("\\", "")));
            return availableDriveLetters.First().First();
        }

        public IDriveInfo GetSystemDriveInfo()
        {
            string systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.System));
            IDriveInfo systemDriveInfo = fileSystem.DriveInfo.GetDrives().Where(x => x.Name.First() == systemDrive.First()).Single();
            return systemDriveInfo;
        }

        public string FindBestLocationForVhdTemporaryFolder()
        {
            IDriveInfo systemDriveInfo = GetSystemDriveInfo();

            long systemDriveUsedSpace = systemDriveInfo.TotalSize - systemDriveInfo.TotalFreeSpace;

            var applicableDrives = fileSystem.DriveInfo.GetDrives()
                .Where(x => x.IsReady
                    && x.AvailableFreeSpace > (systemDriveUsedSpace + (200 << 20))
                    && x.Name != systemDriveInfo.Name)
                .OrderByDescending(x => x.AvailableFreeSpace);

            if (!applicableDrives.Any())
            {
                return null;
            }

            string tempVhdLocation = $"{applicableDrives.First().Name}VHDNBOM_Temp";
            return tempVhdLocation;
        }

        public void ExecuteDiskpart(string diskpartScriptFile, ILogger logger)
        {
            string commandToExecute = $" /s {diskpartScriptFile}";
            ProcessStartInfo diskpartsi = new ProcessStartInfo("diskpart.exe");
            diskpartsi.Arguments = commandToExecute;
            diskpartsi.UseShellExecute = false;
            diskpartsi.RedirectStandardOutput = true;
            diskpartsi.RedirectStandardError = true;
            diskpartsi.CreateNoWindow = true;

            Process diskpart = Process.Start(diskpartsi);
            string output = diskpart.StandardOutput.ReadToEnd();
            diskpart.WaitForExit();

            if (diskpart.HasExited && diskpart.ExitCode != 0)
            {
                logger.LogError(output);
                throw new Exception("Execution of diskpart failed.");
            }
            else
            {
                logger.LogDebug(output);
            }
        }

        public string ExecuteBcdeditCommand(string bcdeditArguments, ILogger logger)
        {
            string commandToExecute = bcdeditArguments;
            ProcessStartInfo bcdeditSi = new ProcessStartInfo("bcdedit.exe");
            bcdeditSi.Arguments = commandToExecute;
            bcdeditSi.UseShellExecute = false;
            bcdeditSi.RedirectStandardOutput = true;
            bcdeditSi.RedirectStandardError = true;
            bcdeditSi.CreateNoWindow = true;

            Process bcdedit = Process.Start(bcdeditSi);
            string output = bcdedit.StandardOutput.ReadToEnd();
            bcdedit.WaitForExit();

            if (bcdedit.HasExited && bcdedit.ExitCode != 0)
            {
                logger.LogError(output);
                throw new Exception("Execution of bcdedit failed.");
            }
            return output;
        }

        public bool CreateSymbolicLinkToDirectory(string symbolicLinkLocation, string sourceForTheLink)
        {
            return CreateSymbolicLink(symbolicLinkLocation, sourceForTheLink, SymbolicLink.Directory);
        }

        public void CreateSparseFile(string fullName, long fileSize)
        {
            using (FileStream fs = (FileStream)fileSystem.File.Create(fullName))
            {
                MarkAsSparseFile(fs.SafeFileHandle);
                fs.SetLength(fileSize);
            }
        }

        public void MarkAsSparseFile(SafeFileHandle fileHandle)
        {
            int bytesReturned = 0;
            NativeOverlapped lpOverlapped = new NativeOverlapped();
            bool result =
                DeviceIoControl(
                    fileHandle,
                    590020, //FSCTL_SET_SPARSE,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    0,
                    ref bytesReturned,
                    ref lpOverlapped);
            if (result == false)
                throw new Win32Exception();
        }

        public string GetJunctionTargetPath(string junctionFullName)
        {
            string result = null;

            using (FileStream fs = Alphaleonis.Win32.Filesystem.File.OpenBackupRead(junctionFullName))
            {
                StringBuilder path = new StringBuilder(1024);
                GetFinalPathNameByHandle(fs.SafeFileHandle.DangerousGetHandle(), path, path.Capacity, 0);
                result = path.ToString();
                result = result.Replace(@"\\?\", "");
            }

            return result;
        }

        /// <summary>
        /// Clones file system sector by sector omiting the unused ones. 
        /// Note: caller is responsible for disposing the streams used in parameters
        /// </summary>
        /// <param name="sourceFileSystemToClone"></param>
        /// <param name="destinationLocationStream"></param>
        public void CloneNtfsFileSystem(Stream sourceFileSystemToClone, Stream destinationLocationStream, ILogger logger)
        {
            if (sourceFileSystemToClone == null || destinationLocationStream == null)
            {
                throw new ArgumentNullException("Neither sourceFileSystemToClone nor destinationLocationStream can be null.");
            }
            if (destinationLocationStream.Length < sourceFileSystemToClone.Length)
            {
                throw new ArgumentException("Cloning NTFS file system to a smaller destination is not supported.");
            }

            byte[] volBitmap;
            int clusterSize;
            using (NtfsFileSystem ntfs = new NtfsFileSystem(sourceFileSystemToClone))
            {
                ntfs.NtfsOptions.HideSystemFiles = false;
                ntfs.NtfsOptions.HideHiddenFiles = false;
                ntfs.NtfsOptions.HideMetafiles = false;

                using (Stream bitmapStream = ntfs.OpenFile(@"$Bitmap", FileMode.Open))
                {
                    volBitmap = new byte[bitmapStream.Length];

                    int totalRead = 0;
                    int numRead = bitmapStream.Read(volBitmap, 0, volBitmap.Length - totalRead);
                    while (numRead > 0)
                    {
                        totalRead += numRead;
                        numRead = bitmapStream.Read(volBitmap, totalRead, volBitmap.Length - totalRead);
                    }
                }

                clusterSize = (int)ntfs.ClusterSize;
            }

            List<StreamExtent> extents = new List<StreamExtent>(ConvertBitmapToRanges(volBitmap, clusterSize));
            List<StreamExtent> outOfRangeExtents = extents.Where(x => x.Start + x.Length > sourceFileSystemToClone.Length).ToList();   //removes extenst that are beyond the source stream - bitmap size is always a multiple of 8, so it can contain bits for clusters that do not exist
            outOfRangeExtents.ForEach((extent) =>
            {
                extents.Remove(extent);
            });

            SparseStream contentStream = SparseStream.FromStream(sourceFileSystemToClone, Ownership.None, extents);
            {
                StreamPump pump = new StreamPump()
                {
                    InputStream = contentStream,
                    OutputStream = destinationLocationStream,
                };

                long totalBytes = 0;
                foreach (var se in contentStream.Extents)
                {
                    totalBytes += se.Length;
                }

                long onePercentageBytesAmount = totalBytes / 100;
                int currentPercentage = 0;

                if (logger != null)
                {
                    pump.ProgressEvent += (o, e) =>
                    {
                        if (e.BytesWritten >= (currentPercentage + 1) * onePercentageBytesAmount)
                        {
                            currentPercentage++;
                            logger.LogInformation($"Copy ntfs file system: {currentPercentage}%");
                        }
                    };
                }

                pump.Run();
            }
        }

        private static IEnumerable<StreamExtent> ConvertBitmapToRanges(byte[] bitmap, int bytesPerCluster)
        {
            long numClusters = bitmap.Length * 8;
            long cluster = 0;
            while (cluster < numClusters && !IsBitmapBitSet(bitmap, cluster))
            {
                ++cluster;
            }

            while (cluster < numClusters)
            {
                long startCluster = cluster;
                while (cluster < numClusters && IsBitmapBitSet(bitmap, cluster))
                {
                    ++cluster;
                }

                yield return new StreamExtent((long)(startCluster * (long)bytesPerCluster), (long)((cluster - startCluster) * (long)bytesPerCluster));

                while (cluster < numClusters && !IsBitmapBitSet(bitmap, cluster))
                {
                    ++cluster;
                }
            }
        }

        private static bool IsBitmapBitSet(byte[] buffer, long bit)
        {
            int byteIdx = (int)(bit >> 3);
            if (byteIdx >= buffer.Length)
            {
                return false;
            }

            byte val = buffer[byteIdx];
            byte mask = (byte)(1 << (int)(bit & 0x7));

            return (val & mask) != 0;
        }

        public Stream OpenRawDiskStream(string diskPath)
        {
            using (RawDisk rawDisk = new RawDisk(diskPath))
            {
                return rawDisk.CreateDiskStream();
            }
        }

        public VirtualDiskDecorator OpenVhdx(string vhdxFilePath)
        {
            return new VirtualDiskDecorator(new Disk(vhdxFilePath));
        }

        public void ChangeSystemDriveMappingFromRegistry(Stream registryFileStream)
        {
            using (RegistryHive hive = new RegistryHive(registryFileStream))
            {
                IDriveInfo systemDriveInfo = this.GetSystemDriveInfo();
                RegistryKey mountedDevicesKey = hive.Root.OpenSubKey("MountedDevices");
                var keyValue = mountedDevicesKey.GetValue($@"\DosDevices\{systemDriveInfo.Name.First()}:");
                if (keyValue != null)
                {
                    char freeLetter = this.GetFirstAvailableDriveLetter();
                    mountedDevicesKey.DeleteValue($@"\DosDevices\{systemDriveInfo.Name.First()}:");
                    if (freeLetter != 0)
                    {
                        mountedDevicesKey.SetValue($@"\DosDevices\{freeLetter}:", keyValue, RegistryValueType.Binary);
                    }
                }
            }
        }

        public DiscFileSystem OpenNtfsFileSystem(Stream rawPartitionStream)
        {
            return new NtfsFileSystem(rawPartitionStream);
        }
    }
}
