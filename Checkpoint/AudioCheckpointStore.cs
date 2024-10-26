// See https://aka.ms/new-console-template for more information
using System.Text.Json;
using BtCloudDownload.BtCloud;
using Microsoft.Extensions.Logging;

namespace BtCloudDownload.Checkpoint;

public class AudioCheckpointStore 
{
    private readonly string _checkpointFilePath;

    public AudioCheckpointStore(BackupOptions backupOptions, ILogger<AudioCheckpointStore> logger) 
    {
        _checkpointFilePath = Path.Combine(backupOptions.BackupDirectory, "audioCheckpoint.json");
        logger.LogInformation($"Checkpoint path: {Path.GetFullPath(_checkpointFilePath)}");
    }

    public void StoreCheckpoint(CheckpointRecord checkpointData) 
    {
        File.WriteAllText(_checkpointFilePath, JsonSerializer.Serialize(checkpointData, new JsonSerializerOptions() { WriteIndented = true }));
    }

    public CheckpointRecord GetCheckpoint() 
    {
        if (File.Exists(_checkpointFilePath))
        {
            var checkpointText = File.ReadAllText(_checkpointFilePath);
            var checkpointRecord = JsonSerializer.Deserialize<CheckpointRecord>(checkpointText) ?? throw new ArgumentNullException("checkpoint");
            return checkpointRecord;
        }
        else 
        {
            var checkpointRecord = CheckpointRecord.Empty;
            File.WriteAllText(_checkpointFilePath, JsonSerializer.Serialize(checkpointRecord, new JsonSerializerOptions() { WriteIndented = true }));
            return checkpointRecord;
        }
    }
}
