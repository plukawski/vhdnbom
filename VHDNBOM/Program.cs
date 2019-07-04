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

using Microsoft.Extensions.DependencyInjection;
using Autofac;
using System;
using System.Linq;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using VHDNBOM.Logic;
using VHDNBOM.Logic.Helpers;
using VHDNBOM.Logic.Models;
using System.IO.Abstractions;
using VHDNBOM.Enums;

namespace VHDNBOM
{
    class Program
    {
        class LaunchParameters
        {
            public const string Mode = "-mode";
            public const string AddToBootManager = "-addToBootManager";
            public const string Quiet = "-quiet";
            public const string ImageTemporaryFolderPath = "-vhdTempFolderPath";
        }

        private static bool quiet = false;
        private static bool addToBootManager = false;
        private static OperationModeEnum operationMode = OperationModeEnum.MigrateCurrentOsToVhd;
        private static string vhdTemporaryFolderPath = null;

        static void Main(string[] args)
        {
            ContainerBuilder builder = new ContainerBuilder();
            ConfigureContainer(builder);

            IContainer container = builder.Build();
            IFileSystemHelper fileSystemHelper = container.Resolve<IFileSystemHelper>();

            ILogger<Program> logger = container.Resolve<ILogger<Program>>();
            Console.WriteLine("**********************************************************");
            Console.WriteLine("*   Vhd Native Boot Os Migrator by Przemysław Łukawski   *");
            Console.WriteLine("**********************************************************");
            logger.LogDebug("Vhd Native Boot Os Migrator has started.");

            DisplayUsage();
            ParseArguments(args);

            MigrationFlowData migrationData = BuildMigrationData(operationMode, fileSystemHelper, addToBootManager);
            DisplaySummary(migrationData, logger);

            if (!quiet)
            {
                Console.Write("Are the above information ok (Y/N)?");
                ConsoleKeyInfo answer = Console.ReadKey();
                if (answer.Key != ConsoleKey.Y)
                {
                    return;
                }
                Console.WriteLine();
            }

            container.Dispose();
            builder = new ContainerBuilder();
            ConfigureContainer(builder);
            builder.RegisterInstance(migrationData);    //register migrationDate
            container = builder.Build();    //rebuild container to contain migrationData
            DependencyResolverAccessor.Container = container;


            MigratorEngine engine = container.Resolve<MigratorEngine>();
            string[] messages;
            bool checkResult = engine.CheckPrerequisities(out messages);
            if (checkResult)
            {
                engine.PerformMigration();
            }
            else
            {
                Console.WriteLine("Cannot continue because of the following checks failed:");
                foreach (string message in messages)
                {
                    Console.WriteLine(message);
                }
            }

            if (!quiet)
            {
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
            }
        }

        private static ContainerBuilder ConfigureContainer(ContainerBuilder builder)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddAutofac();
            services.AddLogging(loggingBuilder =>
            {
                // configure Logging with NLog
                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(LogLevel.Trace);
                loggingBuilder.AddNLog();
            });


            builder.Populate(services);
            builder.RegisterModule<LogicModule>();
            return builder;
        }

        private static MigrationFlowData BuildMigrationData(OperationModeEnum operationMode, IFileSystemHelper fileSystemHelper, bool addToBootManager)
        {
            MigrationFlowData migrationData = new MigrationFlowData();
            IDriveInfo systemDriveInfo = fileSystemHelper.GetSystemDriveInfo();
            migrationData.OperatingSystemDriveLetter = systemDriveInfo.Name.First();
            migrationData.TemporaryVhdFileMaxSize = systemDriveInfo.TotalSize + (200 << 20)/*200MB*/;
            migrationData.VhdFileName = "VHDNBOM_System_Image.vhdx";
            migrationData.VhdFileType = Logic.Models.Enums.VhdType.VhdxDynamic;
            migrationData.VhdFileTemporaryFolder = vhdTemporaryFolderPath ?? fileSystemHelper.FindBestLocationForVhdTemporaryFolder();
            migrationData.AddVhdToBootManager = addToBootManager;

            if (operationMode == OperationModeEnum.MigrateCurrentOsToVhd)
            {
                migrationData.DeleteTemporaryVhdFile = true;
                migrationData.DestinationVhdMaxFileSize = (systemDriveInfo.AvailableFreeSpace - Constants.FiveGigs + Constants.OneGig) + (200 << 20);
                migrationData.DesiredTempVhdShrinkSize = (systemDriveInfo.TotalSize - (systemDriveInfo.AvailableFreeSpace - Constants.FiveGigs));
                migrationData.VhdFileDestinationFolder = $"{migrationData.OperatingSystemDriveLetter}:\\VHD_Boot";
            }

            return migrationData;
        }

        private static void DisplaySummary(MigrationFlowData migrationData, ILogger logger)
        {
            logger.LogInformation($"Configuration that will be used for migration:");
            if (migrationData.TemporaryVhdFileIsTheFinalOne)
            {
                logger.LogInformation($"Destination image folder: {migrationData.VhdFileTemporaryFolder}");
            }
            else
            {
                logger.LogInformation($"Temporary image folder: {migrationData.VhdFileTemporaryFolder}");
                logger.LogInformation($"Destination image folder: {migrationData.VhdFileDestinationFolder}");
                logger.LogInformation($"Temporary image will be deleted after completion: {migrationData.DeleteTemporaryVhdFile}");
            }
            logger.LogInformation($"Image file name: {migrationData.VhdFileName}");
            logger.LogInformation($"Image will be added to boot manager: {migrationData.AddVhdToBootManager}");
        }

        private static void DisplayUsage()
        {
            Console.WriteLine("Usage (all parameters are optional):");
            Console.WriteLine("VHDNBOM.exe -mode {MigrateCurrentOsToVhd,CreateCurrentOsVhdOnly} -addToBootManager -quiet -vhdTempFolderPath");
            Console.WriteLine("NOTE: When no parameters are specified the migratore will launch in MigrateCurrentOsToVhd mode and will NOT add the image to boot manager.");
            Console.WriteLine();
        }

        private static void ParseArguments(string[] args)
        {
            if (args.Length > 0)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    string currentParameter = args[i];
                    if (currentParameter == LaunchParameters.Mode && (i + 1) < args.Length)
                    {
                        if (Enum.TryParse<OperationModeEnum>(args[i + 1], out OperationModeEnum mode))
                        {
                            operationMode = mode;
                        }
                    }

                    if (currentParameter == LaunchParameters.AddToBootManager)
                    {
                        addToBootManager = true;
                    }

                    if (currentParameter == LaunchParameters.Quiet)
                    {
                        quiet = true;
                    }

                    if (currentParameter == LaunchParameters.ImageTemporaryFolderPath && (i + 1) < args.Length)
                    {
                        vhdTemporaryFolderPath = args[i + 1];
                    }
                }
            }
        }
    }
}
