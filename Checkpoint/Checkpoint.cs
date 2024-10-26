using System.Collections.Immutable;

namespace BtCloudDownload.Checkpoint;

public record CheckpointData(CheckpointRecord DocumentCheckpoint, CheckpointRecord PhotosVideoCheckpoint) 
{
    public static CheckpointData Empty => new CheckpointData(CheckpointRecord.Empty, CheckpointRecord.Empty);
}

public record CheckpointRecord(Checkpoint LastCheckpoint, ImmutableList<Checkpoint> CheckpointHistory)
{
    public static CheckpointRecord Empty => new CheckpointRecord(Checkpoint.Empty, ImmutableList<Checkpoint>.Empty.Add(Checkpoint.Empty));
}

public record Checkpoint(CheckpointParameters Parameters, int FileCount, bool IsLast)
{
    public static Checkpoint Empty => new Checkpoint(new CheckpointParameters(1, 500, null), 0, false);
}

public record CheckpointParameters(int Start, int Count, string? Cursor = null);

public record CheckpointHistory(List<Checkpoint> CheckpointParameters);