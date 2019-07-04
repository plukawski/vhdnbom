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
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using VHDNBOM.Logic.Helpers;
using VHDNBOM.Logic.Helpers.Decorators;
using VHDNBOM.Logic.Models;

namespace VHDNBOM.Logic.MigrationSteps
{
    public class ShrinkVhdVolumeMigrationStep : BaseMigrationStep
    {
        private readonly ILogger<ShrinkVhdVolumeMigrationStep> logger;
        private readonly IFileSystemHelper fileSystemHelper;
        private readonly System.IO.Abstractions.IFileSystem fileSystem;

        public ShrinkVhdVolumeMigrationStep(ILogger<ShrinkVhdVolumeMigrationStep> logger,
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
            if (!this.migrationData.TemporaryVhdFileIsTheFinalOne)
            {
                logger.LogInformation("Shrinking vhd image.");

                ShrinkVolume();

                logger.LogInformation("Shrinking vhd image completed.");
            }

            return;
        }

        private void ShrinkVolume()
        {
            var systemDriveInfo = fileSystemHelper.GetSystemDriveInfo();

            long desiredShrinkSize = migrationData.DesiredTempVhdShrinkSize >> 20; // / (1024 * 1024);
            string vhdFullName = $"{migrationData.VhdFileTemporaryFolder}\\{migrationData.VhdFileName}";
            logger.LogDebug($"Shrinking volume '{vhdFullName}' by {desiredShrinkSize} MB");

            List<string> diskpartScriptContent = new List<string>()
            {
                $"select vdisk file={vhdFullName}",
                "attach vdisk",
                "select partition 1",
                $"shrink desired={desiredShrinkSize}",
                "detach vdisk",
                "exit"
            };

            Policy retryPolicy = Policy
            .Handle<Exception>()
            .Retry(onRetry: (exception, retry) =>
            {
                this.logger.LogWarning(exception, $"Shrinking volume failed {retry} time.");
                diskpartScriptContent = new List<string>()
                {
                    $"select vdisk file={vhdFullName}",
                    "select partition 1",
                    $"shrink desired={desiredShrinkSize}",
                    "detach vdisk",
                    "exit"
                };
            });

            string diskpartScriptLocation = $"{migrationData.VhdFileTemporaryFolder}\\diskpartScriptContent.txt";

            retryPolicy.Execute(() =>
            {
                using (var sw = fileSystem.File.CreateText(diskpartScriptLocation))
                {
                    foreach (string line in diskpartScriptContent)
                    {
                        sw.WriteLine(line);
                    }
                }

                fileSystemHelper.ExecuteDiskpart(diskpartScriptLocation, logger);
            });

            //cleanup
            fileSystem.File.Delete(diskpartScriptLocation);

            this.ValidateVhdVolumeSize();
        }

        private void ValidateVhdVolumeSize()
        {
            string vhdFile = $"{migrationData.VhdFileTemporaryFolder}\\{migrationData.VhdFileName}";
            using (VirtualDiskDecorator sourceVhd = this.fileSystemHelper.OpenVhdx(vhdFile))
            {
                using (Stream inpuStream = sourceVhd.Partitions[0].Open())
                {
                    if (inpuStream.Length > migrationData.DestinationVhdMaxFileSize)
                    {
                        throw new Exception("Volume after shrink operation is too big to successfully migrate to system drive. Migration cannot continue.");
                    }
                }
            }
        }
    }
}
