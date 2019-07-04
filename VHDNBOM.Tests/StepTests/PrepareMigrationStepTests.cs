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
    public class PrepareMigrationStepTests
    {
        private Mock<ILogger<PrepareMigrationStep>> loggerMock;
        private Mock<IOperatingSystemHelper> osHelperMock;
        private Mock<IFileSystemHelper> fileSystemHelperMock;
        private Mock<IDriveInfo> systemDriveInfoMock;

        [SetUp]
        public void Prepare()
        {
            loggerMock = new Mock<ILogger<PrepareMigrationStep>>();
            osHelperMock = new Mock<IOperatingSystemHelper>();
            osHelperMock.Setup(x => x.IsOperatingSystemSupported())
                .Returns(() => { return true; });

            osHelperMock.Setup(x => x.IsBitlockerEnabledOnSystemDrive())
                .Returns(() => { return false; });

            fileSystemHelperMock = new Mock<IFileSystemHelper>();
            systemDriveInfoMock = new Mock<IDriveInfo>();

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

            PrepareMigrationStep step = new PrepareMigrationStep(loggerMock.Object,
                osHelperMock.Object,
                fileSystemHelperMock.Object,
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
            osHelperMock.Setup(x => x.IsOperatingSystemSupported())
                .Returns(() => { return false; });

            osHelperMock.Setup(x => x.IsBitlockerEnabledOnSystemDrive())
                .Returns(() => { return true; });

            MigrationFlowData flowData = new MigrationFlowData();
            flowData.VhdFileTemporaryFolder = null;
            flowData.VhdFileDestinationFolder = "final\\path";

            systemDriveInfoMock.Setup(x => x.AvailableFreeSpace)
                .Returns(49);
            systemDriveInfoMock.Setup(x => x.TotalSize)
                .Returns(100);

            PrepareMigrationStep step = new PrepareMigrationStep(loggerMock.Object,
                osHelperMock.Object,
                fileSystemHelperMock.Object,
                flowData);

            //act
            string[] messages = new string[0];
            bool result = step.CheckPrerequisities(ref messages);

            //assert
            Assert.IsFalse(result);
            Assert.AreEqual(1, messages.Where(x => x.Contains("store temporary")).Count());
            Assert.AreEqual(1, messages.Where(x => x.Contains("Not supported operating system detected")).Count());
            Assert.AreEqual(1, messages.Where(x => x.Contains("Not enough available free space on system drive")).Count());
            Assert.AreEqual(1, messages.Where(x => x.Contains("Migration of operating system to VHD native boot is not supported when BitLocker is enabled")).Count());
        }
    }
}
