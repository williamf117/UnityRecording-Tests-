// Disable threading on web platforms
#if UNITY_WEBGL
#define ULTIMATEREPLAY_NOTHREADING
#endif

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;

#if UNITY_5_3_OR_NEWER
using UnityEngine.SceneManagement;
#endif

namespace UltimateReplay.Storage
{
    /// <summary>
    /// A file task request identifier.
    /// Used to request specific file operations on the streaming thread.
    /// </summary>
    internal enum ReplayFileRequest
    {
        /// <summary>
        /// No task, Skip cycle.
        /// </summary>
        Idle = 0,
        /// <summary>
        /// Fetch a chunk from the replay file.
        /// </summary>
        FetchChunk,
        /// <summary>
        /// Fetch a chunk from the replay file as low priority and buffer it for later use.
        /// </summary>
        FetchChunkBuffered,
        /// <summary>
        /// Write a chunk to the replay file.
        /// </summary>
        WriteChunk,
        /// <summary>
        /// Commit any buffered data and finalize the replay file.
        /// </summary>
        Commit,
        /// <summary>
        /// Discard any buffered data and destroy the replay file.
        /// </summary>
        Discard,
        /// <summary>
        /// Fetch the file header from the replay file.
        /// </summary>
        FetchHeader,
        /// <summary>
        /// Write the header to the replay file.
        /// </summary>
        WriteHeader,
        /// <summary>
        /// Fetch the file chunk table from the replay file.
        /// </summary>
        FetchTable,
        /// <summary>
        /// Fetch the initial state buffer from the replay file.
        /// </summary>
        FetchStateBuffer,
    }

    /// <summary>
    /// The priority of a file stream task request.
    /// All <see cref="ReplayFileTaskPriority.High"/> priority tasks will be pushed ahead of <see cref="ReplayFileTaskPriority.Normal"/> tasks.  
    /// </summary>
    internal enum ReplayFileTaskPriority
    {        
        /// <summary>
        /// The task is time critical and should be completed as fast as possible.
        /// </summary>
        High = 0,
        /// <summary>
        /// The task is not time critical and may be held off until other important tasks have completed.
        /// </summary>
        Normal,
    }

    internal struct ReplayFileTaskRequest
    {
        // Public
        public ReplayFileTaskID taskID;
        public ReplayFileRequest task;
        public ReplayFileTaskPriority priority;
        public object data;
    }

    /// <summary>
    /// Internal structure used for managing unique task id's for threaded task requests.
    /// The structure is thread safe.
    /// </summary>
    internal struct ReplayFileTaskID
    {
        // Private
        private static List<int> usedTasks = new List<int>();
        private int id;

        // Public
        /// <summary>
        /// Get a task id that is initialized to the default value.
        /// </summary>
        public static ReplayFileTaskID empty = new ReplayFileTaskID { id = -1 };

        // Constructor
        private ReplayFileTaskID(int id)
        {
            this.id = id;
        }

        // Methods
        /// <summary>
        /// Generate a unique task ID.
        /// </summary>
        /// <returns>A <see cref="ReplayFileTaskID"/> that is unique for the current session</returns>
        public static ReplayFileTaskID GenerateID()
        {
            lock(usedTasks)
            {
                int current = 0;
                int id = -1;

                while(id == -1)
                {
                    // Increase search
                    int temp = current++ * (27 + current);

                    // We have found a valid id
                    if (usedTasks.Contains(temp) == false)
                        id = temp;
                }

                // Create a task id
                return new ReplayFileTaskID(id);
            }
        }

        /// <summary>
        /// Release a unique task ID. use this to allow previously assigned id's to be reused.
        /// </summary>
        /// <param name="taskID">The task ID to release</param>
        public static void ReleaseID(ReplayFileTaskID taskID)
        {
            lock(usedTasks)
            {
                // Remove if present
                if(usedTasks.Contains(taskID.id) == true)
                    usedTasks.Remove(taskID.id);
            }
        }
    }

    internal class ReplayFileContext
    {
        // Public
        public ReplayFileHeader header = new ReplayFileHeader();
        public ReplayFileChunkTable chunkTable = new ReplayFileChunkTable();
        public ReplayFileChunk chunk = new ReplayFileChunk();
        public ReplayFileBuffer buffer = new ReplayFileBuffer();
        public ReplayInitialDataBuffer initialStateBuffer = new ReplayInitialDataBuffer();
        public ReplayFileStream fileStream = null;
    }

    /// <summary>
    /// Represents a file storage target were replay data can be stored persistently between game sessions.
    /// The file target has an unlimited storage capacity but you should ensure that files do not get too large by optimizing your replay objects.
    /// </summary>
    public sealed class ReplayFileTarget : ReplayTarget
    {
        // Private
        // Chunk storage
        private ReplayFileContext context = new ReplayFileContext();

        // Threading
        private List<ReplayFileTaskRequest> threadTasks = new List<ReplayFileTaskRequest>();
        private HashSet<int> processingChunkRequests = new HashSet<int>();
        private Thread streamThread = null;
        private bool threadRunning = true;
        private bool threadStarted = false;
        private int chunkIdGenerator = 0;

