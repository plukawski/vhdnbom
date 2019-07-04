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

using DiscUtils;
using DiscUtils.Partitions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VHDNBOM.Logic.Helpers.Decorators
{
    public class VirtualDiskDecorator : IDisposable
    {
        private readonly VirtualDisk virtualDisk;

        public virtual BiosPartitionTableDecorator Partitions { get; set; }
        public virtual Geometry Geometry => this.virtualDisk.Geometry;

        /// <summary>
        /// Used for mocking
        /// </summary>
        protected VirtualDiskDecorator()
        {

        }

        public VirtualDiskDecorator(VirtualDisk virtualDisk)
        {
            this.virtualDisk = virtualDisk;
            if (this.virtualDisk == null)
            {
                throw new ArgumentNullException("Virtual disk cannot be null");
            }

            this.Partitions = new BiosPartitionTableDecorator(this.virtualDisk.Partitions as BiosPartitionTable);
        }

        public void Dispose()
        {
            this.virtualDisk?.Dispose();
        }
    }
}
