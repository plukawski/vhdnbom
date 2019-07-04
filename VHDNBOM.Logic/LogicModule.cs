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

using Alphaleonis.Win32.Vss;
using Autofac;
using System;
using System.IO.Abstractions;
using VHDNBOM.Logic.Helpers;

namespace VHDNBOM.Logic
{
    public class LogicModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<MigratorEngine>();
            builder.RegisterAssemblyTypes(AppDomain.CurrentDomain.GetAssemblies())
                .Where(t => t.Name.EndsWith("MigrationStep"))
                .InstancePerDependency();

            builder.RegisterType<FileSystemHelper>().AsImplementedInterfaces();
            builder.RegisterType<FileCopyHelper>().AsImplementedInterfaces().InstancePerDependency();
            builder.RegisterType<OperatingSystemHelper>().AsImplementedInterfaces();
            builder.RegisterType<FileSystem>().AsImplementedInterfaces();
            IVssImplementation vss = VssUtils.LoadImplementation();
            builder.RegisterInstance(vss).AsImplementedInterfaces();
        }
    }
}
