using System.Diagnostics;
using BtCloudDownload.BtCloud;
using Microsoft.Extensions.Logging;

namespace BtCloudDownload.Checkpoint;

public class BackupService 
{
    private readonly AudioCheckpointStore _audioCheckpointStore;
    private readonly DocumentCheckpointStore _documentCheckpointStore;
    private readonly PhotosVideoCheckpointStore _photosAndVideoCheckpointStore;
    private readonly BtCloudClient _btCloudClient;
    private readonly BackupOptions _backupOptions;
    private readonly ILogger _logger;

    public BackupService(AudioCheckpointStore audioCheckpointStore, DocumentCheckpointStore documentCheckpointStore, PhotosVideoCheckpointStore photosAndVideoCheckpointStore,
        BtCloudClient btCloudClient, BackupOptions backupOptions, ILogger<BackupService> logger) 
    {
        _audioCheckpointStore = audioCheckpointStore;
        _documentCheckpointStore = documentCheckpointStore;
        _photosAndVideoCheckpointStore = photosAndVideoCheckpointStore;
        _btCloudClient = btCloudClient;
        _backupOptions = backupOptions;
        _logger = logger;
    }

    public async Task PerformBackup() 
    {
        await Task.WhenAll(RunDocumentsBackup(), RunPhotosAndVideosBackup(), RunAudioBackup());
    }

     // Documents
    async Task RunDocumentsBackup()
    {
        var checkpointRecord = _documentCheckpointStore.GetCheckpoint();

        while (true)
        {
            if (checkpointRecord.LastCheckpoint.IsLast) 
            {
                _logger.LogInformation("Completed downloading documents");
                return;
            }

            var backupDirectory = Path.Combine(_backupOptions.BackupDirectory, "Documents");
            Directory.CreateDirectory(backupDirectory);

            var checkpoint = checkpointRecord.LastCheckpoint;
            var checkpointParameters = checkpoint.Parameters;
            _logger.LogInformation("Document params: Start={Start}, Count={Count}, Cursor={Cursor}", checkpointParameters.Start, checkpointParameters.Count, checkpointParameters.Cursor);

            var sw = Stopwatch.StartNew();
            var documentsResponse = await _btCloudClient.GetDocuments(checkpointParameters.Start, checkpointParameters.Count, checkpointParameters.Cursor);

            var files = documentsResponse.FilesHolder.Files.File
                .Select(f => $"{f.Repository.Repository}:{f.ParentPath.ParentPath}/{f.Name.Name}")
                .ToArray();
            if (files.Length == 0)
            {
                break;
            }

            var fileStart = checkpoint.FileCount + 1;
            var filesEnd = checkpoint.FileCount + files.Length;


            _logger.LogInformation("Requesting zip of {FileCount} documents ({FileStart} - {FileEnd})", files.Length, fileStart, filesEnd);

            var zipStream = await _btCloudClient.GetZip(files);

            _logger.LogInformation("Creating zip of {FileCount} documents ({FileStart} - {FileEnd})", files.Length, fileStart, filesEnd);

            using (var fileStream = File.Create(Path.Combine(backupDirectory, $"Zip{fileStart}-{filesEnd}.zip")))
            {
                zipStream.CopyTo(fileStream);
            }

            sw.Stop();
            _logger.LogInformation("Creating zip of {FileCount} documents ({FileStart} - {FileEnd}) in {Elapsed}", files.Length, fileStart, filesEnd, sw.Elapsed);

            checkpoint = new Checkpoint(new CheckpointParameters(
                checkpointParameters.Start + 1, 
                checkpointParameters.Count, 
                documentsResponse.FilesHolder.Cursor?.Cursor),
                checkpoint.FileCount + files.Length,
                documentsResponse.FilesHolder.Cursor?.Cursor is null);
            checkpointRecord = new CheckpointRecord(checkpoint, checkpointRecord.CheckpointHistory.Add(checkpoint));
            _documentCheckpointStore.StoreCheckpoint(checkpointRecord);
        }
    }

