using System;
using System.IO;

namespace UltimateReplay.Storage
{
    /// <summary>
    /// The file stream mode used when opening a file stream.
    /// </summary>
    public enum ReplayFileStreamMode
    {
        /// <summary>
        /// Open the stream using a read only stream.
        /// </summary>
        ReadOnly,
        /// <summary>
        /// Open the stream using a write only stream.
        /// </summary>
        WriteOnly,
    }

    /// <summary>
    /// Represents a file stream that is in an opened state that can either be written to or read from exclusivley.
    /// The stream provides access to both <see cref="BinaryWriter"/> and <see cref="BinaryReader"/> objects for reading from and writing to the stream.
    /// Note that only one writer object will be valid at any time because combined reading and writing is not supported.
    /// Implements <see cref="IDisposable"/>. Call <see cref="Dispose"/> to close any open streams.  
    /// </summary>
    public sealed class ReplayFileStream : IDisposable
    {
        // Private
        private Stream stream = null;
        private BinaryWriter writer = null;
        private BinaryReader reader = null;

        // Properties
        /// <summary>
        /// Returns true if the stream is currently open for reading.
        /// </summary>
        public bool IsReading
        {
            get { return stream.CanRead; }
        }

        /// <summary>
        /// Returns true if the stream is currently open for writing.
        /// </summary>
        public bool IsWriting
        {
            get { return stream.CanWrite; }
        }

        /// <summary>
        /// Get the <see cref="BinaryWriter"/> for the writable stream.
        /// If the stream is not currently writable then this value will be null.
        /// </summary>
        public BinaryWriter Writer
        {
            get
            {
                // Check if writing is enabled
                if (IsWriting == false)
                    throw new InvalidOperationException("Failed to write to file stream. The stream is not writable in its current state");

                return writer;
            }
        }

        /// <summary>
        /// Get the <see cref="BinaryReader"/> for the readable stream. 
        /// If the stream is not currently readable then this value will be null.
        /// </summary>
        public BinaryReader Reader
        {
            get
            {
                // Check if reading is enabled
                if (IsReading == false)
                    throw new InvalidOperationException("Failed to read from file stream. The stream is not readable in its current state");

                return reader;
            }
        }

        /// <summary>
        /// Get the read position of the file stream.
        /// </summary>
        public int Position
        {
            get
            {
                // Check for already disposed
                DisposeCheck();

                // Get position
                return (int)stream.Position;
            }
        }

        /// <summary>
        /// Check if this stream has already been disposed.
        /// </summary>
        public bool IsDisposed
        {
            get
            {
                try
                {
                    DisposeCheck();
                    return false;
                }
                catch (ObjectDisposedException) { }

                // Is disposed
                return true;
            }
        }

        // Constructor
        /// <summary>
        /// Create a new <see cref="ReplayFileStream"/> for the specified filepath. 
        /// </summary>
        /// <param name="filepath">The filepath of the file to open or create</param>
        /// <param name="mode">The <see cref="ReplayFileStreamMode"/> to open the stream with</param>
        /// <param name="hiddenFile">Should the stream create a hidden file if we are in writing mode</param>
        public ReplayFileStream(string filepath, ReplayFileStreamMode mode, bool hiddenFile = false)
        {
            if (mode == ReplayFileStreamMode.ReadOnly)
            {
                // Open for reading only
                this.stream = File.OpenRead(filepath);
            }
            else if (mode == ReplayFileStreamMode.WriteOnly)
            {
                // Open for writing only
                this.stream = File.OpenWrite(filepath);

                // Make the file hidden
                if (hiddenFile == true)
                    File.SetAttributes(filepath, File.GetAttributes(filepath) | FileAttributes.Hidden);
            }
            
            // Create the reader
            if (IsReading == true)
                this.reader = new BinaryReader(this.stream);

            // Create the writer
            if (IsWriting == true)
                this.writer = new BinaryWriter(this.stream);
        }

        // Methods
        /// <summary>
        /// Attempts to copy th contents of this <see cref="ReplayFileStream"/> to another stream.
        /// All content will be block copied as chunks until completed.
        /// </summary>
        /// <param name="other">The other <see cref="ReplayFileStream"/> to copy the data to</param>
        public void CopyTo(ReplayFileStream other)
        {
            // Check for already disposed
            DisposeCheck();

            // Check for writable
            if (other.IsWriting == false)
                throw new IOException("Expected writable stream. Target is not writable!");

            byte[] buffer = new byte[4096];
            int readSize = 0;

            while ((readSize = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                // Write to final stream
                other.stream.Write(buffer, 0, readSize);
            }
        }

        /// <summary>
        /// Write the specified bytes to the stream.
        /// </summary>
        /// <param name="bytes">The byte array to write</param>
        public void Write(byte[] bytes)
        {
            // Check for already disposed
            DisposeCheck();

            // Write all bytes
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Read a number of bytes from the stream.
        /// </summary>
        /// <param name="size">The amount of bytes to read</param>
        /// <returns>A byte array of specified size containing read data</returns>
        public byte[] Read(int size)
        {
            // Check for already disposed
            DisposeCheck();

            byte[] buffer = new byte[size];
            int read = 0;

            // Read into buffer
            while ((read += stream.Read(buffer, read, size)) < size) ;
            
            // Get the buffer
            return buffer;
        }

        /// <summary>
        /// Seek the file stream to the specified offset.
        /// </summary>
        /// <param name="offset">The file offset to seek to</param>
        /// <param name="origin">The seek origin to offset from</param>
        public void Seek(long offset, SeekOrigin origin)
        {
            // Check for already disposed
            DisposeCheck();

            // Seek stream
            stream.Seek(offset, origin);
        }

        /// <summary>
        /// Clear all contents in the file stream.
        /// </summary>
        public void Clear()
        {
            // Check for already disposed
            DisposeCheck();

            // Check if we are writing
            if (IsWriting == true)
            {
                // Set size to 0 - delete all data{
                stream.SetLength(0);
            }
        }

        /// <summary>
        /// Dispose of the file stream causing any open files to be closed.
        /// </summary>
        public void Dispose()
        {
            // Make sure we have not already disposed
            if (IsDisposed == false)
            {
                // Close reader
                if (IsReading == true)
                    reader.Close();

                // Close writer
                if (IsWriting == true)
                    writer.Close();

                // Dispose of stream
                stream.Dispose();
                stream = null;
            }
        }

        private void DisposeCheck()
        {
            // Check for already disposed
            if (stream == null)
                throw new ObjectDisposedException(GetType().Name);
        }
    }
}