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
    public class ShrinkVhdVolumeMigrationStepTests
    {
        private Mock<ILogger<ShrinkVhdVolumeMigrationStep>> loggerMock;
        private Mock<IFileSystemHelper> fileSystemHelperMock;
        private Mock<IDriveInfo> systemDriveInfoMock;
        private Mock<System.IO.Abstractions.IFileSystem> fileSystemMock;

        [SetUp]
        public void Prepare()
        {
            loggerMock = new Mock<ILogger<ShrinkVhdVolumeMigrationStep>>();

            fileSystemHelperMock = new Mock<IFileSystemHelper>();
            systemDriveInfoMock = new Mock<IDriveInfo>();
            fileSystemMock = new Mock<System.IO.Abstractions.IFileSystem>();

            systemDriveInfoMock.Setup(x => x.AvailableFreeSpace)
                .Returns(51);
            systemDriveInfoMock.Setup(x => x.TotalSize)
                .Returns(100);

            fileSystemHelperMock.Setup(x => x.GetSystemDriveInfo())
                .Returns(() =>
                {
                    return systemDriveInfoMock.Object;
                })
                .Verifiable();
        }

        [Test]
        public void CheckPrerequisitiesTest_ShouldBeOk()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();

            ShrinkVhdVolumeMigrationStep step = new ShrinkVhdVolumeMigrationStep(loggerMock.Object,
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
            flowData.VhdFileName = "test.vhdx";
            flowData.DestinationVhdMaxFileSize = 200000000;
            flowData.DesiredTempVhdShrinkSize = 100000000;
            long desiredShrinkSize = flowData.DesiredTempVhdShrinkSize >> 20; // / (1024 * 1024);

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

            Mock<VirtualDiskDecorator> diskMock = new Mock<VirtualDiskDecorator>();
            Mock<BiosPartitionTableDecorator> partitionTableMock = new Mock<BiosPartitionTableDecorator>();
            Mock<PartitionInfoDecorator> partitionInfoMock = new Mock<PartitionInfoDecorator>();
            Mock<SparseStream> sparseStreamMock = new Mock<SparseStream>();
            sparseStreamMock.Setup(x => x.Length)
                .Returns(flowData.DestinationVhdMaxFileSize);

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

            this.fileSystemHelperMock.Setup(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")))
                .Returns(diskMock.Object)
                .Verifiable();

            ShrinkVhdVolumeMigrationStep step = new ShrinkVhdVolumeMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            step.PerformStep();

            //assert
            streamWriterMock.Verify(x => x.WriteLine(It.Is<string>(str => str.Contains($"shrink desired={desiredShrinkSize}"))), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.ExecuteDiskpart(It.IsAny<string>(), It.IsAny<ILogger>()), Times.Once);
            this.fileSystemMock.Verify(x => x.File.CreateText(It.IsAny<string>()), Times.Once);
            this.fileSystemMock.Verify(x => x.File.Delete(It.IsAny<string>()), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")), Times.Once);
        }

        [Test]
        public void PerformStepTest_ShouldThrowExceptionOnWrongShrinkSize()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";
            flowData.VhdFileDestinationFolder = "destination\\folder";
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

            Mock<VirtualDiskDecorator> diskMock = new Mock<VirtualDiskDecorator>();
            Mock<BiosPartitionTableDecorator> partitionTableMock = new Mock<BiosPartitionTableDecorator>();
            Mock<PartitionInfoDecorator> partitionInfoMock = new Mock<PartitionInfoDecorator>();
            Mock<SparseStream> sparseStreamMock = new Mock<SparseStream>();
            sparseStreamMock.Setup(x => x.Length)
                .Returns(flowData.DestinationVhdMaxFileSize + 1);   //returns not supported vhd partition size

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

            this.fileSystemHelperMock.Setup(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")))
                .Returns(diskMock.Object)
                .Verifiable();

            ShrinkVhdVolumeMigrationStep step = new ShrinkVhdVolumeMigrationStep(loggerMock.Object,
                fileSystemHelperMock.Object,
                fileSystemMock.Object,
                flowData);

            //act
            Exception ex = Assert.Throws<Exception>(() =>
            {
                step.PerformStep();
            });

            //assert
            Assert.IsTrue(ex.Message.Contains("Volume after shrink operation is too big to successfully migrate to system drive"));
            streamWriterMock.Verify(x => x.WriteLine(It.Is<string>(str => str.Contains("shrink desired"))), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.ExecuteDiskpart(It.IsAny<string>(), It.IsAny<ILogger>()), Times.Once);
            this.fileSystemMock.Verify(x => x.File.CreateText(It.IsAny<string>()), Times.Once);
            this.fileSystemMock.Verify(x => x.File.Delete(It.IsAny<string>()), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")), Times.Once);
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

            ShrinkVhdVolumeMigrationStep step = new ShrinkVhdVolumeMigrationStep(loggerMock.Object,
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