        // File names
        private string targetFileLocation = string.Empty;

        /// <summary>
        /// The directory path where all replay files should be stored.
        /// </summary>
        [SerializeField]
        [Tooltip("The directory path where all recorded files will be stored. If this value is empty then the files will be saved in the application directory")]
        private string fileDirectory = string.Empty;
        /// <summary>
        /// The name of the file to record replay data to.
        /// </summary>
        [SerializeField]
        [Tooltip("The file name to save recorded data to")]
        private string fileName = "ReplayData" + defaultExtension;

        // Public
        /// <summary>
        /// The ideal number of chunks that are stored as a block. Larger chunk sizes allow for better compression and seeking but cost more memory.
        /// </summary>
        public const int chunkSize = 24; // 24 snapshots per chunk
        /// <summary>
        /// The default file extension for all replay files.
        /// </summary>
        public const string defaultExtension = ".replay";

        /// <summary>
        /// Should any existing replay file with the same name be replaced during recording.
        /// </summary>
        [Tooltip("When true, any existing files with the same name will be overwritten. When false, a new file with an auto-incremented id will be created based on the name value")]
        public bool overwriteExistingFiles = false;

        /// <summary>
        /// The amount of chunks that can be loaded into memory at any time.
        /// </summary>
        [Tooltip("The amount of chunks that can be pre-fetched from the replay file so that buffering does not occur. Higher values may give smoother results but will use more memory")]
        public int chunkCacheSize = 16;

        [Header("Debug")]
        public bool logDebugMessages = false;

        // Properties
        /// <summary>
        /// Get the amount of time in seconds that the current recording lasts.
        /// </summary>
        public override float Duration
        {
            get
            {
                // Lock thread access
                lock (context)
                {
                    // Get duration
                    return context.header.duration;
                }
            }
        }

        /// <summary>
        /// Get the amount of bytes required to store the replay data.
        /// </summary>
        public override int MemorySize
        {
            get
            {
                // Lock thread access
                lock (context)
                {
                    // Get memory size
                    return context.header.memorySize;
                }
            }
        }

        public override ReplayInitialDataBuffer InitialStateBuffer
        {
            get
            {
                // Lock thread access
                lock(context)
                {
                    // Get the initial state buffer
                    return context.initialStateBuffer;
                }
            }
        }

        /// <summary>
        /// Get the name of the scene that the recording was taken from.
        /// </summary>
        public override string TargetSceneName
        {
            get
            {
                // Lock thread access
                lock(context)
                {
                    // Get the scene name
                    return context.header.sceneName;
                }
            }
        }

        /// <summary>
        /// The path to the target directory where all replay files should be stored.
        /// By default, this value is empty and will cause files to be generated next to the executable.
        /// </summary>
        public string FileOutputDirectory
        {
            get { return fileDirectory; }
            set
            {
                fileDirectory = value;
                RebuildFilePaths();

                // Release buffered chunks
                context.buffer.ReleaseAllChunks();
            }
        }

        /// <summary>
        /// The name of the replay file that will be saved.
        /// The name may be modified when assigned to ensure that it conforms to variour settings such as <see cref="overwriteExistingFiles"/>.
        /// File extensions may be specified in the name to overwrite the <see cref="defaultExtension"/>. 
        /// </summary>
        public string FileOutputName
        {
            get { return fileName; }
            set
            {
                fileName = value;
                RebuildFilePaths();

                // Release buffered chunks
                context.buffer.ReleaseAllChunks();
            }
        }

        /// <summary>
        /// Get the file path for the target replay recording file.
        /// Use <see cref="FileOutputDirectory"/> and <see cref="FileOutputName"/> to modify the file location.  
        /// </summary>
        public string TargetFileLocation
        {
            get { return targetFileLocation; }
        }

        // Methods
        /// <summary>
        /// Called by Unity.
        /// </summary>
        public override void Awake()
        {
            // Dont execute in edit mode
            if (Application.isPlaying == false)
                return;

            // Dont call base method - not required

#if ULTIMATEREPLAY_NOTHREADING == false
            // Create the thread
            streamThread = new Thread(new ThreadStart(StreamThreadMain));

            // Setup the thread
            streamThread.IsBackground = true;
            streamThread.Name = "UltimateReplay_StreamService";

            // Launch the thread
            streamThread.Start();
#endif

            // Force paths to be updated
            RebuildFilePaths();
        }

        /// <summary>
        /// Called by Unity.
        /// </summary>
        public override void OnDestroy()
        {
            // Dont execute in edit mode
            if (Application.isPlaying == false)
                return;

            // Dont call base method

#if ULTIMATEREPLAY_NOTHREADING == false
            // Request the thread to exit
            threadRunning = false;
            streamThread.Join(1500);

            // Force the thread to stop
            if (streamThread.IsAlive == true)
                streamThread.Abort();
#endif

            // Close the file streams
            Close();
        }

#region FileSystem
        /// <summary>
        /// Closes all open file streams.
        /// Do not call this method during recording or playback.
        /// It should only be used when the <see cref="ReplayFileTarget"/> is no longer required.
        /// Note that calling <see cref="UnityEngine.Object.Destroy(UnityEngine.Object)"/> on the target will trigger this <see cref="Close"/> method. 
        /// </summary>
        public void Close()
        {
            // Close the main stream
            if (context.fileStream != null)
            {
                context.fileStream.Dispose();
                context.fileStream = null;
            }
        }

