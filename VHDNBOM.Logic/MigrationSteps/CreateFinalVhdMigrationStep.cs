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
using VHDNBOM.Logic.Models.Enums;
using System.IO.Abstractions;

namespace VHDNBOM.Logic.MigrationSteps
{
    public class CreateFinalVhdMigrationStep : BaseMigrationStep
    {
        private readonly ILogger<CreateFinalVhdMigrationStep> logger;
        private readonly IFileSystemHelper fileSystemHelper;
        private readonly IFileSystem fileSystem;

        public CreateFinalVhdMigrationStep(ILogger<CreateFinalVhdMigrationStep> logger,
            IFileSystemHelper fileSystemHelper,
            IFileSystem fileSystem,
            MigrationFlowData migrationData) : base(migrationData)
        {
            this.logger = logger;
            this.fileSystemHelper = fileSystemHelper;
            this.fileSystem = fileSystem;
        }

        public override bool CheckPrerequisities(ref string[] messages)
        {
            List<string> messageList = new List<string>();

            logger.LogDebug("Checking vhd requirements.");

            if (!this.migrationData.TemporaryVhdFileIsTheFinalOne)
            {
                string vhdFullName = $"{migrationData.VhdFileDestinationFolder}\\{migrationData.VhdFileName}";
                if (fileSystem.File.Exists(vhdFullName))
                {
                    messageList.Add($"Destination file {vhdFullName} already exists. You have to remove previous system image file before executing this migration.");
                }

                if (!fileSystem.File.Exists($"{Environment.SystemDirectory}\\diskpart.exe"))
                {
                    messageList.Add($"DISKPART was not found on your system. Migration impossible.");
                }
            }

            messages = messageList.ToArray();
            return messageList.Count == 0;
        }

        public override void PerformStep()
        {
            if (this.migrationData.TemporaryVhdFileIsTheFinalOne)
            {
                return;
            }

            logger.LogInformation("Creating final vhd image.");

            if (!fileSystem.Directory.Exists(migrationData.VhdFileDestinationFolder))
            {
                fileSystem.Directory.CreateDirectory(migrationData.VhdFileDestinationFolder);
            }

            CreateDynamicVirtualHardDisk();

            return;
        }

        private void CreateDynamicVirtualHardDisk()
        {
            long maximumVhdSizeInMb = migrationData.DestinationVhdMaxFileSize >> 20;// / (1024 * 1024);
            string vhdTypeString = migrationData.VhdFileType == VhdType.VhdDynamic || migrationData.VhdFileType == VhdType.VhdxDynamic
                ? "expandable" : "fixed";

            string vhdFullName = $"{migrationData.VhdFileDestinationFolder}\\{migrationData.VhdFileName}";
            logger.LogInformation($"Final vhd file path: {vhdFullName}");

            List<string> diskpartScriptContent = new List<string>()
            {
                $"create vdisk file={vhdFullName} maximum={maximumVhdSizeInMb} type={vhdTypeString}",
                $"select vdisk file={vhdFullName}",
                "attach vdisk",
                "convert mbr",
                "detach vdisk",
                "exit"
            };
            string diskpartScriptLocation = $"{migrationData.VhdFileDestinationFolder}\\diskpartScriptContent.txt";
            using (var sw = fileSystem.File.CreateText(diskpartScriptLocation))
            {
                foreach (string line in diskpartScriptContent)
                {
                    sw.WriteLine(line);
                }
            }

            fileSystemHelper.ExecuteDiskpart(diskpartScriptLocation, logger);

            //cleanup
            fileSystem.File.Delete(diskpartScriptLocation);

        }
    }
}
