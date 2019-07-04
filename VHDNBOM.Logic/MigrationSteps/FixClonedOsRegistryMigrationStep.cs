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
using System.Collections.Generic;
using VHDNBOM.Logic.Helpers;
using VHDNBOM.Logic.Models;
using DiscUtils;
using DiscUtils.Ntfs;
using System.IO;
using VHDNBOM.Logic.Helpers.Decorators;

namespace VHDNBOM.Logic.MigrationSteps
{
    /// <summary>
    /// This step is necessary to prevent wrongly mounting the physical system drive when runnign the operating system from VHD file. Without that the launched system from VHD will enter the infinite loop
    /// </summary>
    public class FixClonedOsRegistryMigrationStep : BaseMigrationStep
    {
        private readonly ILogger<FixClonedOsRegistryMigrationStep> logger;
        private readonly IFileSystemHelper fileSystemHelper;
        private readonly System.IO.Abstractions.IFileSystem fileSystem;

        public FixClonedOsRegistryMigrationStep(ILogger<FixClonedOsRegistryMigrationStep> logger,
            IFileSystemHelper fileSystemHelper,
            System.IO.Abstractions.IFileSystem fileSystem,
            MigrationFlowData migrationData) : base(migrationData)
        {
            this.logger = logger;
            this.fileSystemHelper = fileSystemHelper;
            this.fileSystem = fileSystem;
        }

        public override bool CheckPrerequisities(ref string[] messages)
        {
            List<string> messageList = new List<string>();

            messages = messageList.ToArray();
            return messageList.Count == 0;
        }

        public override void PerformStep()
        {
            logger.LogInformation("Fixing cloned OS registry.");

            string vhdFullName = $"{migrationData.VhdFileTemporaryFolder}\\{migrationData.VhdFileName}";
            using (VirtualDiskDecorator disk = this.fileSystemHelper.OpenVhdx(vhdFullName))
            {
                PartitionInfoDecorator clonedPartition = disk.Partitions[0];
                using (Stream partitionStream = clonedPartition.Open())
                {
                    using (DiscFileSystem ntfs = this.fileSystemHelper.OpenNtfsFileSystem(partitionStream))
                    {
                        if (ntfs is NtfsFileSystem)
                        {
                            (ntfs as NtfsFileSystem).NtfsOptions.HideSystemFiles = false;
                            (ntfs as NtfsFileSystem).NtfsOptions.HideHiddenFiles = false;
                        }

                        // removes not necessary files from the image
                        // Remove VSS snapshot files (can be very large)
                        foreach (string filePath in ntfs.GetFiles(@"\System Volume Information", "*{3808876B-C176-4e48-B7AE-04046E6CC752}"))
                        {
                            ntfs.DeleteFile(filePath);
                        }

                        // Remove the page file
                        if (ntfs.FileExists(@"\Pagefile.sys"))
                        {
                            ntfs.DeleteFile(@"\Pagefile.sys");
                        }

                        // Remove the hibernation file
                        if (ntfs.FileExists(@"\hiberfil.sys"))
                        {
                            ntfs.DeleteFile(@"\hiberfil.sys");
                        }

                        using (Stream systemRegistryStream = ntfs.OpenFile(@"windows\system32\config\system", FileMode.Open))
                        {
                            this.fileSystemHelper.ChangeSystemDriveMappingFromRegistry(systemRegistryStream);
                        }
                    }
                }

                if (migrationData.TemporaryVhdFileIsTheFinalOne)
                {
                    char vhdLocationDriveLetter = migrationData.VhdFileTemporaryFolder[0];
                    var driveInfo = this.fileSystem.DriveInfo.FromDriveName(vhdLocationDriveLetter.ToString() + ":\\");
                    if (driveInfo.AvailableFreeSpace <= disk.Geometry.Capacity)
                    {
                        logger.LogWarning($"The image is located in a drive which has not enough free space for it to expand. If you will not free some space on drive '{driveInfo.Name}' then you will see BSOD when trying to boot from the created image.");
                    }
                }
            }
            return;
        }
    }
}
