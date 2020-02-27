using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UltimateReplay.Core;

namespace UltimateReplay.Storage
{
    /// <summary>
    /// The main file header for replay files.
    /// Contains information about the replay data stored within the file.
    /// </summary>
    public struct ReplayFileHeader
    {
        // Public
        /// <summary>
        /// Replay file identifier used to ensure that the specified file is actually a replay file.
        /// </summary>
        public const int replayIdentifier = 0x2D42;

        /// <summary>
        /// The amount of bytes that the header takes up in the file.
        /// </summary>
        public int headerSize;
        /// <summary>
        /// The amount of size in bytes that the recording requires.
        /// </summary>
        public int memorySize;
        /// <summary>
        /// The file offset for the main replay data.
        /// </summary>
        public int dataOffset;
        /// <summary>
        /// The negative file offset of the chunk table.
        /// The chunk table is stored at the very end of the file for performance and as a result the offset is from the end of the file.
        /// </summary>
        public int chunkTableOffset;
        /// <summary>
        /// The state buffer is stored after the chunk table and contains initial state information for all dynamic replay objects.
        /// </summary>
        public int stateBufferOffset;
        /// <summary>
        /// The amount of time in seconds that the recording lasts.
        /// </summary>
        public float duration;
        /// <summary>
        /// The name of the scene that should be loaded in order for the replay to playback correctly.
        /// </summary>
        public string sceneName;

        // Constructor
        /// <summary>
        /// Create a replay header and assign the specified scene name.
        /// </summary>
        /// <param name="sceneName"></param>
        public ReplayFileHeader(string sceneName)
        {
            this.headerSize = 0;
            this.memorySize = 0;
            this.dataOffset = 0;
            this.chunkTableOffset = 0;
            this.stateBufferOffset = 0;
            this.duration = 0;
            this.sceneName = sceneName;
        }
        
        // Methods
        /// <summary>
        /// Called by the file streamer when the file header should be written to the specified binary stream.
        /// </summary>
        /// <param name="writer">A binary writer used to store the data</param>
        public void OnReplayDataSerialize(BinaryWriter writer)
        {
            // Write identifier
            writer.Write(replayIdentifier);

            // Write header data
            writer.Write(headerSize);
            writer.Write(memorySize);
            writer.Write(dataOffset);
            writer.Write(chunkTableOffset);
            writer.Write(stateBufferOffset);
            writer.Write(duration);
            writer.Write(sceneName);
        }

        /// <summary>
        /// Called by the file streamer when the file header should be read from the specified binary stream.
        /// </summary>
        /// <param name="reader">A binary reader to read the data from</param>
        public void OnReplayDataDeserialize(BinaryReader reader)
        {
            int identifier = reader.ReadInt32();

            // Check for the file identifier
            if (replayIdentifier != identifier)
                throw new FormatException("The specified file target is not a valid UltimateReplay file");

            // Read header data
            headerSize = reader.ReadInt32();
            memorySize = reader.ReadInt32();
            dataOffset = reader.ReadInt32();
            chunkTableOffset = reader.ReadInt32();
            stateBufferOffset = reader.ReadInt32();
            duration = reader.ReadSingle();
            sceneName = reader.ReadString();
        }
    }

    public struct ReplayFileChunkFetchData
    {
        public bool isIDBased;
        public int chunkID;
        public float chunkTimeStamp;

        // Constructor
        public ReplayFileChunkFetchData(int chunkID)
        {
            this.isIDBased = true;
            this.chunkID = chunkID;
            this.chunkTimeStamp = -1;
        }

        public ReplayFileChunkFetchData(float chunkTimeStamp)
        {
            this.isIDBased = false;
            this.chunkTimeStamp = chunkTimeStamp;
            this.chunkID = -1;
        }
    }

    /// <summary>
    /// Represents a chunk entry in the <see cref="ReplayFileChunkTable"/>.
    /// </summary>
    public struct ReplayFileChunkTableEntry
    {
        // Public
        /// <summary>
        /// The unique id for the current chunk.
        /// </summary>
        public int chunkID;

        /// <summary>
        /// The start time in seconds for the current chunk.
        /// </summary>
        public float startTimeStamp;
        /// <summary>
        /// The end time in seconds of the current chunk.
        /// </summary>
        public float endTimeStamp;
        /// <summary>
        /// The 32 bit file pointer representing the byte offset that the chunk data is located at.
        /// </summary>
        public int filePointer;
    }

    /// <summary>
    /// A chunk table is a quick reference lookup table stored near the start of a replay file and specifys the location of varouis replay chunks using file offsets.
    /// This is used as a quick seek table for chunk jumping during playback. 
    /// Playback may pause slightly when seeking to random locations in the recording as the required chunk information is fetched (Buffering).
    /// </summary>
    public class ReplayFileChunkTable : HashSet<ReplayFileChunkTableEntry>, IReplayDataSerialize
    {
        // Methods
        /// <summary>
        /// Add a new chunk reference to the chunk table.
        /// </summary>
        /// <param name="startTimeStamp">The start time in seconds for the chunk</param>
        /// <param name="endTimeStamp">The end time in seconds for the chunk</param>
        /// <param name="filePointer">The 32 bit file pointer for the chunk</param>
        public void CreateEntry(int chunkID, float startTimeStamp, float endTimeStamp, int filePointer)
        {
            Add(new ReplayFileChunkTableEntry
            {
                chunkID = chunkID,
                startTimeStamp = startTimeStamp,
                endTimeStamp = endTimeStamp,
                filePointer = filePointer,
            });
        }

