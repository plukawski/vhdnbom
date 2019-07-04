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

using System.IO;
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
    public class CreateTempVhdMigrationStepTests
    {
        private Mock<ILogger<CreateTemporaryVhdMigrationStep>> loggerMock;
        private Mock<IOperatingSystemHelper> osHelperMock;
        private Mock<IFileSystemHelper> fileSystemHelperMock;
        private Mock<IDriveInfo> systemDriveInfoMock;
        private Mock<IFileSystem> fileSystemMock;

        [SetUp]
        public void Prepare()
        {
            loggerMock = new Mock<ILogger<CreateTemporaryVhdMigrationStep>>();
            osHelperMock = new Mock<IOperatingSystemHelper>();
            osHelperMock.Setup(x => x.IsOperatingSystemSupported())
                .Returns(() => { return true; });

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

            this.fileSystemMock.Setup(x => x.File.Exists(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return path == flowData.VhdFileTemporaryFolder + "\\" ? false : true;
                });

            CreateTemporaryVhdMigrationStep step = new CreateTemporaryVhdMigrationStep(loggerMock.Object,
                osHelperMock.Object,
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

            this.fileSystemMock.Setup(x => x.File.Exists(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return path == flowData.VhdFileTemporaryFolder + "\\" ? true : false;
                });

            CreateTemporaryVhdMigrationStep step = new CreateTemporaryVhdMigrationStep(loggerMock.Object,
                osHelperMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            string[] messages = new string[0];
            bool result = step.CheckPrerequisities(ref messages);

            //assert
            Assert.IsFalse(result);
            Assert.AreEqual(1, messages.Where(x => x.Contains("already exists")).Count());
            Assert.AreEqual(1, messages.Where(x => x.Contains("DISKPART was not found")).Count());
        }

        [Test]
        public void PerformStepTest_ShouldFinishOk()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";

            StreamWriter swMock = new StreamWriter(new MemoryStream());

            this.fileSystemMock.Setup(x => x.File.CreateText(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return swMock;
                })
                .Verifiable();

            this.fileSystemMock.Setup(x => x.Directory.Exists(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return path == flowData.VhdFileTemporaryFolder ? false : true;
                });

            this.fileSystemMock.Setup(x => x.Directory.CreateDirectory(It.IsAny<string>()))
                .Verifiable();

            this.fileSystemMock.Setup(x => x.File.Delete(It.IsAny<string>()))
                .Verifiable();

            CreateTemporaryVhdMigrationStep step = new CreateTemporaryVhdMigrationStep(loggerMock.Object,
                osHelperMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            step.PerformStep();
            swMock.Dispose();

            //assert
            this.fileSystemMock.Verify(x => x.Directory.CreateDirectory(It.IsAny<string>()), Times.Once);
            this.fileSystemMock.Verify(x => x.File.CreateText(It.IsAny<string>()), Times.Once);
            this.fileSystemMock.Verify(x => x.File.Delete(It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void PerformStepTest_ShouldNotCreateTempDir()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";

            StreamWriter swMock = new StreamWriter(new MemoryStream());

            this.fileSystemMock.Setup(x => x.File.CreateText(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return swMock;
                })
                .Verifiable();

            this.fileSystemMock.Setup(x => x.Directory.Exists(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return path == flowData.VhdFileTemporaryFolder ? true : false;
                });

            this.fileSystemMock.Setup(x => x.Directory.CreateDirectory(It.IsAny<string>()))
                .Verifiable();

            this.fileSystemMock.Setup(x => x.File.Delete(It.IsAny<string>()))
                .Verifiable();

            CreateTemporaryVhdMigrationStep step = new CreateTemporaryVhdMigrationStep(loggerMock.Object,
                osHelperMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            step.PerformStep();
            swMock.Dispose();

            //assert
            this.fileSystemMock.Verify(x => x.Directory.CreateDirectory(It.IsAny<string>()), Times.Never);
            this.fileSystemMock.Verify(x => x.File.CreateText(It.IsAny<string>()), Times.Once);
            this.fileSystemMock.Verify(x => x.File.Delete(It.IsAny<string>()), Times.Once);
        }
    }
}
