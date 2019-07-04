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
using System.IO.Abstractions;
using VHDNBOM.Logic.Helpers;
using VHDNBOM.Logic.Models;

namespace VHDNBOM.Logic.MigrationSteps
{
    public class PrepareMigrationStep : BaseMigrationStep
    {
        private readonly ILogger<PrepareMigrationStep> logger;
        private readonly IOperatingSystemHelper osHelper;
        private readonly IFileSystemHelper fileSystemHelper;

        public PrepareMigrationStep(ILogger<PrepareMigrationStep> logger,
            IOperatingSystemHelper osHelper,
            IFileSystemHelper fileSystemHelper,
            MigrationFlowData migrationData) : base(migrationData)
        {
            this.logger = logger;
            this.osHelper = osHelper;
            this.fileSystemHelper = fileSystemHelper;
        }

        public override bool CheckPrerequisities(ref string[] messages)
        {
            List<string> messageList = new List<string>();

            logger.LogDebug("Checking critical requirements.");
            bool osSupported = osHelper.IsOperatingSystemSupported();
            if (!osSupported)
            {
                messageList.Add("Not supported operating system detected. Only the following Windows systems are supported: Windows 7 (Enterprise,Ultimate), Windows 8, Windows 8.1, Windows 10");
            }

            if (osHelper.IsBitlockerEnabledOnSystemDrive())
            {
                messageList.Add("Migration of operating system to VHD native boot is not supported when BitLocker is enabled. Migration cannot continue.");
            }

            if (string.IsNullOrWhiteSpace(migrationData.VhdFileTemporaryFolder))
            {
                messageList.Add("Cannot find any drive with free space to store temporary VHD(x) file.");
            }

            IDriveInfo systemDriveInfo = fileSystemHelper.GetSystemDriveInfo();
            if (systemDriveInfo.AvailableFreeSpace < (systemDriveInfo.TotalSize / 2) && !migrationData.TemporaryVhdFileIsTheFinalOne)
            {
                messageList.Add("Not enough available free space on system drive to place the VHD(x) file. System drive must have at least 50% of free space to perform in place os migration to native VHD boot.");
            }

            messages = messageList.ToArray();
            return messageList.Count == 0;
        }

        public override void PerformStep()
        {
            logger.LogInformation("Preparing migration flow.");
            return;
        }
    }
}
