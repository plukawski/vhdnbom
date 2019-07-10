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
//
//NOTE: this file is an improved version of StreamPump from DiscUtils project. The orignal one could enter into an infinite loop on certain disk configurations
using DiscUtils;
using System;
using System.IO;

namespace VHDNBOM.Logic.Helpers.Streams
{
    /// <summary>
    /// Improved utility class for pumping the contents of one stream into another. It will not crash or enter infinite loop if the original stream will read 0 bytes.
    /// </summary>
    /// <remarks>
    /// This class is aware of sparse streams, and will avoid copying data that is not
    /// valid in the source stream.  This functionality should normally only be used
    /// when the destination stream is known not to contain any existing data.
    /// </remarks>
    public sealed class ImprovedStreamPump
    {
        /// <summary>
        /// Initializes a new instance of the StreamPump class.
        /// </summary>
        public ImprovedStreamPump()
        {
            SparseChunkSize = 512;
            BufferSize = (int)(512 * 1024);
            SparseCopy = true;
        }

        /// <summary>
        /// Initializes a new instance of the ImprovedStreamPump class.
        /// </summary>
        /// <param name="inStream">The stream to read from.</param>
        /// <param name="outStream">The stream to write to.</param>
        /// <param name="sparseChunkSize">The size of each sparse chunk.</param>
        public ImprovedStreamPump(Stream inStream, Stream outStream, int sparseChunkSize)
        {
            InputStream = inStream;
            OutputStream = outStream;
            SparseChunkSize = sparseChunkSize;
            BufferSize = (int)(512 * 1024);
            SparseCopy = true;
        }

        /// <summary>
        /// Gets or sets the amount of data to read at a time from <c>InputStream</c>.
        /// </summary>
        public int BufferSize { get; set; }

        /// <summary>
        /// Gets the number of bytes read from <c>InputStream</c>.
        /// </summary>
        public long BytesRead { get; private set; }

        /// <summary>
        /// Gets the number of bytes written to <c>OutputStream</c>.
        /// </summary>
        public long BytesWritten { get; private set; }

        /// <summary>
        /// Gets or sets the stream that will be read from.
        /// </summary>
        public Stream InputStream { get; set; }

        /// <summary>
        /// Gets or sets the stream that will be written to.
        /// </summary>
        public Stream OutputStream { get; set; }

