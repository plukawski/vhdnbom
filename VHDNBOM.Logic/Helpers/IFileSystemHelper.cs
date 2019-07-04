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
using System.IO;
using DiscUtils;
using System.IO.Abstractions;
using VHDNBOM.Logic.Helpers.Decorators;

namespace VHDNBOM.Logic.Helpers
{
    public interface IFileSystemHelper
    {
        char GetFirstAvailableDriveLetter();
        IDriveInfo GetSystemDriveInfo();
        string FindBestLocationForVhdTemporaryFolder();
        void ExecuteDiskpart(string diskpartScriptFile, ILogger logger);
        bool CreateSymbolicLinkToDirectory(string symbolicLinkLocation, string sourceForTheLink);
        void CreateSparseFile(string fullName, long fileSize);
        string GetJunctionTargetPath(string junctionFullName);
        void CloneNtfsFileSystem(Stream sourceFileSystemToClone, Stream destinationLocationStream, ILogger logger);
        string ExecuteBcdeditCommand(string bcdeditArguments, ILogger logger);
        Stream OpenRawDiskStream(string diskPath);
        VirtualDiskDecorator OpenVhdx(string vhdxFilePath);
        DiscFileSystem OpenNtfsFileSystem(Stream rawPartitionStream);
        void ChangeSystemDriveMappingFromRegistry(Stream registryFileStream);
    }
}