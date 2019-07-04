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
using System.IO.Abstractions;
using VHDNBOM.Logic.Helpers;
using VHDNBOM.Logic.Models;

namespace VHDNBOM.Logic.MigrationSteps
{
    public class AddFinalVhdToBootManagerMigrationStep : BaseMigrationStep
    {
        private readonly ILogger<AddFinalVhdToBootManagerMigrationStep> logger;
        private readonly IFileSystemHelper fileSystemHelper;
        private readonly IFileSystem fileSystem;
        string vhdBcdeditEntryPath;

        public AddFinalVhdToBootManagerMigrationStep(ILogger<AddFinalVhdToBootManagerMigrationStep> logger,
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

            logger.LogDebug("Checking requirements to modify boot manager.");

            if (this.migrationData.AddVhdToBootManager)
            {
                if (!fileSystem.File.Exists($"{Environment.SystemDirectory}\\bcdedit.exe"))
                {
                    messageList.Add($"BCDEDIT was not found on your system. Migration impossible.");
                }
            }

            messages = messageList.ToArray();
            return messageList.Count == 0;
        }

        public override void PerformStep()
        {
            if (!this.migrationData.AddVhdToBootManager)
            {
                return;
            }

            if (migrationData.TemporaryVhdFileIsTheFinalOne)
            {
                migrationData.VhdFileDestinationFolder = migrationData.VhdFileTemporaryFolder;
            }

            this.vhdBcdeditEntryPath = $"{migrationData.VhdFileDestinationFolder}\\{migrationData.VhdFileName}";
            vhdBcdeditEntryPath = vhdBcdeditEntryPath.Substring(2); //remove drive letter from path
            logger.LogInformation($"Adding {vhdBcdeditEntryPath} vhd image to boot menu.");

            AddVhdToBootMenu();

            logger.LogInformation($"Added {vhdBcdeditEntryPath} vhd image to boot menu.");

            return;
        }

        private void AddVhdToBootMenu()
        {
            string entryDescription = "VHDNBOM OS Image";
            string copyCurrentOutput = this.fileSystemHelper.ExecuteBcdeditCommand($"/copy {{current}} /d \"{entryDescription}\"", logger);

            if (copyCurrentOutput != null && copyCurrentOutput.LastIndexOf('{') > 0 && copyCurrentOutput.LastIndexOf('}') > copyCurrentOutput.LastIndexOf('{'))
            {
                int beginGuidIndex = copyCurrentOutput.LastIndexOf('{');
                int endGuidIndex = copyCurrentOutput.LastIndexOf('}');
                string newEntryGuidValue = copyCurrentOutput.Substring(beginGuidIndex + 1, endGuidIndex - beginGuidIndex - 1);
                logger.LogDebug($"Created new boot menu entry '{entryDescription}' with id {newEntryGuidValue}");

                Guid newEntryId = Guid.Parse(newEntryGuidValue);
                char vhdLocationDriveLetter = migrationData.VhdFileDestinationFolder[1] == ':' ? migrationData.VhdFileDestinationFolder[0] : migrationData.OperatingSystemDriveLetter;
                fileSystemHelper.ExecuteBcdeditCommand($"/set {{{newEntryId}}} osdevice vhd=[{vhdLocationDriveLetter}:]{this.vhdBcdeditEntryPath}", logger);
                fileSystemHelper.ExecuteBcdeditCommand($"/set {{{newEntryId}}} device vhd=[{vhdLocationDriveLetter}:]{this.vhdBcdeditEntryPath}", logger);
            }
            else
            {
                throw new ArgumentException("New boot entry has not been created for unknown reasons. Please check if bcdedit is working correctly on your system.");
            }
        }
    }
}