        /// <summary>
        /// Causes <see cref="TargetFileLocation"/> to be validated and generated if necessary.
        /// This may also cause <see cref="FileOutputName"/> to be modified to conform to storage settings. 
        /// </summary>
        public void RebuildFilePaths()
        {
            // ### Target location
            string extension = Path.GetExtension(fileName);
            string name = Path.GetFileNameWithoutExtension(fileName);

            // Check for any extension - append the default extension if none is found
            if (string.IsNullOrEmpty(extension) == true)
            {
                extension = defaultExtension;
                fileName += defaultExtension;
            }

            // Create the directory if required
            if (string.IsNullOrEmpty(fileDirectory) == false)
                if (Directory.Exists(fileDirectory) == false)
                    Directory.CreateDirectory(fileDirectory);

            // Build the full path
            string fullPath = string.Format("{0}{1}", Path.Combine(fileDirectory, name), extension);

            // Check for overwite
            if (overwriteExistingFiles == false)
            {
                int counter = 0;

                // Loop until we find an unused name
                while (File.Exists(fullPath) == true)
                {
                    // Append a number to the file name and retry
                    fullPath = string.Format("{0}{1}{2}", Path.Combine(fileDirectory, name), counter, extension);

                    // increment number counter
                    counter++;
                }
            }

            // Store the path
            targetFileLocation = fullPath;
        }

        /// <summary>
        /// Attempt to open the final output file stream using the specified file mode.
        /// </summary>
        /// <param name="mode">The file mode used to open the file</param>
        /// <returns>A <see cref="ReplayFileStream"/> representing the open file stream</returns>
        private ReplayFileStream OpenFileStream(ReplayFileStreamMode mode)
        {
            // Get the full path
            string fullPath = TargetFileLocation;

            if (File.Exists(fullPath) == true)
            {
                // Check for write only
                if (mode == ReplayFileStreamMode.WriteOnly)
                {
                    // Delete the file
                    File.Delete(fullPath);
                }
            }

            // Open file for reading and writing
            return new ReplayFileStream(fullPath, mode);
        }
#endregion

#region ReplayTarget_Behaviour
        public void Update()
        {
        }
#endregion

#region ReplayTarget_Base
        /// <summary>
        /// Attempts to store the specified <see cref="ReplaySnapshot"/> into the file stream using a chunk based system.
        /// The operation will be pushed to a streaming thread so that game systems will not be affected performance wise.
        /// </summary>
        /// <param name="state">The snapshot to write to file</param>
        public override void RecordSnapshot(ReplaySnapshot state)
        {
            // Lock the chunk from other threads
            lock (context)
            {
                // Check for trial
#if ULTIMATEREPLAY_TRIAL == true
                // Limit the file recording to 5 minutes
                if(state.TimeStamp > (5f * 60f))
                {
                    Debug.LogWarning("The trial version of Ultimate Replay only allows 5 minutes recording so you can sample the file size and streaming performance");
                    return;
                }
#endif

                // Set the time stamp
                context.header.duration = state.TimeStamp;

                // Store the state
                context.chunk.Store(state);

                // Check if we can commit the chunk yet
                if (context.chunk.Count > chunkSize)
                {
                    // Clone the chunk so we can operate on it in the streaming thread
                    ReplayFileChunk copy = context.chunk.Clone();

                    // Create a chunk write request
                    CreateTaskAsync(ReplayFileRequest.WriteChunk, ReplayFileTaskPriority.Normal, copy);

                    // Reset our working chunk
                    context.chunk = new ReplayFileChunk(++chunkIdGenerator);
                }
            }
        }