        /// <summary>
        /// Gets or sets, for sparse transfers, the size of each chunk.
        /// </summary>
        /// <remarks>
        /// A chunk is transfered if any byte in the chunk is valid, otherwise it is not.
        /// This value should normally be set to reflect the underlying storage granularity
        /// of <c>OutputStream</c>.
        /// </remarks>
        public int SparseChunkSize { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to enable the sparse copy behaviour (default true).
        /// </summary>
        public bool SparseCopy { get; set; }

        /// <summary>
        /// Event raised periodically through the pump operation.
        /// </summary>
        /// <remarks>
        /// This event is signalled synchronously, so to avoid slowing the pumping activity
        /// implementations should return quickly.
        /// </remarks>
        public event EventHandler<PumpProgressEventArgs> ProgressEvent;

        /// <summary>
        /// Performs the pump activity, blocking until complete.
        /// </summary>
        public void Run()
        {
            if (InputStream == null)
            {
                throw new InvalidOperationException("Input stream is null");
            }

            if (OutputStream == null)
            {
                throw new InvalidOperationException("Output stream is null");
            }

            if (!OutputStream.CanSeek)
            {
                throw new InvalidOperationException("Output stream does not support seek operations");
            }

            if (SparseChunkSize <= 1)
            {
                throw new InvalidOperationException("Chunk size is invalid");
            }

            if (SparseCopy)
            {
                RunSparse();
            }
            else
            {
                RunNonSparse();
            }
        }

        private static bool IsAllZeros(byte[] buffer, int offset, int count)
        {
            for (int j = 0; j < count; j++)
            {
                if (buffer[offset + j] != 0)
                {
                    return false;
                }
            }

            return true;
        }

        private void RunNonSparse()
        {
            byte[] copyBuffer = new byte[BufferSize];

            InputStream.Position = 0;
            OutputStream.Position = 0;

            int numRead = InputStream.Read(copyBuffer, 0, copyBuffer.Length);
            while (numRead > 0)
            {
                BytesRead += numRead;

                OutputStream.Write(copyBuffer, 0, numRead);
                BytesWritten += numRead;

                RaiseProgressEvent();

                numRead = InputStream.Read(copyBuffer, 0, copyBuffer.Length);
            }
        }

        private void RunSparse()
        {
            SparseStream inStream = InputStream as SparseStream;
            if (inStream == null)
            {
                inStream = SparseStream.FromStream(InputStream, Ownership.None);
            }

            if (BufferSize > SparseChunkSize && BufferSize % SparseChunkSize != 0)
            {
                throw new InvalidOperationException("Buffer size is not a multiple of the sparse chunk size");
            }

            byte[] copyBuffer = new byte[Math.Max(BufferSize, SparseChunkSize)];

            BytesRead = 0;
            BytesWritten = 0;

            foreach (StreamExtent extent in inStream.Extents)
            {
                inStream.Position = extent.Start;

                long extentOffset = 0;
                while (extentOffset < extent.Length)
                {
                    int numRead = (int)Math.Min(copyBuffer.Length, extent.Length - extentOffset);
                    bool readSuccess = ReadExact(inStream, copyBuffer, 0, numRead);
                    if (!readSuccess)
                    {
                        break;  //if we wasn't able to read this extent we should go to the next one
                    }
                    BytesRead += numRead;

                    int copyBufferOffset = 0;
                    for (int i = 0; i < numRead; i += SparseChunkSize)
                    {
                        if (IsAllZeros(copyBuffer, i, Math.Min(SparseChunkSize, numRead - i)))
                        {
                            if (copyBufferOffset < i)
                            {
                                OutputStream.Position = extent.Start + extentOffset + copyBufferOffset;
                                OutputStream.Write(copyBuffer, copyBufferOffset, i - copyBufferOffset);
                                BytesWritten += i - copyBufferOffset;
                            }

                            copyBufferOffset = i + SparseChunkSize;
                        }
                    }

                    if (copyBufferOffset < numRead)
                    {
                        OutputStream.Position = extent.Start + extentOffset + copyBufferOffset;
                        OutputStream.Write(copyBuffer, copyBufferOffset, numRead - copyBufferOffset);
                        BytesWritten += numRead - copyBufferOffset;
                    }

                    extentOffset += numRead;

                    RaiseProgressEvent();
                }
            }

            // Ensure the output stream is at least as long as the input stream.  This uses
            // read/write, rather than SetLength, to avoid failing on streams that can't be
            // explicitly resized.  Side-effect of this, is that if outStream is an NTFS
            // file stream, then actual clusters will be allocated out to at least the
            // length of the input stream.
            if (OutputStream.Length < inStream.Length)
            {
                inStream.Position = inStream.Length - 1;
                int b = inStream.ReadByte();
                if (b >= 0)
                {
                    OutputStream.Position = inStream.Length - 1;
                    OutputStream.WriteByte((byte)b);
                }
            }
        }

        private void RaiseProgressEvent()
        {
            // Raise the event by using the () operator.
            if (ProgressEvent != null)
            {
                PumpProgressEventArgs args = new PumpProgressEventArgs();
                args.BytesRead = BytesRead;
                args.BytesWritten = BytesWritten;
                args.SourcePosition = InputStream.Position;
                args.DestinationPosition = OutputStream.Position;
                ProgressEvent(this, args);
            }
        }

        /// <summary>
        /// Read bytes until buffer filled or throw EndOfStreamException.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="buffer">The buffer to populate.</param>
        /// <param name="offset">Offset in the buffer to start.</param>
        /// <param name="count">The number of bytes to read.</param>
        private bool ReadExact(Stream stream, byte[] buffer, int offset, int count)
        {
            int originalCount = count;

            while (count > 0)
            {
                int numRead = stream.Read(buffer, offset, count);

                if (numRead == 0)
                {
                    return false;   //an end of stream was reached or the stream cannot be read in this position
                }

                offset += numRead;
                count -= numRead;
            }
            return true;
        }
    }
}
