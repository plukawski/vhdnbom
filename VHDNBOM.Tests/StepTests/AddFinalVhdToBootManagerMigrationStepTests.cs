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

using System;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using VHDNBOM.Logic.Helpers;
using VHDNBOM.Logic.MigrationSteps;
using VHDNBOM.Logic.Models;

namespace VHDNBOM.Tests.StepTests
{
    [TestFixture]
    public class AddFinalVhdToBootManagerMigrationStepTests
    {
        private Mock<ILogger<AddFinalVhdToBootManagerMigrationStep>> loggerMock;
        private Mock<IFileSystemHelper> fileSystemHelperMock;
        private Mock<IDriveInfo> systemDriveInfoMock;
        private Mock<IFileSystem> fileSystemMock;

        [SetUp]
        public void Prepare()
        {
            loggerMock = new Mock<ILogger<AddFinalVhdToBootManagerMigrationStep>>();

            fileSystemHelperMock = new Mock<IFileSystemHelper>();
            systemDriveInfoMock = new Mock<IDriveInfo>();
            fileSystemMock = new Mock<IFileSystem>();

            systemDriveInfoMock.Setup(x => x.AvailableFreeSpace)
                .Returns(51);
            systemDriveInfoMock.Setup(x => x.TotalSize)
                .Returns(100);

            fileSystemHelperMock.Setup(x => x.GetSystemDriveInfo())
                .Returns(() =>
                {
                    return systemDriveInfoMock.Object;
                });
        }

        [Test]
        public void CheckPrerequisitiesTest_ShouldBeOk()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";
            flowData.AddVhdToBootManager = true;

            this.fileSystemMock.Setup(x => x.File.Exists(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return path == flowData.VhdFileTemporaryFolder + "\\" ? false : true;
                });

            AddFinalVhdToBootManagerMigrationStep step = new AddFinalVhdToBootManagerMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            string[] messages = new string[0];
            bool result = step.CheckPrerequisities(ref messages);

            //assert
            Assert.IsTrue(result);

        }

        [Test]
        public void CheckPrerequisitiesTest_ShouldFail()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";
            flowData.AddVhdToBootManager = true;

            this.fileSystemMock.Setup(x => x.File.Exists(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return false;
                });

            AddFinalVhdToBootManagerMigrationStep step = new AddFinalVhdToBootManagerMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            string[] messages = new string[0];
            bool result = step.CheckPrerequisities(ref messages);

            //assert
            Assert.IsFalse(result);
            Assert.AreEqual(1, messages.Where(x => x.Contains("BCDEDIT was not found on your system")).Count());
        }

        [Test]
        public void PerformStepTest_ShouldFinishOk()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "F:\\temporary\\folder";
            flowData.AddVhdToBootManager = true;
            flowData.OperatingSystemDriveLetter = 'C';

            Guid newEntryId = Guid.NewGuid();

            this.fileSystemHelperMock.Setup(x => x.ExecuteBcdeditCommand(
                It.Is<string>(script => script.Contains("/copy")),
                It.IsAny<ILogger>()))
                .Returns($"Cloned successfully {{{newEntryId}}}");

            this.fileSystemHelperMock.Setup(x => x.ExecuteBcdeditCommand(
                It.Is<string>(script => script.Contains("/set") && script.Contains(newEntryId.ToString())),
                It.IsAny<ILogger>()))
                .Returns($"Operation completed successfully.");

            AddFinalVhdToBootManagerMigrationStep step = new AddFinalVhdToBootManagerMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            step.PerformStep();

            //assert
            this.fileSystemHelperMock.Verify(x => x.ExecuteBcdeditCommand(
                It.Is<string>(script => script.Contains("/copy")),
                It.IsAny<ILogger>()), Times.Once);

            this.fileSystemHelperMock.Verify(x => x.ExecuteBcdeditCommand(
                It.Is<string>(script => script.Contains("/set") && script.Contains(newEntryId.ToString())),
                It.IsAny<ILogger>()), Times.Exactly(2));

            this.fileSystemHelperMock.Verify(x => x.ExecuteBcdeditCommand(
                It.Is<string>(script => script.Contains("/set") && script.Contains("vhd=[" + flowData.VhdFileTemporaryFolder[0])),
                It.IsAny<ILogger>()), Times.Exactly(2));
        }

        [Test]
        public void PerformStepTest_ShouldFinishOkButDoNothing()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";
            flowData.AddVhdToBootManager = false;


            AddFinalVhdToBootManagerMigrationStep step = new AddFinalVhdToBootManagerMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            step.PerformStep();

            //assert
            this.fileSystemHelperMock.Verify(x => x.ExecuteBcdeditCommand(
                It.Is<string>(script => script.Contains("/copy")),
                It.IsAny<ILogger>()), Times.Never);

            this.fileSystemHelperMock.Verify(x => x.ExecuteBcdeditCommand(
                It.Is<string>(script => script.Contains("/set")),
                It.IsAny<ILogger>()), Times.Never);
        }

        [Test]
        public void PerformStepTest_ShouldNotBeAbleToCreateNewBootManagerEntry()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";
            flowData.AddVhdToBootManager = true;

            Guid newEntryId = Guid.NewGuid();

            this.fileSystemHelperMock.Setup(x => x.ExecuteBcdeditCommand(
                It.Is<string>(script => script.Contains("/copy")),
                It.IsAny<ILogger>()))
                .Returns($"Operation failed");

            AddFinalVhdToBootManagerMigrationStep step = new AddFinalVhdToBootManagerMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            ArgumentException aex = Assert.Throws<ArgumentException>(() =>
            {
                step.PerformStep();
            });

            //assert
            this.fileSystemHelperMock.Verify(x => x.ExecuteBcdeditCommand(
                It.Is<string>(script => script.Contains("/copy")),
                It.IsAny<ILogger>()), Times.Once);

            this.fileSystemHelperMock.Verify(x => x.ExecuteBcdeditCommand(
                It.Is<string>(script => script.Contains("/set") && script.Contains(newEntryId.ToString())),
                It.IsAny<ILogger>()), Times.Never);
        }
    }
}