        /// <summary>
        /// Attempts to fetch a replay chunk from the replay file containing the required replay data and then returns the desired <see cref="ReplaySnapshot"/> for the specified replay offset.
        /// Seeking may cause higher fetch times as random seek offsets cannot be predicted (Buffering).
        /// </summary>
        /// <param name="offset">The replay time offset to fetch the snapshot for</param>
        /// <returns>A napshot of the recording for the specified time offset</returns>
        public override ReplaySnapshot RestoreSnapshot(float offset)
        {
            bool fetchChunkRequired = false;

            lock(context)
            {
                // Check if we need to load the chunk
                if(context.chunk.Restore(offset) == null)
                {
                    // Check for a match
                    if(context.buffer.HasLoadedChunk(offset) == true)
                    {
                        LogReplayWarning("Using buffered chunk");

                        // Make the chunk active
                        context.chunk = context.buffer.GetLoadedChunk(offset);
                    }
                    else
                    {
                        // We need to fetch the chunk from file
                        fetchChunkRequired = true;
                    }
                }
            }

            // Check if we need to fetch the chunk from the file
            if(fetchChunkRequired == true)
            {
                // Request a chunk fetch quickly
                ReplayFileTaskID task = CreateTaskAsync(ReplayFileRequest.FetchChunk, ReplayFileTaskPriority.High, new ReplayFileChunkFetchData(offset));
                
                // Wait for completion - This may take a few 100 milliseconds
                WaitForSingleTask(task);
            }
            else
            {
                LogReplayMessage("An appropriate cached chunk was found and will be used to prevent chunk buffering");
            }
            
            // Chunk Prediciton - This section will issue a number of fetch requests for chunks that will likley be required soon
            lock (context)
            {
                // Fetch the next chunk
                int currentChunkID = context.chunk.chunkID;
                
                // Get a direction indicator value
                int incrementValue = (PlaybackDirection == PlaybackDirection.Forward) ? 1 : -1;

                // Queue up some fetch requests
                int current = currentChunkID + incrementValue;

                // Check if the chunk is loaded - if so then skip it since we are only interested in non-loaded chunks
                while (context.buffer.HasLoadedChunk(current) == true 
                    && Mathf.Abs(current) < chunkCacheSize)
                {
                    // Increment the chunk id until we find a chunk that we have not loaded
                    current += incrementValue;
                }


                if (context.buffer.HasLoadedChunk(current) == false && processingChunkRequests.Contains(current) == false)
                {
                    LogReplayWarning("Fetching next chunk: {0}", current);

                    // Begin streaming the next chunk from file
                    CreateTaskAsync(ReplayFileRequest.FetchChunkBuffered, ReplayFileTaskPriority.Normal, new ReplayFileChunkFetchData(current));

                    // Register the chunk as being requested
                    if(processingChunkRequests.Contains(current) == false)
                        processingChunkRequests.Add(current);
                }


#if ULTIMATEREPLAY_NOTHREADING == false
                // Chunk Dropout - This section will cleanup any hanging chunks which will likley not be required in the near future 
                if (PlaybackDirection == PlaybackDirection.Forward)
                {
                    // Release chunks before the current chunk start time because we are replaying in forward mode
                    context.buffer.ReleaseOldChunks(context.chunk.ChunkStartTime, ReplayFileEnumReleaseMode.ChunksBefore);
                }
                else
                {
                    // Release chunks after the current chunk end time because we are replaying in reverse
                    context.buffer.ReleaseOldChunks(context.chunk.ChunkEndTime, ReplayFileEnumReleaseMode.ChunksAfter);
                }
#endif

                //float nextTime = (PlaybackDirection == PlaybackDirection.Forward) ? context.chunk.ChunkEndTime + 1 : context.chunk.ChunkStartTime - 1;
                //float currentTime = nextTime;
                //float maxPredictionTime = 20f;

                //// 
                //while (Mathf.Abs(currentTime) - Mathf.Abs(nextTime) < maxPredictionTime && 
                //    context.buffer.HasLoadedChunk(nextTime) == true)
                //    currentTime += (PlaybackDirection == PlaybackDirection.Forward) ? 1 : -1;

                //// Check if the buffered chunk has been loaded
                //if (context.buffer.HasLoadedChunk(currentTime) == false)
                //{
                //    LogReplayWarning("Fetching next chunk: {0}", currentTime);

                //    // Begin streaming the next chunk from file
                //    CreateTaskAsync(ReplayFileRequest.FetchChunkBuffered, ReplayFileTaskPriority.Normal, currentTime);
                //}

                // Get the best matching snapshot
                return context.chunk.Restore(offset);
            }
        }