        /// <summary>
        /// Attempts to find the chunk pointer for the replay chunk with the matching chunk id.
        /// </summary>
        /// <param name="chunkID">The id of the chunk to get the file pointer for</param>
        /// <returns>A 32 bit file offset or -1 if the timestamp is not found in the recording</returns>
        public int GetPointerForChunk(int chunkID)
        {
            foreach (ReplayFileChunkTableEntry entry in this)
            {
                // Check for matching ids
                if (chunkID == entry.chunkID)
                {
                    // Get the file pointer for the entry
                    return entry.filePointer;
                }
            }

            // Chunk not found
            return -1;
        }

        /// <summary>
        /// Attempts to find the chunk pointer for the replay chunk that best matches the specified time stamp.
        /// If there are no chunks that contian the specified time stamp then the return value will be -1.
        /// </summary>
        /// <param name="timeStamp">The timestamp to find the chunk offset pointer for</param>
        /// <returns>A 32 bit file offset or -1 if the timestamp is not found in the recording</returns>
        public int GetPointerForTimeStamp(float timeStamp)
        {
            // Negative time stamps are not alowed
            if (timeStamp < 0)
                return -1;

            foreach(ReplayFileChunkTableEntry entry in this)
            {
                if(timeStamp >= entry.startTimeStamp && 
                    timeStamp <= entry.endTimeStamp)
                {
                    // Get the file pointer for the entry
                    return entry.filePointer;
                }
            }

            // We have not found an easy match yet so we need to do a bit more work
            // Due to the recording inverval, it is possible that a time stamp lies between chunks.
            // If this is the case then we need to select the chunk that the time stamp is closest to for smoothest playback.
            int index = 0;
            int size = Count;

            bool foundBestMatch = false;
            float bestMatchDifference = float.MaxValue;
            ReplayFileChunkTableEntry bestMatch = new ReplayFileChunkTableEntry();

            foreach (ReplayFileChunkTableEntry entry in this)
            {
                // Make sure the time stamp is not past the end of the recording
                if (index == (size - 1))
                    break;

                // Check for closest entry
                if(timeStamp < entry.startTimeStamp)
                {
                    // Find the timestamp difference
                    float difference = entry.startTimeStamp - timeStamp;

                    // Check for smallest difference
                    if(difference < bestMatchDifference)
                    {
                        // We have found a new best match
                        foundBestMatch = true;
                        bestMatchDifference = difference;
                        bestMatch = entry;
                    }
                }
                else if(timeStamp > entry.endTimeStamp)
                {
                    // Find the timestamp difference
                    float difference = timeStamp - entry.endTimeStamp;

                    // Check for smallest difference
                    if(difference < bestMatchDifference)
                    {
                        // We have found a new best match
                        foundBestMatch = true;
                        bestMatchDifference = difference;
                        bestMatch = entry;
                    }
                }

                // Increase index
                index++;
            }

            // Check for best match
            if (foundBestMatch == true)
                return bestMatch.filePointer;

            // No entry found
            return -1;
        }

        /// <summary>
        /// Called by the file streamer when the chunk table should be serialized to the specified stream.
        /// </summary>
        /// <param name="writer"></param>
        public void OnReplayDataSerialize(BinaryWriter writer)
        {
            // Write the size
            writer.Write(Count);

            // Write the chunk table
            foreach (ReplayFileChunkTableEntry entry in this)
            {
                // Write the table item
                writer.Write(entry.chunkID);
                writer.Write(entry.startTimeStamp);
                writer.Write(entry.endTimeStamp);
                writer.Write(entry.filePointer);
            }
        }

        /// <summary>
        /// Called by the file streamer when the chunk table should be deserialized from the specified stream.
        /// </summary>
        /// <param name="reader"></param>
        public void OnReplayDataDeserialize(BinaryReader reader)
        {
            // Read the size
            int size = reader.ReadInt32();

            // Read each chunk item
            for (int i = 0; i < size; i++)
            {
                int id = reader.ReadInt32();
                float start = reader.ReadSingle();
                float end = reader.ReadSingle();
                int pointer = reader.ReadInt32();

                // Add the item
                Add(new ReplayFileChunkTableEntry
                {
                    chunkID = id,
                    startTimeStamp = start,
                    endTimeStamp = end,
                    filePointer = pointer,
                });
            }
        }
    }

    /// <summary>
    /// A chunk is a container for multiple replay states that may or may not be compressed in order to reduce file size.
    /// Chunks are used to best optimize performance during playback and filesize.
    /// </summary>
    public class ReplayFileChunk : List<ReplaySnapshot>, IEquatable<ReplayFileChunk>
    {
        // Public
        /// <summary>
        /// Due to the way chunks are stored, it is possible to attmempt to load a snapshot that lies inbetween 2 chunks.
        /// </summary>
        public const float chunkOverlapThreshold = 1f;

