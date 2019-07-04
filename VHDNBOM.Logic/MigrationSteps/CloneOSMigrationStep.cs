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
using VHDNBOM.Logic.Helpers;
using VHDNBOM.Logic.Models;
using Alphaleonis.Win32.Vss;
using DiscUtils.Partitions;
using Alphaleonis.Win32.Security;
using System.IO;
using VHDNBOM.Logic.Helpers.Decorators;

namespace VHDNBOM.Logic.MigrationSteps
{
    public class CloneOSMigrationStep : BaseMigrationStep
    {
        private readonly ILogger<CloneOSMigrationStep> logger;
        private readonly IOperatingSystemHelper osHelper;
        private readonly IFileSystemHelper fileSystemHelper;
        private readonly IVssImplementation vss;
        private Guid vssSnapshotSetId, vssSnapshotId;

        public CloneOSMigrationStep(ILogger<CloneOSMigrationStep> logger,
            IOperatingSystemHelper osHelper,
            IFileSystemHelper fileSystemHelper,
            MigrationFlowData migrationData,
            IVssImplementation vss) : base(migrationData)
        {
            this.logger = logger;
            this.osHelper = osHelper;
            this.fileSystemHelper = fileSystemHelper;
            this.vss = vss;
        }

        public override bool CheckPrerequisities(ref string[] messages)
        {
            List<string> messageList = new List<string>();

            logger.LogDebug("Checking vss requirements.");

            using (IVssBackupComponents backupComponents = vss.CreateVssBackupComponents())
            {
                backupComponents.InitializeForBackup(null);
                if (!backupComponents.IsVolumeSupported($"{migrationData.OperatingSystemDriveLetter}:\\"))
                {
                    messageList.Add($"System volume does not support VSS.");
                }
            }

            messages = messageList.ToArray();
            return messageList.Count == 0;
        }

        public override void PerformStep()
        {
            logger.LogInformation("Cloning operating system.");
            using (IVssBackupComponents backupComponents = vss.CreateVssBackupComponents())
            {
                this.CreateVssSnapshotOfSystemVolume(backupComponents);

                try
                {
                    logger.LogInformation("Copying system partition data...");
                    using (PrivilegeEnabler privEnabler = new PrivilegeEnabler(Privilege.Backup, Privilege.Restore))
                    {
                        VssSnapshotProperties snapshotInfo = backupComponents.GetSnapshotProperties(this.vssSnapshotId);
                        using (Stream rawVolStream = this.fileSystemHelper.OpenRawDiskStream(snapshotInfo.SnapshotDeviceObject))
                        {
                            string vhdFullName = $"{migrationData.VhdFileTemporaryFolder}\\{migrationData.VhdFileName}";
                            using (VirtualDiskDecorator disk = this.fileSystemHelper.OpenVhdx(vhdFullName))
                            {
                                var partitionTable = disk.Partitions;
                                if (partitionTable != null)
                                {
                                    int partIndex = partitionTable.CreatePrimaryBySector(1, (rawVolStream.Length / disk.Geometry.BytesPerSector), BiosPartitionTypes.Ntfs, false);
                                    PartitionInfoDecorator partition = disk.Partitions[partIndex];

                                    using (var vhdPartitionStream = partition.Open())
                                    {
                                        this.fileSystemHelper.CloneNtfsFileSystem(rawVolStream, vhdPartitionStream, logger);
                                    }
                                }
                                else
                                {
                                    logger.LogError("VHD disk does not contain BIOS partition table. Other partitions tables are not supported.");
                                }
                            }
                        }
                    }
                }
                finally
                {
                    this.RemoveVssSnapshotOfSystemVolume(backupComponents); //always remove the VSS snapshot, either after success or failure
                }
                logger.LogInformation("Cloning operating system completed.");
            }
            return;
        }

        private void CreateVssSnapshotOfSystemVolume(IVssBackupComponents backupComponents)
        {
            try
            {
                logger.LogInformation($"Creating VSS snapshot for drive {migrationData.OperatingSystemDriveLetter}:\\");
                backupComponents.InitializeForBackup(null);
                backupComponents.GatherWriterMetadata();
                backupComponents.SetContext(VssVolumeSnapshotAttributes.Persistent | VssVolumeSnapshotAttributes.NoAutoRelease);

                this.vssSnapshotSetId = backupComponents.StartSnapshotSet();

                this.vssSnapshotId = backupComponents.AddToSnapshotSet($"{migrationData.OperatingSystemDriveLetter}:\\");
                backupComponents.SetBackupState(false, true, VssBackupType.Differential, false);
                backupComponents.PrepareForBackup();
                backupComponents.DoSnapshotSet();

                VssSnapshotProperties snapshotInfo = backupComponents.GetSnapshotProperties(this.vssSnapshotId);
                logger.LogDebug($"VSS snapshot created. Root Path: {snapshotInfo.SnapshotDeviceObject}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occured during VSS snapshot creation");
                if (this.vssSnapshotId != Guid.Empty)
                {
                    backupComponents.DeleteSnapshot(this.vssSnapshotId, true);
                    this.vssSnapshotId = Guid.Empty;
                    this.vssSnapshotSetId = Guid.Empty;
                }
                throw;
            }
        }

        private void RemoveVssSnapshotOfSystemVolume(IVssBackupComponents backupComponents)
        {
            if (this.vssSnapshotId != Guid.Empty)
            {
                try
                {
                    backupComponents.BackupComplete();
                }
                catch (VssBadStateException) { }

                VssSnapshotProperties snapshotInfo = backupComponents.GetSnapshotProperties(this.vssSnapshotId);

                logger.LogDebug($"Removing snapshot {snapshotInfo.SnapshotDeviceObject}");
                backupComponents.DeleteSnapshot(this.vssSnapshotId, true);
            }
        }
    }
}
