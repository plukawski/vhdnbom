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
using System.Linq;
using Alphaleonis.Win32.Vss;
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
    public class CloneOsMigrationStepTests
    {
        private Mock<ILogger<CloneOSMigrationStep>> loggerMock;
        private Mock<IOperatingSystemHelper> osHelperMock;
        private Mock<IFileSystemHelper> fileSystemHelperMock;
        private Mock<IVssBackupComponents> vssBackupComponentsMock;
        private Mock<IVssImplementation> vssImplementationMock;

        [SetUp]
        public void Prepare()
        {
            loggerMock = new Mock<ILogger<CloneOSMigrationStep>>();
            osHelperMock = new Mock<IOperatingSystemHelper>();
            osHelperMock.Setup(x => x.IsOperatingSystemSupported())
                .Returns(() => { return true; });

            fileSystemHelperMock = new Mock<IFileSystemHelper>();
            vssBackupComponentsMock = new Mock<IVssBackupComponents>();
            vssImplementationMock = new Mock<IVssImplementation>();
            vssImplementationMock.Setup(x => x.CreateVssBackupComponents())
                .Returns(vssBackupComponentsMock.Object);


        }

        [Test]
        public void CheckPrerequisitiesTest_ShouldBeOk()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";

            this.vssBackupComponentsMock.Setup(x => x.IsVolumeSupported(It.IsAny<string>()))
                .Returns(true);

            CloneOSMigrationStep step = new CloneOSMigrationStep(loggerMock.Object,
                osHelperMock.Object,
                fileSystemHelperMock.Object,
                flowData,
                vssImplementationMock.Object);

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

            this.vssBackupComponentsMock.Setup(x => x.IsVolumeSupported(It.IsAny<string>()))
                .Returns(false);

            CloneOSMigrationStep step = new CloneOSMigrationStep(loggerMock.Object,
                osHelperMock.Object,
                fileSystemHelperMock.Object,
                flowData,
                vssImplementationMock.Object);

            //act
            string[] messages = new string[0];
            bool result = step.CheckPrerequisities(ref messages);

            //assert
            Assert.IsFalse(result);
            Assert.AreEqual(1, messages.Where(x => x.Contains("System volume does not support VSS")).Count());
        }

        [Test]
        public void PerformStepTest_ShouldFinishOk()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";
            flowData.OperatingSystemDriveLetter = 'C';
            flowData.VhdFileName = "test.vhdx";

            MemoryStream streamMock = new MemoryStream();
            StreamWriter swMock = new StreamWriter(streamMock);
            Mock<VirtualDiskDecorator> diskMock = new Mock<VirtualDiskDecorator>();
            Mock<BiosPartitionTableDecorator> partitionTableMock = new Mock<BiosPartitionTableDecorator>();
            Mock<PartitionInfoDecorator> partitionInfoMock = new Mock<PartitionInfoDecorator>();
            Mock<SparseStream> sparseStreamMock = new Mock<SparseStream>();
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

            VssSnapshotProperties fakeSnapshotDetails = new VssSnapshotProperties(Guid.NewGuid(), Guid.NewGuid(), 1, "snapshotDeviceObject", "C:", "unit-testing", "unit-testing", null, null, Guid.NewGuid(), VssVolumeSnapshotAttributes.Differential, DateTime.Now, VssSnapshotState.Created);

            this.vssBackupComponentsMock.Setup(x => x.InitializeForBackup(It.IsAny<string>())).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.GatherWriterMetadata()).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.SetContext(It.IsAny<VssVolumeSnapshotAttributes>())).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.StartSnapshotSet())
                .Returns(fakeSnapshotDetails.SnapshotSetId)
                .Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.AddToSnapshotSet(It.Is<string>((param) => param == $"{flowData.OperatingSystemDriveLetter}:\\")))
                .Returns(fakeSnapshotDetails.SnapshotId)
                .Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.PrepareForBackup()).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.DoSnapshotSet()).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.DeleteSnapshot(It.Is<Guid>((snapshotId) => snapshotId == fakeSnapshotDetails.SnapshotId), It.IsAny<bool>())).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.GetSnapshotProperties(It.Is<Guid>((snapshotId) => snapshotId == fakeSnapshotDetails.SnapshotId)))
                .Returns(fakeSnapshotDetails)
                .Verifiable();

            this.fileSystemHelperMock.Setup(x => x.OpenRawDiskStream(It.Is<string>(snapshotPath => snapshotPath == fakeSnapshotDetails.SnapshotDeviceObject)))
                .Returns(streamMock)
                .Verifiable();

            this.fileSystemHelperMock.Setup(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")))
                .Returns(diskMock.Object)
                .Verifiable();

            this.fileSystemHelperMock.Setup(x => x.CloneNtfsFileSystem(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<ILogger>()))
                .Verifiable();

            CloneOSMigrationStep step = new CloneOSMigrationStep(loggerMock.Object,
                osHelperMock.Object,
                fileSystemHelperMock.Object,
                flowData,
                vssImplementationMock.Object);

            //act
            step.PerformStep();
            swMock.Dispose();

            //assert
            this.vssBackupComponentsMock.Verify(x => x.InitializeForBackup(It.IsAny<string>()), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.GatherWriterMetadata(), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.SetContext(It.IsAny<VssVolumeSnapshotAttributes>()), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.StartSnapshotSet(), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.AddToSnapshotSet(It.Is<string>((param) => param == $"{flowData.OperatingSystemDriveLetter}:\\")), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.PrepareForBackup(), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.DoSnapshotSet(), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.DeleteSnapshot(It.Is<Guid>((snapshotId) => snapshotId == fakeSnapshotDetails.SnapshotId), It.IsAny<bool>()), Times.Once);
            partitionTableMock.Verify(x => x.CreatePrimaryBySector(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<byte>(), It.IsAny<bool>()), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.CloneNtfsFileSystem(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<ILogger>()), Times.Once);
        }

        [Test]
        public void PerformStepTest_ShouldDeleteSnapshotOnError()
        {
            //arrange
            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = "temporary\\folder";
            flowData.OperatingSystemDriveLetter = 'C';
            flowData.VhdFileName = "test.vhdx";

            Mock<VirtualDiskDecorator> diskMock = new Mock<VirtualDiskDecorator>();
            Mock<BiosPartitionTableDecorator> partitionTableMock = new Mock<BiosPartitionTableDecorator>();

            diskMock.Setup(x => x.Partitions)
                .Returns(partitionTableMock.Object);

            MemoryStream streamMock = new MemoryStream();
            StreamWriter swMock = new StreamWriter(streamMock);
            VssSnapshotProperties fakeSnapshotDetails = new VssSnapshotProperties(Guid.NewGuid(), Guid.NewGuid(), 1, "snapshotDeviceObject", "C:", "unit-testing", "unit-testing", null, null, Guid.NewGuid(), VssVolumeSnapshotAttributes.Differential, DateTime.Now, VssSnapshotState.Created);

            this.vssBackupComponentsMock.Setup(x => x.InitializeForBackup(It.IsAny<string>())).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.GatherWriterMetadata()).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.SetContext(It.IsAny<VssVolumeSnapshotAttributes>())).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.StartSnapshotSet())
                .Returns(fakeSnapshotDetails.SnapshotSetId)
                .Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.AddToSnapshotSet(It.Is<string>((param) => param == $"{flowData.OperatingSystemDriveLetter}:\\")))
                .Returns(fakeSnapshotDetails.SnapshotId)
                .Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.PrepareForBackup()).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.DoSnapshotSet())
                .Throws<Exception>()
                .Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.DeleteSnapshot(It.Is<Guid>((snapshotId) => snapshotId == fakeSnapshotDetails.SnapshotId), It.IsAny<bool>())).Verifiable();
            this.vssBackupComponentsMock.Setup(x => x.GetSnapshotProperties(It.Is<Guid>((snapshotId) => snapshotId == fakeSnapshotDetails.SnapshotId)))
                .Returns(fakeSnapshotDetails)
                .Verifiable();

            this.fileSystemHelperMock.Setup(x => x.OpenRawDiskStream(It.Is<string>(snapshotPath => snapshotPath == fakeSnapshotDetails.SnapshotDeviceObject)))
                .Returns(streamMock)
                .Verifiable();

            this.fileSystemHelperMock.Setup(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")))
                .Returns(diskMock.Object)
                .Verifiable();

            this.fileSystemHelperMock.Setup(x => x.CloneNtfsFileSystem(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<ILogger>()))
                .Verifiable();

            CloneOSMigrationStep step = new CloneOSMigrationStep(loggerMock.Object,
                osHelperMock.Object,
                fileSystemHelperMock.Object,
                flowData,
                vssImplementationMock.Object);

            //act
            Assert.Throws<Exception>(() =>
            {
                step.PerformStep();
            });
            swMock.Dispose();

            //assert
            this.vssBackupComponentsMock.Verify(x => x.InitializeForBackup(It.IsAny<string>()), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.GatherWriterMetadata(), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.SetContext(It.IsAny<VssVolumeSnapshotAttributes>()), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.StartSnapshotSet(), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.AddToSnapshotSet(It.Is<string>((param) => param == $"{flowData.OperatingSystemDriveLetter}:\\")), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.PrepareForBackup(), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.DoSnapshotSet(), Times.Once);
            this.vssBackupComponentsMock.Verify(x => x.DeleteSnapshot(It.Is<Guid>((snapshotId) => snapshotId == fakeSnapshotDetails.SnapshotId), It.IsAny<bool>()), Times.Once);
            this.fileSystemHelperMock.Verify(x => x.OpenVhdx(It.Is<string>(snapshotPath => snapshotPath == $"{flowData.VhdFileTemporaryFolder}\\{flowData.VhdFileName}")), Times.Never);
            this.fileSystemHelperMock.Verify(x => x.CloneNtfsFileSystem(It.IsAny<Stream>(), It.IsAny<Stream>(), It.IsAny<ILogger>()), Times.Never);
        }
    }
}