        /// <summary>
        /// Called by the replay system when file target preparation is required.
        /// </summary>
        /// <param name="mode">The <see cref="ReplayTargetTask"/> that the target should prepare for</param>
        public override void PrepareTarget(ReplayTargetTask mode)
        {
            switch (mode)
            {
                case ReplayTargetTask.Commit:
                    {
                        ReplayFileTaskID task = ReplayFileTaskID.empty;

                        // Check if the working chunk has any data that should be flushed
                        lock (context)
                        {
                            if (context.chunk.Count > 0)
                            {
                                // Copy the chunk so we can work on it in the streaming thread
                                ReplayFileChunk copy = context.chunk.Clone();

                                // Write the working chunk
                                task = CreateTaskAsync(ReplayFileRequest.WriteChunk, ReplayFileTaskPriority.High, copy);

                                // Set the flushing flag - force wait for completion
                                context.chunk = new ReplayFileChunk();
                            }
                        }

                        // Wait for the task to complete
                        if(task.Equals(ReplayFileTaskID.empty) == false)
                            WaitForSingleTask(task);

#if ULTIMATEREPLAY_NOTHREADING
                        // Process all waiting tasks on the main thread - all chunks will be waiting to be written and registered with the chunk table
                        StreamThreadProcessAllWaitingTasks();
#endif

                        // We can finally commit the data
                        task = CreateTaskAsync(ReplayFileRequest.Commit, ReplayFileTaskPriority.High);

                        // Wait for the commit to complete before returning
                        WaitForSingleTask(task);

#if ULTIMATEREPLAY_NOTHREADING
                        // Process all waiting tasks on the main thread
                        StreamThreadProcessAllWaitingTasks();
#endif
                        break;
                    }

                case ReplayTargetTask.Discard:
                    {
                        // Run the task on the background thread
                        ReplayFileTaskID task = CreateTaskAsync(ReplayFileRequest.Discard);

                        // Wait for the discard to complete before returning
                        WaitForSingleTask(task);

#if ULTIMATEREPLAY_NOTHREADING
                        // Process all waiting tasks on the main thread
                        StreamThreadProcessAllWaitingTasks();
#endif
                        break;
                    }

                case ReplayTargetTask.PrepareWrite:
                    {
                        // Open temp stream
                        context.fileStream = OpenFileStream(ReplayFileStreamMode.WriteOnly);

#if UNITY_5_3_OR_NEWER
                        // Update the scene name
                        context.header.sceneName = SceneManager.GetActiveScene().name;
#else
                        // Update the scene name
                        context.header.sceneName = Application.loadedLevelName;
#endif

                        // Reset chunk generator
                        chunkIdGenerator = 0;

                        // Write the header - We will overwirte thislater during the commit stage so most data can be meaningless
                        ReplayFileTaskID headerTask = CreateTaskAsync(ReplayFileRequest.WriteHeader, ReplayFileTaskPriority.High);

                        // Wait for the header to be written
                        WaitForSingleTask(headerTask);

#if ULTIMATEREPLAY_NOTHREADING
                        // Process all waiting tasks on the main thread
                        StreamThreadProcessAllWaitingTasks();
#endif
                        break;
                    }

                case ReplayTargetTask.PrepareRead:
                    {
                        // Open the file for reading
                        context.fileStream = OpenFileStream(ReplayFileStreamMode.ReadOnly);

                        // Request the file header and wait for completion
                        ReplayFileTaskID headerTask = CreateTaskAsync(ReplayFileRequest.FetchHeader, ReplayFileTaskPriority.High);
                        
                        // Wait for the header to be loaded - We need it before we can accept fetch calls
                        WaitForSingleTask(headerTask);

                        // Request the chunk table and wait for completion
                        ReplayFileTaskID tableTask = CreateTaskAsync(ReplayFileRequest.FetchTable, ReplayFileTaskPriority.High);

                        WaitForSingleTask(tableTask);

                        // Request the initial state buffer and wait for completion
                        ReplayFileTaskID bufferTask = CreateTaskAsync(ReplayFileRequest.FetchStateBuffer, ReplayFileTaskPriority.High);

                        WaitForSingleTask(bufferTask);

#if ULTIMATEREPLAY_NOTHREADING
                        // Fetch the header and chunk table before we can identify the chunks that the file contains
                        StreamThreadProcessAllWaitingTasks();

                        // Fetch all chunks in the file immediatley - Be very carefull - large files may cause out of memory issues of have long wait times on the main thread
                        CreateFetchAllChunksTask();

                        // Process all waiting tasks on the main thread
                        StreamThreadProcessAllWaitingTasks();
#endif
                        break;
                    }
            }
        }
#endregion

        private void CreateFetchAllChunksTask()
        {
            // Process all chunks in the table
            foreach(ReplayFileChunkTableEntry entry in context.chunkTable)
            {
                // Request the chunk
                CreateTaskAsync(ReplayFileRequest.FetchChunk, ReplayFileTaskPriority.Normal, new ReplayFileChunkFetchData(entry.chunkID));

                // Release the id immediatley
                //ReplayFileTaskID.ReleaseID(task);
            }
        }

#region StreamThreadTasks
        private void ThreadWriteReplayChunk(ReplayFileChunk chunk)
        {
            LogReplayMessage("Attempting to write replay chunk '{0}' - streaming operation", chunk.chunkID);

            // Lock thread access
            lock (context)
            {
                // Check for valid stream
                if (context.fileStream != null)
                {
                    // Get the file pointer for the chunk - offsets are 0 based so we need to account for the header size
                    int pointer = context.fileStream.Position - context.header.dataOffset;

                    // Add an entry in the chunk table
                    context.chunkTable.CreateEntry(chunk.chunkID, chunk.ChunkStartTime, chunk.ChunkEndTime, pointer);

                    // Append the chunk
                    chunk.OnReplayDataSerialize(context.fileStream.Writer);
                }
            }
        }

