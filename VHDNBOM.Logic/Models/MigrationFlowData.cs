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
using VHDNBOM.Logic.Models.Enums;

namespace VHDNBOM.Logic.Models
{
    /// <summary>
    /// Stores data that controls the migration process
    /// </summary>
    public class MigrationFlowData
    {
        /// <summary>
        /// Windows partition drive letter
        /// </summary>
        public char OperatingSystemDriveLetter { get; set; }

        /// <summary>
        /// Folder where temporary VHD file should be created
        /// </summary>
        public string VhdFileTemporaryFolder { get; set; }

        /// <summary>
        /// Folder where final VHD file should be created
        /// </summary>
        public string VhdFileDestinationFolder { get; set; }

        /// <summary>
        /// File name of the vhd file with extension
        /// </summary>
        public string VhdFileName { get; set; }

        /// <summary>
        /// MAximum size in bytes of the temporary VHD file
        /// </summary>
        public long TemporaryVhdFileMaxSize { get; set; }

        /// <summary>
        /// MAximum size of the final VHD file
        /// </summary>
        public long DestinationVhdMaxFileSize { get; set; }

        /// <summary>
        /// Amount of bytes the temporary VHD should be shrinked by before moving its data to the final partition
        /// Required to be able to store the VHD file in the same partition as the physical operating system
        /// </summary>
        public long DesiredTempVhdShrinkSize { get; set; }

        /// <summary>
        /// The type of the VHD file
        /// </summary>
        public VhdType VhdFileType { get; set; }

        /// <summary>
        /// Indicates if the temporary vhd file should be removed after migration completed successfuly
        /// </summary>
        public bool DeleteTemporaryVhdFile { get; set; }

        /// <summary>
        /// Indicates if the temporary vhd file is the final one. In such case the following steps will not be performed:
        /// ShrinkVhdVolumeMigrationStep
        /// CreateFinalVhdMigrationStep
        /// CloneTempVhdToFinalVhdMigrationStep
        /// </summary>
        public bool TemporaryVhdFileIsTheFinalOne
        {
            get
            {
                return string.IsNullOrWhiteSpace(this.VhdFileDestinationFolder)
                    || this.VhdFileDestinationFolder.Equals(this.VhdFileTemporaryFolder, StringComparison.InvariantCultureIgnoreCase);
            }
        }

        /// <summary>
        /// Indicates if the final vhd file should be added to boot manager
        /// </summary>
        public bool AddVhdToBootManager { get; set; }

        /// <summary>
        /// indicates if auto reboot should be performed after successfull migration
        /// </summary>
        public bool AutoRebootAfterSuccess { get; set; }
    }
}
