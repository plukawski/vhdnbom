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

namespace VHDNBOM.Logic.Helpers.Decorators
{
    public class PartitionInfoDecorator
    {
        private readonly PartitionInfo partitionInfo;

        public virtual long SectorCount => this.partitionInfo.SectorCount;

        /// <summary>
        /// Used for mocking
        /// </summary>
        protected PartitionInfoDecorator()
        {

        }

        public PartitionInfoDecorator(PartitionInfo partitionInfo)
        {
            this.partitionInfo = partitionInfo;
            if (this.partitionInfo == null)
            {
                throw new ArgumentNullException("partitionInfo cannot be null.");
            }
        }

        public virtual SparseStream Open()
        {
            return this.partitionInfo.Open();
        }
    }
}
