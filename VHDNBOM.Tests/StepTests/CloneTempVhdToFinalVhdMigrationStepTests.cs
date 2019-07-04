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
    public class CloneTempVhdToFinalVhdMigrationStepTests
    {
        private Mock<ILogger<CloneTempVhdToFinalVhdMigrationStep>> loggerMock;
        private Mock<System.IO.Abstractions.IFileSystem> fileSystemMock;
        private Mock<IFileSystemHelper> fileSystemHelperMock;

        [SetUp]
        public void Prepare()
        {
            loggerMock = new Mock<ILogger<CloneTempVhdToFinalVhdMigrationStep>>();
            fileSystemMock = new Mock<System.IO.Abstractions.IFileSystem>();

            fileSystemHelperMock = new Mock<IFileSystemHelper>();
        }

        [Test]
        public void CheckPrerequisitiesTest_ShouldBeOk()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";

            CloneTempVhdToFinalVhdMigrationStep step = new CloneTempVhdToFinalVhdMigrationStep(loggerMock.Object,
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
            flowData.VhdFileDestinationFolder = "destination\\folder";
            flowData.OperatingSystemDriveLetter = 'C';
            flowData.VhdFileName = "test.vhdx";
            flowData.DeleteTemporaryVhdFile = true;

            MemoryStream streamMock = new MemoryStream();
            StreamWriter swMock = new StreamWriter(streamMock);
            Mock<VirtualDiskDecorator> diskMock = new Mock<VirtualDiskDecorator>();
            Mock<BiosPartitionTableDecorator> partitionTableMock = new Mock<BiosPartitionTableDecorator>();
            Mock<PartitionInfoDecorator> partitionInfoMock = new Mock<PartitionInfoDecorator>();
            Mock<SparseStream> sparseStreamMock = new Mock<SparseStream>();
            sparseStreamMock.Setup(x => x.Length)
                .Returns(100);
            SparseStream sparseStream = sparseStreamMock.Object;

            partitionTableMock.Setup(x => x.CreatePrimaryBySector(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<byte>(), It.IsAny<bool>()))
                .Returns(0)
                .Verifiable();

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
                .Returns(new Geometry(100, 1, 2, 512));

            this.fileSystemHelperMock.Setup(x => x.OpenVhdx(It.IsAny<string>()))
                .Returns(diskMock.Object)
                .Verifiable();

            this.fileSystemHelperMock.Setup(x => x.CloneNtfsFileSystem(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<ILogger>()))
                .Verifiable();

            this.fileSystemMock.Setup(x => x.File.Delete(It.IsAny<string>()));

            CloneTempVhdToFinalVhdMigrationStep step = new CloneTempVhdToFinalVhdMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            step.PerformStep();
            swMock.Dispose();

            //assert
            partitionTableMock.Verify(x => x.CreatePrimaryBySector(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<byte>(), It.IsAny<bool>()), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileDestinationFolder}\\{flowData.VhdFileName}")), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.CloneNtfsFileSystem(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<ILogger>()), Times.Once);
            this.fileSystemMock.Verify(x => x.File.Delete(It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void PerformStepTest_ShouldFinishOkButDoNothing()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";
            flowData.VhdFileDestinationFolder = null;
            flowData.OperatingSystemDriveLetter = 'C';
            flowData.VhdFileName = "test.vhdx";

            Mock<VirtualDiskDecorator> diskMock = new Mock<VirtualDiskDecorator>();
            Mock<BiosPartitionTableDecorator> partitionTableMock = new Mock<BiosPartitionTableDecorator>();
            Mock<PartitionInfoDecorator> partitionInfoMock = new Mock<PartitionInfoDecorator>();

            CloneTempVhdToFinalVhdMigrationStep step = new CloneTempVhdToFinalVhdMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            step.PerformStep();

            //assert
            partitionTableMock.Verify(x => x.CreatePrimaryBySector(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<byte>(), It.IsAny<bool>()), Times.Never);
            this.fileSystemHelperMock.Verify(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")), Times.Never);
            this.fileSystemHelperMock.Verify(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileDestinationFolder}\\{flowData.VhdFileName}")), Times.Never);
            this.fileSystemHelperMock.Verify(x => x.CloneNtfsFileSystem(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<ILogger>()), Times.Never);
        }
    }
}