        private ReplayFileChunk ThreadReadReplayChunk(ReplayFileChunkFetchData fetchData)// float timeStamp)
        {
            // Check for error
            if (context.fileStream == null)
                return null;

            int pointer = -1;

            if(fetchData.isIDBased == true)
            {
                LogReplayMessage("Attempting to fetch replay chunk with id '{0}'", fetchData.chunkID);

                // Try to get the file pointer of the chunk from the id
                pointer = context.chunkTable.GetPointerForChunk(fetchData.chunkID);

                // Check for invalid pointer
                if (pointer == -1)
                {
                    LogReplayWarning("Failed to read replay chunk from file stream: With chunk id: '{0}'", fetchData.chunkID);
                    return null;
                }
            }
            else
            {
                LogReplayMessage("Attempting to fetch replay chunk for timestamp '{0}'", fetchData.chunkTimeStamp);

                // Get the chunk for the time stamp
                pointer = context.chunkTable.GetPointerForTimeStamp(fetchData.chunkTimeStamp);

                // Check for invalid pointer
                if (pointer == -1)
                {
                    LogReplayWarning("Failed to read replay chunk from file stream: For time stamp: '{0}'", fetchData.chunkTimeStamp);
                    return null;
                }
            }      
            

            // Seek to offset - jump over the header and chunk table data
            int fileOffset = context.header.dataOffset + pointer;
            
            // Move to chunk location
            context.fileStream.Seek(fileOffset, SeekOrigin.Begin);

            // Create a chunk to hold the data
            ReplayFileChunk chunk = new ReplayFileChunk();
            
            // Deserialize chunk
            chunk.OnReplayDataDeserialize(context.fileStream.Reader);

            // Get the chunk data
            return chunk;
        }

        private void ThreadCommitReplayFile()
        {
            LogReplayMessage("Begining replay file commit - The replay file will be finalized for loading");

            lock (context)
            {
                // Check for open stream
                if (context.fileStream == null)
                    return;

                // Get the end position of the file
                int chunkTableStart = context.fileStream.Position;

                // Write the chunk table at the end of the file
                ThreadWriteReplayChunkTable();

                // Get the end position of the file
                int stateBufferStart = context.fileStream.Position;

                // Write the initial state information at the end of the file
                ThreadWriteInitialStateBuffer();

                // Get the new end of the file
                //int fileEnd = context.fileStream.Position;

                // Calcualte the size that the chunk table takes up in bytes
                //int chunkTableSize = fileEnd - chunkTableStart;

                // Go back to the start of the file
                context.fileStream.Seek(0, SeekOrigin.Begin);

                // Update the header info
                {
                    context.header.chunkTableOffset = chunkTableStart; // chunkTableSize;
                    context.header.stateBufferOffset = stateBufferStart;
                }

                // We need to overwrite the header with the correct info - The first time we wrote it, it was just as a size placeholder
                ThreadWriteReplayHeader();                

                // Reset the memory buffers so that identical information is not re-commited
                context.chunkTable = new ReplayFileChunkTable();
                context.chunk = new ReplayFileChunk();
                
                // Release final stream
                context.fileStream.Dispose();
                context.fileStream = null;
            }            
        }

        private void ThreadDiscardReplayFile()
        {
            LogReplayMessage("Discarding repay file recording at: {0}", FileOutputName);

            // Release buffered chunks
            context.buffer.ReleaseAllChunks();

            // Reset all information
            context.header = new ReplayFileHeader();
            context.chunkTable = new ReplayFileChunkTable();
            context.chunk = new ReplayFileChunk();

            // Empty the temp stream
            if (context.fileStream != null)
            {
                context.fileStream.Clear();
                context.fileStream.Dispose();
                context.fileStream = null;
            }

            // Delete file
            string path = TargetFileLocation;

            // Delete the recording file
            if (File.Exists(path) == true)
                File.Delete(path);
        }

        private void ThreadWriteReplayHeader()
        {
            LogReplayMessage("Attempting to write replay file header");

            // Lock thread access
            lock (context)
            {
                // Write to a temp stream so we can calculate the data offset for the header and chunk table
                MemoryStream tempStream = new MemoryStream();

                using (BinaryWriter tempWriter = new BinaryWriter(tempStream))
                {
                    // Call serialize
                    context.header.OnReplayDataSerialize(tempWriter);

                    // Find the data skip size
                    context.header.headerSize = (int)tempStream.Length;
                    context.header.dataOffset = (int)tempStream.Length;
                }

                // Serialize the header and chunk table
                context.header.OnReplayDataSerialize(context.fileStream.Writer);
            }
        }

        private void ThreadWriteReplayChunkTable()
        {
            LogReplayMessage("Attempting to write replay file chunk table");

            // Lock thread access
            lock(context)
            {
                // Check for closed stream
                if (context.fileStream == null)
                    return;

                // Write to file
                context.chunkTable.OnReplayDataSerialize(context.fileStream.Writer);
            }
        }

        private void ThreadWriteInitialStateBuffer()
        {
            LogReplayMessage("Attempting to write initial state buffer to file");

            // Lock thread access
            lock(context)
            {
                // Check for closed stream
                if (context.fileStream == null)
                    return;

                // Write to file
                context.initialStateBuffer.OnReplayDataSerialize(context.fileStream.Writer);
            }
        }