    // Photos and videos
    async Task RunPhotosAndVideosBackup()
    {
        var checkpointRecord = _photosAndVideoCheckpointStore.GetCheckpoint();

        var backupDirectory = Path.Combine(_backupOptions.BackupDirectory, "Photos");
        Directory.CreateDirectory(backupDirectory);

        while (true)
        {
            if (checkpointRecord.LastCheckpoint.IsLast) 
            {
                _logger.LogInformation("Completed downloading photos/videos");
                return;
            }

            var checkpoint = checkpointRecord.LastCheckpoint;
            var checkpointParameters = checkpoint.Parameters;
            _logger.LogInformation("Photos params: Start={Start}, Count={Count}, Cursor={Cursor}", checkpointParameters.Start, checkpointParameters.Count, checkpointParameters.Cursor);

            var sw = Stopwatch.StartNew();
            var documentsResponse = await _btCloudClient.GetPhotos(checkpointParameters.Start, checkpointParameters.Count, checkpointParameters.Cursor);

            var files = documentsResponse.FilesHolder.Files.File
                .Select(f => $"{f.Repository.Repository}:{f.ParentPath.ParentPath}/{f.Name.Name}")
                .ToArray();

            if (files.Length == 0)
            {
                break;
            }

            var fileStart = checkpoint.FileCount + 1;
            var filesEnd = checkpoint.FileCount + files.Length;

            _logger.LogInformation("Requesting zip of {FileCount} photos/videos ({FileStart} - {FileEnd})", files.Length, fileStart, filesEnd);

            var zipStream = await _btCloudClient.GetZip(files);

            _logger.LogInformation("Creating zip of {FileCount} photos/videos ({FileStart} - {FileEnd})", files.Length, fileStart, filesEnd);

            using (var fileStream = File.Create(Path.Combine(backupDirectory, $"Zip{fileStart}-{filesEnd}.zip")))
            {
                zipStream.CopyTo(fileStream);
            }

            sw.Stop();
            _logger.LogInformation("Creating zip of {FileCount} photos/videos ({FileStart} - {FileEnd}) in {Elapsed}", files.Length, fileStart, filesEnd, sw.Elapsed);

            checkpoint = new Checkpoint(new CheckpointParameters(
                checkpointParameters.Start + 1, 
                checkpointParameters.Count, 
                documentsResponse.FilesHolder.Cursor?.Cursor), 
                checkpoint.FileCount + files.Length,
                documentsResponse.FilesHolder.Cursor?.Cursor is null);
            checkpointRecord = new CheckpointRecord(checkpoint, checkpointRecord.CheckpointHistory.Add(checkpoint));
            _photosAndVideoCheckpointStore.StoreCheckpoint(checkpointRecord);
        }
    }

    // Music
    async Task RunAudioBackup()
    {
        var checkpointRecord = _audioCheckpointStore.GetCheckpoint();

        var backupDirectory = Path.Combine(_backupOptions.BackupDirectory, "Audio");
        Directory.CreateDirectory(backupDirectory);

        while (true)
        {
            if (checkpointRecord.LastCheckpoint.IsLast) 
            {
                _logger.LogInformation("Completed downloading audio files");
                return;
            }

            var checkpoint = checkpointRecord.LastCheckpoint;
            var checkpointParameters = checkpoint.Parameters;
            _logger.LogInformation("Audio params: Start={Start}, Count={Count}, Cursor={Cursor}", checkpointParameters.Start, checkpointParameters.Count, checkpointParameters.Cursor);

            var sw = Stopwatch.StartNew();
            var documentsResponse = await _btCloudClient.GetAudio(checkpointParameters.Start, checkpointParameters.Count, checkpointParameters.Cursor);

            var files = documentsResponse.FilesHolder.Files.File
                .Select(f => $"{f.Repository.Repository}:{f.ParentPath.ParentPath}/{f.Name.Name}")
                .ToArray();

            if (files.Length == 0)
            {
                break;
            }

            var fileStart = checkpoint.FileCount + 1;
            var filesEnd = checkpoint.FileCount + files.Length;

            _logger.LogInformation("Requesting zip of {FileCount} audio files ({FileStart} - {FileEnd})", files.Length, fileStart, filesEnd);

            var zipStream = await _btCloudClient.GetZip(files);

            _logger.LogInformation("Creating zip of {FileCount} audio files ({FileStart} - {FileEnd})", files.Length, fileStart, filesEnd);

            using (var fileStream = File.Create(Path.Combine(backupDirectory, $"Zip{fileStart}-{filesEnd}.zip")))
            {
                zipStream.CopyTo(fileStream);
            }

            sw.Stop();
            _logger.LogInformation("Creating zip of {FileCount} audio files ({FileStart} - {FileEnd}) in {Elapsed}", files.Length, fileStart, filesEnd, sw.Elapsed);

            checkpoint = new Checkpoint(new CheckpointParameters(
                checkpointParameters.Start + 1, 
                checkpointParameters.Count, 
                documentsResponse.FilesHolder.Cursor?.Cursor), 
                checkpoint.FileCount + files.Length,
                documentsResponse.FilesHolder.Cursor?.Cursor is null);
            checkpointRecord = new CheckpointRecord(checkpoint, checkpointRecord.CheckpointHistory.Add(checkpoint));
            _audioCheckpointStore.StoreCheckpoint(checkpointRecord);
        }
    }
}

