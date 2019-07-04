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
using VHDNBOM.Logic.Models.Enums;

namespace VHDNBOM.Tests.StepTests
{
    [TestFixture]
    public class CreateFinalVhdMigrationStepTests
    {
        private Mock<ILogger<CreateFinalVhdMigrationStep>> loggerMock;
        private Mock<IFileSystemHelper> fileSystemHelperMock;
        private Mock<IDriveInfo> systemDriveInfoMock;
        private Mock<System.IO.Abstractions.IFileSystem> fileSystemMock;

        [SetUp]
        public void Prepare()
        {
            loggerMock = new Mock<ILogger<CreateFinalVhdMigrationStep>>();

            fileSystemHelperMock = new Mock<IFileSystemHelper>();
            systemDriveInfoMock = new Mock<IDriveInfo>();
            fileSystemMock = new Mock<System.IO.Abstractions.IFileSystem>();
        }

        [Test]
        public void CheckPrerequisitiesTest_ShouldBeOk()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileDestinationFolder = "destination\\folder";

            this.fileSystemMock.Setup(x => x.File.Exists(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return path == flowData.VhdFileDestinationFolder + "\\" ? false : true;
                });

            CreateFinalVhdMigrationStep step = new CreateFinalVhdMigrationStep(loggerMock.Object,
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
            flowData.VhdFileDestinationFolder = "destination\\folder";

            this.fileSystemMock.Setup(x => x.File.Exists(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return path == flowData.VhdFileDestinationFolder + "\\" ? true : false;
                });

            CreateFinalVhdMigrationStep step = new CreateFinalVhdMigrationStep(loggerMock.Object,
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
            flowData.VhdFileDestinationFolder = "destination\\folder";
            flowData.VhdFileName = "test.vhdx";
            flowData.VhdFileType = Logic.Models.Enums.VhdType.VhdxDynamic;
            flowData.DestinationVhdMaxFileSize = 200000000;
            flowData.DesiredTempVhdShrinkSize = 100000000;
            string vhdFullName = $"{flowData.VhdFileDestinationFolder}\\{flowData.VhdFileName}";
            long maximumVhdSizeInMb = flowData.DestinationVhdMaxFileSize >> 20;// / (1024 * 1024);
            string vhdTypeString = flowData.VhdFileType == VhdType.VhdDynamic || flowData.VhdFileType == VhdType.VhdxDynamic
                ? "expandable" : "fixed";


            Mock<StreamWriter> streamWriterMock = new Mock<StreamWriter>(new MemoryStream());
            streamWriterMock.Setup(x => x.WriteLine(It.IsAny<string>()))
                .Verifiable();
            StreamWriter swMock = streamWriterMock.Object;

            this.fileSystemMock.Setup(x => x.File.CreateText(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return swMock;
                })
                .Verifiable();

            this.fileSystemMock.Setup(x => x.Directory.CreateDirectory(It.IsAny<string>()))
                .Verifiable();

            this.fileSystemMock.Setup(x => x.File.Delete(It.IsAny<string>()))
                .Verifiable();

            CreateFinalVhdMigrationStep step = new CreateFinalVhdMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            step.PerformStep();

            //assert
            streamWriterMock.Verify(x => x.WriteLine(It.Is<string>(str => str.Contains($"create vdisk file={vhdFullName} maximum={maximumVhdSizeInMb} type={vhdTypeString}"))), Times.Once);
            streamWriterMock.Verify(x => x.WriteLine(It.Is<string>(str => str.Contains($"select vdisk file={vhdFullName}"))), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.ExecuteDiskpart(It.IsAny<string>(), It.IsAny<ILogger>()), Times.Once);
            this.fileSystemMock.Verify(x => x.File.CreateText(It.IsAny<string>()), Times.Once);
            this.fileSystemMock.Verify(x => x.File.Delete(It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void PerformStepTest_ShouldFinishOkButDoNothing()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";
            flowData.VhdFileDestinationFolder = null;
            flowData.VhdFileName = "test.vhdx";
            flowData.DestinationVhdMaxFileSize = 101;

            Mock<StreamWriter> streamWriterMock = new Mock<StreamWriter>(new MemoryStream());
            streamWriterMock.Setup(x => x.WriteLine(It.IsAny<string>()))
                .Verifiable();
            StreamWriter swMock = streamWriterMock.Object;

            this.fileSystemMock.Setup(x => x.File.CreateText(It.IsAny<string>()))
                .Returns<string>((path) =>
                {
                    return swMock;
                })
                .Verifiable();

            this.fileSystemMock.Setup(x => x.Directory.CreateDirectory(It.IsAny<string>()))
                .Verifiable();

            this.fileSystemMock.Setup(x => x.File.Delete(It.IsAny<string>()))
                .Verifiable();

            CreateFinalVhdMigrationStep step = new CreateFinalVhdMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            step.PerformStep();

            //assert
            streamWriterMock.Verify(x => x.WriteLine(It.Is<string>(str => str.Contains("shrink desired"))), Times.Never);
            this.fileSystemMock.Verify(x => x.File.CreateText(It.IsAny<string>()), Times.Never);
            this.fileSystemMock.Verify(x => x.File.Delete(It.IsAny<string>()), Times.Never);
        }
    }
}
