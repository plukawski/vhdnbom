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

using DiscUtils.Partitions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace VHDNBOM.Logic.Helpers.Decorators
{
    public class BiosPartitionTableDecorator
    {
        private readonly BiosPartitionTable partitionTable;

        public virtual List<PartitionInfoDecorator> Partitions { get; set; }

        public PartitionInfoDecorator this[int index] => this.Partitions[index];

        /// <summary>
        /// Used for mocking
        /// </summary>
        protected BiosPartitionTableDecorator()
        {

        }

        public BiosPartitionTableDecorator(BiosPartitionTable partitionTable)
        {
            this.partitionTable = partitionTable;
            if (this.partitionTable == null)
            {
                throw new ArgumentNullException("Partition table cannot be null");
            }

            this.Partitions = this.partitionTable.Partitions.Select(x => new PartitionInfoDecorator(x)).ToList();
        }

        public virtual int CreatePrimaryBySector(long first, long last, byte type, bool markActive)
        {
            int partIndex = this.partitionTable.CreatePrimaryBySector(first, last, type, markActive);

            //refresh partitions collection
            this.Partitions = this.partitionTable.Partitions.Select(x => new PartitionInfoDecorator(x)).ToList();

            return partIndex;
        }
    }
}
