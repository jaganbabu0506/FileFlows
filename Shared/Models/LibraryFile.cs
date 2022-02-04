namespace FileFlows.Shared.Models
{
    using FileFlows.Plugin;
    using System;

    public class LibraryFile : FileFlowObject
    {
        public string RelativePath { get; set; }

        public string OutputPath { get; set; }

        public ObjectReference Flow { get; set; }

        public ObjectReference Library { get; set; }
        public ObjectReference Duplicate { get; set; }

        public long OriginalSize { get; set; }
        public long FinalSize { get; set; }
        public string Fingerprint { get; set; }
        public ObjectReference Node { get; set; }

        public Guid WorkerUid { get; set; }

        public DateTime ProcessingStarted { get; set; }
        public DateTime ProcessingEnded { get; set; }

        public FileStatus Status { get; set; }
        public int Order { get; set; }

        public bool IsDirectory { get; set; }

        public TimeSpan ProcessingTime
        {
            get
            {
                if (Status == FileStatus.Unprocessed)
                    return new TimeSpan();
                if (Status == FileStatus.Processing)
                    return DateTime.UtcNow.Subtract(ProcessingStarted);
                if (ProcessingEnded < new DateTime(2000, 1, 1))
                    return new TimeSpan();
                return ProcessingEnded.Subtract(ProcessingStarted);
            }
        }
    }

    public enum FileStatus
    {
        OutOfSchedule = -1,
        Unprocessed = 0,
        Processed = 1,
        Processing = 2,
        FlowNotFound = 3,
        ProcessingFailed = 4,
        Duplicate = 5
    }
}