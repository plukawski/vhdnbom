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

using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using DiscUtils;
using Microsoft.Extensions.Logging;
using Moq;
using NUnit.Framework;
using VHDNBOM.Logic.Helpers;
using VHDNBOM.Logic.Helpers.Decorators;
using VHDNBOM.Logic.MigrationSteps;
using VHDNBOM.Logic.Models;

namespace VHDNBOM.Tests.StepTests
{
    [TestFixture]
    public class FixClonedOsRegistryMigrationStepTests
    {
        private Mock<ILogger<FixClonedOsRegistryMigrationStep>> loggerMock;
        private Mock<IFileSystemHelper> fileSystemHelperMock;
        private Mock<System.IO.Abstractions.IFileSystem> fileSystemMock;

        [SetUp]
        public void Prepare()
        {
            loggerMock = new Mock<ILogger<FixClonedOsRegistryMigrationStep>>();

            fileSystemHelperMock = new Mock<IFileSystemHelper>();
            fileSystemMock = new Mock<System.IO.Abstractions.IFileSystem>();
        }

        [Test]
        public void CheckPrerequisitiesTest_ShouldBeOk()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();

            FixClonedOsRegistryMigrationStep step = new FixClonedOsRegistryMigrationStep(loggerMock.Object,
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
        public void PerformStepTest_ShouldFinishOk()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";
            flowData.VhdFileName = "test.vhdx";

            MemoryStream streamMock = new MemoryStream();
            StreamWriter swMock = new StreamWriter(streamMock);
            Mock<IDriveInfo> driveInfoMock = new Mock<IDriveInfo>();
            driveInfoMock.Setup(x => x.AvailableFreeSpace)
                .Returns(600);

            fileSystemMock.Setup(x => x.DriveInfo.FromDriveName(It.IsAny<string>()))
                .Returns(driveInfoMock.Object);

            Mock<VirtualDiskDecorator> diskMock = new Mock<VirtualDiskDecorator>();
            Mock<BiosPartitionTableDecorator> partitionTableMock = new Mock<BiosPartitionTableDecorator>();
            Mock<PartitionInfoDecorator> partitionInfoMock = new Mock<PartitionInfoDecorator>();
            Mock<SparseStream> sparseStreamMock = new Mock<SparseStream>();
            Mock<DiscFileSystem> discFileSystemMock = new Mock<DiscFileSystem>();

            SparseStream sparseStream = sparseStreamMock.Object;

            partitionInfoMock.Setup(x => x.Open())
                .Returns(sparseStream)
                .Verifiable();

            var partInfo = partitionInfoMock.Object;

            partitionTableMock.Setup(x => x.Partitions)
                .Returns(new List<PartitionInfoDecorator>()
                {
                    partInfo
                })
                .Verifiable();

            diskMock.Setup(x => x.Partitions)
                .Returns(partitionTableMock.Object);

            diskMock.Setup(x => x.Geometry)
                .Returns(new Geometry((long)500, 1, 10, 512));

            this.fileSystemHelperMock.Setup(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")))
                .Returns(diskMock.Object)
                .Verifiable();

            this.fileSystemHelperMock.Setup(x => x.OpenNtfsFileSystem(It.Is<SparseStream>(stream => stream == sparseStream)))
                .Returns(discFileSystemMock.Object)
                .Verifiable();

            this.fileSystemHelperMock.Setup(x => x.ChangeSystemDriveMappingFromRegistry(It.Is<Stream>(stream => stream == sparseStream)))
                .Verifiable();

            FixClonedOsRegistryMigrationStep step = new FixClonedOsRegistryMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            step.PerformStep();
            swMock.Dispose();

            //assert
            this.fileSystemHelperMock.Verify(x => x.ChangeSystemDriveMappingFromRegistry(It.IsAny<Stream>()), Times.Once);
        }
    }
}
