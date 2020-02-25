
using System;
using System.Collections.Generic;

namespace UltimateReplay.Storage
{
    public enum ReplayFileEnumReleaseMode
    {
        ChunksBefore,
        ChunksAfter,
    }

    public class ReplayFileBuffer
    {
        // Private
        private HashSet<ReplayFileChunk> loadedChunks = new HashSet<ReplayFileChunk>();
        private Queue<ReplayFileChunk> removeQueue = new Queue<ReplayFileChunk>();

        // Methods
        public void StoreChunk(ReplayFileChunk chunk)
        {
            // Cache the chunk
            if (loadedChunks.Contains(chunk) == false)
                loadedChunks.Add(chunk);
        }

        public bool HasLoadedChunk(int chunkID)
        {
            foreach(ReplayFileChunk chunk in loadedChunks)
            {
                if (chunk.chunkID == chunkID)
                    return true;
            }

            return false;
        }

        public bool HasLoadedChunk(float timeStamp)
        {
            foreach(ReplayFileChunk chunk in loadedChunks)
            {
                if (timeStamp >= chunk.ChunkStartTime && timeStamp <= chunk.ChunkEndTime)
                    return true;
            }

            return false;
        }

        public ReplayFileChunk GetLoadedChunk(float timeStamp)
        {
            // Check all chunks
            foreach(ReplayFileChunk chunk in loadedChunks)
            {
                // Check if restore is successful
                if(chunk.Restore(timeStamp) != null)
                {
                    // We have found the chunk
                    return chunk;
                }
            }

            // No loaded chunk found
            return null;
        }

        public void ReleaseAllChunks()
        {
            // Clear data sets
            loadedChunks.Clear();
            removeQueue.Clear();
        }

        public void ReleaseOldChunks(float currentTimeStamp, ReplayFileEnumReleaseMode mode)
        {
            switch(mode)
            {
                case ReplayFileEnumReleaseMode.ChunksBefore:
                    {
                        // Process all chunks
                        foreach(ReplayFileChunk chunk in loadedChunks)
                        {
                            // Check if the chunk ends before the current time stamp
                            if(chunk.ChunkEndTime < currentTimeStamp)
                            {
                                // We should remove the chunk soon
                                removeQueue.Enqueue(chunk);
                            }
                        }

                        break;
                    }

                case ReplayFileEnumReleaseMode.ChunksAfter:
                    {
                        // Process all chunks
                        foreach(ReplayFileChunk chunk in loadedChunks)
                        {
                            // Check if the chunk starts before the current time stamp
                            if(chunk.ChunkStartTime > currentTimeStamp)
                            {
                                // We should remove the chunk soon
                                removeQueue.Enqueue(chunk);
                            }
                        }

                        break;
                    }
            }

            // Remove old chunks
            while(removeQueue.Count > 0)
            {
                // Get the ext chunk
                ReplayFileChunk current = removeQueue.Dequeue();

                // Remove the chunk from loaded chunks
                if (loadedChunks.Contains(current) == true)
                    loadedChunks.Remove(current);
            }
        }
    }
}