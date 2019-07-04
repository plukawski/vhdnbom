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
using DiscUtils.Partitions;
using Alphaleonis.Win32.Security;
using System.IO;
using VHDNBOM.Logic.Helpers.Decorators;

namespace VHDNBOM.Logic.MigrationSteps
{
    public class CloneTempVhdToFinalVhdMigrationStep : BaseMigrationStep
    {
        private readonly ILogger<CloneTempVhdToFinalVhdMigrationStep> logger;
        private readonly IFileSystemHelper fileSystemHelper;
        private readonly System.IO.Abstractions.IFileSystem fileSystem;
        string sourceVhdFile, destinationVhdFile;

        public CloneTempVhdToFinalVhdMigrationStep(ILogger<CloneTempVhdToFinalVhdMigrationStep> logger,
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
            if (this.migrationData.TemporaryVhdFileIsTheFinalOne)
            {
                return;
            }

            logger.LogInformation("Copying partition data...");
            this.sourceVhdFile = $"{migrationData.VhdFileTemporaryFolder}\\{migrationData.VhdFileName}";
            this.destinationVhdFile = $"{migrationData.VhdFileDestinationFolder}\\{migrationData.VhdFileName}";

            using (PrivilegeEnabler privEnabler = new PrivilegeEnabler(Privilege.Backup, Privilege.Restore))
            {
                using (VirtualDiskDecorator sourceVhd = this.fileSystemHelper.OpenVhdx(this.sourceVhdFile))
                {
                    using (Stream inpuStream = sourceVhd.Partitions[0].Open())
                    {
                        using (VirtualDiskDecorator destinationVhd = this.fileSystemHelper.OpenVhdx(this.destinationVhdFile))
                        {
                            var partitionTable = destinationVhd.Partitions;
                            int partIndex = partitionTable.CreatePrimaryBySector(1, (inpuStream.Length / destinationVhd.Geometry.BytesPerSector), BiosPartitionTypes.Ntfs, false);
                            PartitionInfoDecorator partition = destinationVhd.Partitions[partIndex];
                            using (var outputStream = partition.Open())
                            {
                                this.fileSystemHelper.CloneNtfsFileSystem(inpuStream, outputStream, logger);
                            }
                        }
                    }
                }
            }

            if (migrationData.DeleteTemporaryVhdFile)
            {
                this.fileSystem.File.Delete(this.sourceVhdFile);
            }

            logger.LogInformation("Cloning temporary vhd to destination vhd completed.");
        }
    }
}
