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

using Autofac;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using VHDNBOM.Logic.MigrationSteps;
using VHDNBOM.Logic.Models;

namespace VHDNBOM.Logic
{
    public class MigratorEngine
    {
        private readonly ILogger<MigratorEngine> logger;
        private readonly IContainer container;
        private List<BaseMigrationStep> stepsToExecute;
        private MigrationFlowData migrationData;

        public MigratorEngine(ILogger<MigratorEngine> logger, MigrationFlowData migrationData)
        {
            this.logger = logger;
            this.migrationData = migrationData;
            this.container = DependencyResolverAccessor.Container;
            if (this.container == null)
            {
                logger.LogCritical("Unable to access dependency injection container. Process cannot continue");
                throw new Exception("Unable to access dependency injection container.");
            }

            //resolve all migration steps in proper order
            stepsToExecute = new List<BaseMigrationStep>()
            {
                container.Resolve<PrepareMigrationStep>(),
                container.Resolve<CreateTemporaryVhdMigrationStep>(),
                container.Resolve<CloneOSMigrationStep>(),
                container.Resolve<FixClonedOsRegistryMigrationStep>(),
                container.Resolve<ShrinkVhdVolumeMigrationStep>(),
                container.Resolve<CreateFinalVhdMigrationStep>(),
                container.Resolve<CloneTempVhdToFinalVhdMigrationStep>(),
                container.Resolve<AddFinalVhdToBootManagerMigrationStep>(),
            };
        }

        public bool CheckPrerequisities(out string[] messages)
        {
            logger.LogInformation("Checking prerequisities...");
            List<string> messagesFromSteps = new List<string>();
            bool result = true;

            stepsToExecute.ForEach((step) =>
            {
                logger.LogDebug($"Checking prerequisities of {step.GetType().Name}");
                string[] stepMessages = null;
                bool stepResult = step.CheckPrerequisities(ref stepMessages);

                result = result && stepResult;
                messagesFromSteps.AddRange(stepMessages);
            });

            messages = messagesFromSteps.ToArray();

            return result;
        }

        public void PerformMigration()
        {
            logger.LogInformation("Performing migration...");

            stepsToExecute.ForEach((step) =>
            {
                logger.LogDebug($"Executing migration step {step.GetType().Name}");
                step.PerformStep();
            });
        }
    }
}
