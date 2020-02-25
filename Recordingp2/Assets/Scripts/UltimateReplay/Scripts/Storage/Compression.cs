using System.IO;
using System.IO.Compression;

namespace UltimateReplay.Storage
{
    /// <summary>
    /// The amount of compression to apply to a data stream.
    /// </summary>
    public enum CompressionLevel
    {
        /// <summary>
        /// No compression is applied and all data is left unchanged.
        /// </summary>
        None = 0,
        /// <summary>
        /// All data is compressed to the optimal level.
        /// </summary>
        Optimal,
    }
    
    /// <summary>
    /// Compression utility using the GZip compression algorithm.
    /// </summary>
    public static class Compression
    {
        // Private
        private static readonly int decompressBufferSize = (4 * 1024); // 4kb
        private static byte[] decompressBuffer = null;

        // Methods
        /// <summary>
        /// Compress a data stream using the GZip compression algorithm.
        /// </summary>
        /// <param name="data">The input data to compress</param>
        /// <param name="level">The target compression level to use</param>
        /// <returns>The compressed data</returns>
        public static byte[] CompressData(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Check for no compression
            if (level == CompressionLevel.None)
                return data;

            // Create a memory stream to manage the array
            using (MemoryStream stream = new MemoryStream())
            {
                // Create a gzip stream
                using (GZipStream compressStrem = new GZipStream(stream, CompressionMode.Compress))
                {
                    // Write the data for compression
                    compressStrem.Write(data, 0, data.Length);
                }

                // Get the compressed bytes
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Decompress a data stream using the GZip compression algorithm.
        /// </summary>
        /// <param name="data">The input data to decompress</param>
        /// <param name="level">The target compression level to use</param>
        /// <returns>The decompressed data</returns>
        public static byte[] DecompressData(byte[] data, CompressionLevel level = CompressionLevel.Optimal)
        {
            // Check for no compression
            if (level == CompressionLevel.None)
                return data;

            // Create our decompress buffer if it has not been allocated
            if (decompressBuffer == null)
                decompressBuffer = new byte[decompressBufferSize];

            // Create a memory stream to manage the array
            using (MemoryStream stream = new MemoryStream(data))
            {
                using (MemoryStream outputStream = new MemoryStream())
                {
                    // Create a gzip stream
                    using (GZipStream decompressStream = new GZipStream(stream, CompressionMode.Decompress))
                    {
                        int readSize = 0;

                        // Read into our buffer while there is data left
                        while ((readSize = decompressStream.Read(decompressBuffer, 0, decompressBufferSize)) > 0)
                        {
                            // Write the data to our output stream
                            outputStream.Write(decompressBuffer, 0, readSize);
                        }
                    }

                    // Get the stream bytes
                    return outputStream.ToArray();
                }
            }
        }
    }
}