        /// <summary>
        /// The unique chunk id associated with this chunk.
        /// This identifier describes the location of the chunk in the overall playback sequence.
        /// </summary>
        public int chunkID = 0;

        // Properties
        /// <summary>
        /// Get the time in seconds that this chunk starts.
        /// </summary>
        public float ChunkStartTime
        {
            get
            {
                if (Count == 0)
                    return 0;

                // Get timestamp of first frame
                return this[0].TimeStamp;
            }
        }

        /// <summary>
        /// Get the time in seconds that this chunk ends.
        /// </summary>
        public float ChunkEndTime
        {
            get
            {
                // No data
                if (Count == 0)
                    return 0;

                // Get the timestamp of the last frame
                return this[Count - 1].TimeStamp;
            }
        }

        /// <summary>
        /// Get the time in seconds that this chunk lasts.
        /// </summary>
        public float ChunkDuration
        {
            get { return ChunkEndTime - ChunkStartTime; }
        }

        // Constructor
        internal ReplayFileChunk() { }

        public ReplayFileChunk(int chunkID)
        {
            this.chunkID = chunkID;
        }

        // Methods
        public bool Equals(ReplayFileChunk other)
        {
            return chunkID == other.chunkID;
        }

        /// <summary>
        /// Create a member clone of this chunk.
        /// </summary>
        /// <returns>A cloned version of this chunk</returns>
        public ReplayFileChunk Clone()
        {
            ReplayFileChunk result = new ReplayFileChunk(chunkID);
            
            // Store all chunks
            foreach (ReplaySnapshot snapshot in this)
                result.Store(snapshot);

            return result;
        }

        /// <summary>
        /// Store the specified snapshot in this chunk.
        /// The snapshot will be categorized by its time stamp.
        /// </summary>
        /// <param name="snapshot">The snapshot to store</param>
        public void Store(ReplaySnapshot snapshot)
        {
            // Add snapshot
            Add(snapshot);

            // Sort based on timestamp
            Sort((a, b) =>
            {
                return a.TimeStamp.CompareTo(b.TimeStamp);
            });
        }

        /// <summary>
        /// Attempt to restore a snapshot from this chunk that best matches the specified time stamp.
        /// The time stamp must lie within the <see cref="ChunkStartTime"/> and <see cref="ChunkEndTime"/> in order for a snapshot to be resolved.  
        /// </summary>
        /// <param name="timeStamp">The approximate time stamp for the snapshot</param>
        /// <returns>The best matching snapshot for the timestamp or null if the time stamp lie outside the bounds of this chunk</returns>
        public ReplaySnapshot Restore(float timeStamp)
        {
            // Check for no replay data
            if (Count == 0)
                return null;

            // Check for past clip end - this shouldnt happen
            if (timeStamp < ChunkStartTime || timeStamp > ChunkEndTime)
            {
                // This chunk does not contain that time stamp
                return null;
            }

            // Default to first frame
            ReplaySnapshot current = this[0];

            // Check all states to find the best matching snapshot that has a time stamp greater than the specified offset
            foreach(ReplaySnapshot snapshot in this)
            {
                // Store the current snapshot
                current = snapshot;

                // Check if the timestamp is passed the offset
                if (snapshot.TimeStamp >= timeStamp)
                    break;
            }

            return current;
        }

        /// <summary>
        /// Called by the file streamer when the chunk should be serialized to the specified stream.
        /// </summary>
        /// <param name="writer"></param>
        public void OnReplayDataSerialize(BinaryWriter writer)
        {
            writer.Write(chunkID);
            writer.Write(ChunkStartTime);
            writer.Write(ChunkEndTime);

            // Write the snapshot size
            writer.Write(Count);

            // Write all snapshot data
            foreach (ReplaySnapshot snapshot in this)
            {
                // Serialize the snapshot
                snapshot.OnReplayDataSerialize(writer);
            }
        }

        /// <summary>
        /// Called by the file streamer when the chunk should be deserialized from the specified stream.
        /// </summary>
        /// <param name="reader"></param>
        public void OnReplayDataDeserialize(BinaryReader reader)
        {
            chunkID = reader.ReadInt32();
            float start = reader.ReadSingle();
            float end = reader.ReadSingle();

            // Read the snapshot size
            int size = reader.ReadInt32();

            // Read all snapshot data
            for (int i = 0; i < size; i++)
            {
                // Create a new sapshot - time stamp will be overwritten when deserialize is called
                ReplaySnapshot snapshot = new ReplaySnapshot(0);

                // Deserialize the snapshot
                snapshot.OnReplayDataDeserialize(reader);
                
                // Register the snapshot
                Add(snapshot);
            }

            // Verify start / end times
            if (start != ChunkStartTime || end != ChunkEndTime)
                Debug.LogWarning("Possible corrupt replay file chunk - Expected time stamps do not match actual time stamps");
        }
    }
}