        private void ThreadFetchReplayHeader()
        {
            LogReplayMessage("Attempting to fetch replay file header");

            // Create the header
            ReplayFileHeader header = new ReplayFileHeader();

            // Lock thread access
            lock (context)
            {
                // Check for closed stream
                if (context.fileStream == null)
                {
                    context.header = header;
                    return;
                }

                // Move stream to start
                context.fileStream.Seek(0, SeekOrigin.Begin);

                // Desrialize the header and chunk table
                header.OnReplayDataDeserialize(context.fileStream.Reader);

                // Assign the new header
                context.header = header;
            }
        }

        private void ThreadFetchReplayChunkTable()
        {
            LogReplayMessage("Attempting to fetch replay chunk table");

            // Create a chunk table
            ReplayFileChunkTable table = new ReplayFileChunkTable();

            // Lock thread access
            lock (context)
            {
                // Check for closed stream
                if (context.fileStream == null)
                {
                    context.chunkTable = new ReplayFileChunkTable();
                    return;
                }

                // Get the file offset
                int offset = context.header.chunkTableOffset;

                // Seek stream
                context.fileStream.Seek(offset, SeekOrigin.Begin);

                // Deserialize the chunk table
                table.OnReplayDataDeserialize(context.fileStream.Reader);

                // Assign the loaded table
                context.chunkTable = table;
            }
        }

        private void ThreadFetchInitialStateBuffer()
        {
            LogReplayMessage("Attempting to fetch replay initial state buffer");

            // Create a state buffer
            ReplayInitialDataBuffer buffer = new ReplayInitialDataBuffer();

            // Lock thread access
            lock(context)
            {
                // Check for closed stream
                if(context.fileStream == null)
                {
                    context.initialStateBuffer = new ReplayInitialDataBuffer();
                    return;
                }

                // Get the file offset
                int offset = context.header.stateBufferOffset;

                // Seek stream
                context.fileStream.Seek(offset, SeekOrigin.Begin);

                // Assume we are at the correct file position
                buffer.OnReplayDataDeserialize(context.fileStream.Reader);

                // Assign the loaded state buffer
                context.initialStateBuffer = buffer;
            }
        }
#endregion


        #region StreamThread
        private ReplayFileTaskID CreateTaskAsync(ReplayFileRequest task, ReplayFileTaskPriority priority = ReplayFileTaskPriority.Normal, object data = null)
        {
            // Create a task id
            ReplayFileTaskID taskID = ReplayFileTaskID.GenerateID();

            // Create the request
            ReplayFileTaskRequest request = new ReplayFileTaskRequest
            {
                taskID = taskID,
                task = task,
                priority = priority,
                data = data,
            };

            // Lock the collection from other threads
            lock (threadTasks)
            {
                // Push the request to the queue
                threadTasks.Add(request);

                // Sort based on priority
                threadTasks.Sort((x, y) =>
                {
                    // Check for higher priority
                    return x.priority.CompareTo(y.priority);
                });
            }

            return taskID;
        }

        /// <summary>
        /// Causes the calling thread to wait until the specified task has completed on the streaming thread.
        /// If the thread is not running or has been stopped during waiting then an exception will occur to break the infinite loop.
        /// </summary>
        /// <param name="taskID">The task to wait for. If the task is not in the thread queue then this method will do nothing</param>
        private void WaitForSingleTask(ReplayFileTaskID taskID)
        {
#if ULTIMATEREPLAY_NOTHREADING
            // Thread operations are performed on the main thread after commit
            if(threadStarted == false)
                return;
#endif
            
            // Check for thread flag - We need the thread to be running or we will wait infinitley
            if (threadStarted == false || threadRunning == false)
                throw new InvalidOperationException("File operations cannot be awaited due to the current state of the file streamer: Stream thread is not running");

            // We need to wait for the task
            while (true)
            {
                // Make sure our thread is still running
                if (streamThread.IsAlive == false)
                    throw new ThreadStateException("The stream thread was aborted unexpectedly. Waiting was canceled to avoid infinite waiting but this may cause the state of the file streamer to be corrupted");

                bool foundTask = false;

                // Lock access to the thread tasks
                lock (threadTasks)
                {
                    // Check if the task is still in the queue
                    foreach(ReplayFileTaskRequest request in threadTasks)
                    {
                        if(request.taskID.Equals(taskID) == true)
                        {
                            // We can release the task id now
                            ReplayFileTaskID.ReleaseID(taskID);

                            foundTask = true;
                            break;
                        }
                    }
                }

                // The task is no longer in the queue
                if (foundTask == false)
                    break;
                
                // Wait for a bit while the thread works
                Thread.Sleep(10);
            }
        }

        private void StreamThreadMain()
        {
            try
            {
                // Set thread flag
                threadStarted = true;

                // Loop unitl we are asked to quit
                while (threadRunning == true)
                {
                    // Check if any tasks are waiting
                    if (StreamThreadHasTask() == true)
                    {
                        // Process the waiting tasks
                        StreamThreadProcessWaitingTask();
                    }
                }

                // Check for any more tasks - We need to run them all before we end if possible
                while(StreamThreadHasTask() == true)
                {
                    // Process the task
                    StreamThreadProcessWaitingTask();
                }
            }
            catch (Exception e)
            {
                Debug.LogError(string.Format("An exception caused the 'ReplayFileTarget' to fail (file stream thread : {0})", streamThread.ManagedThreadId));
                Debug.LogException(e);

                // Unset running flag - Thread cannot continue
                threadRunning = false;
            }
        }

        private bool StreamThreadHasTask()
        {
            lock (threadTasks)
            {
                // Check for any tasks
                return threadTasks.Count > 0;
            }
        }

        private void StreamThreadProcessAllWaitingTasks()
        {
            // Process all tasks before we continue
            while (StreamThreadHasTask() == true)
                StreamThreadProcessWaitingTask();
        }

        private void StreamThreadProcessWaitingTask()
        {
            // Get a thread task
            ReplayFileTaskRequest request;

            lock (threadTasks)
            {
                // Require a task to carry on
                if (threadTasks.Count == 0)
                    return;

                // Get the front item
                request = threadTasks[0];
            }

            // Switch for task event
            switch (request.task)
            {
                // Idle - do nothing
                case ReplayFileRequest.Idle:
                    break;

                // Write the specified chunk to the file
                case ReplayFileRequest.WriteChunk:
                    {
                        // Get the chunk passed through the request
                        ReplayFileChunk chunk = request.data as ReplayFileChunk;

                        // Try to write the active chunk to the file
                        ThreadWriteReplayChunk(chunk);
                        break;
                    }

                // Fetch a chunk with the specified chunk id
                case ReplayFileRequest.FetchChunk:
                    {
                        // Get the requested time stamp
                        //float timeStamp = (float)request.data;

                        ReplayFileChunkFetchData fetchData = (ReplayFileChunkFetchData)request.data;

                        // Load the chunk from file
                        ReplayFileChunk chunk = ThreadReadReplayChunk(fetchData);// timeStamp);

                        // Make sure that we do not set the active chunk to null 
                        if (chunk != null)
                        {
                            lock (context)
                            {
                                // Store the loaded chunk
                                context.chunk = chunk;
                                context.buffer.StoreChunk(chunk);
                            }

                            // The chunk has been loaded so we can remove it from the requested set
                            if (processingChunkRequests.Contains(chunk.chunkID) == true)
                                processingChunkRequests.Remove(chunk.chunkID);
                        }
                        break;
                    }

                case ReplayFileRequest.FetchChunkBuffered:
                    {
                        ReplayFileChunkFetchData fetchData = (ReplayFileChunkFetchData)request.data;
                        
                        // Make sure it is not already buffered
                        lock(context)
                        {
                            // The chunk is already loaded so we dont need to do anything
                            if (context.buffer.HasLoadedChunk(fetchData.chunkID) == true)
                                break;
                        }

                        // Load the chunk from file
                        ReplayFileChunk chunk = ThreadReadReplayChunk(fetchData);

                        // make sure the chunk was loaded
                        if(chunk != null)
                        {
                            lock(context)
                            {
                                // Store the chunk in the buffer for later use
                                context.buffer.StoreChunk(chunk);
                            }

                            // The chunk has been loaded so we can remove it from the requested set
                            if (processingChunkRequests.Contains(chunk.chunkID) == true)
                                processingChunkRequests.Remove(chunk.chunkID);
                        }
                        break;
                    }

                // Commit any data still in memeory to file
                case ReplayFileRequest.Commit:
                    {
                        // Commit the data to file
                        ThreadCommitReplayFile();
                        break;
                    }

                // Throw the replay data away
                case ReplayFileRequest.Discard:
                    {
                        // Discard any replay data and file data
                        ThreadDiscardReplayFile();
                        break;
                    }

                case ReplayFileRequest.WriteHeader:
                    {
                        // Write the header to file
                        ThreadWriteReplayHeader();
                        break;
                    }

                case ReplayFileRequest.FetchHeader:
                    {
                        // Fetch the replay header
                        ThreadFetchReplayHeader();
                        break;
                    }

                case ReplayFileRequest.FetchTable:
                    {
                        // Fetch the replay chunk table
                        ThreadFetchReplayChunkTable();
                        break;
                    }

                case ReplayFileRequest.FetchStateBuffer:
                    {
                        // Fetch the state buffer
                        ThreadFetchInitialStateBuffer();
                        break;
                    }
            }

            // Remove the task from the queue
            lock(threadTasks)
            {
                // Remove the request
                if (threadTasks.Contains(request) == true)
                    threadTasks.Remove(request);
            }
        }
#endregion

        /// <summary>
        /// Utility used to display useful information about the replay file target.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void LogReplayMessage(string format, params object[] args)
        {
            if(logDebugMessages == true)
                Debug.Log("ReplayFileTarget (Experimental): " + string.Format(format, args));
        }

        /// <summary>
        /// Utility used to display useful information about the replay file target.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void LogReplayWarning(string format, params object[] args)
        {
            Debug.LogWarning("ReplayFileTarget (Experimental): " + string.Format(format, args));
        }
    }
}