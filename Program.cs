using System.Diagnostics;
using BtCloudDownload.BtCloud;
using BtCloudDownload.Checkpoint;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

string? backupPathInput = null;

if (args.Length > 0) 
{
    backupPathInput = args[0];
}
while (string.IsNullOrEmpty(backupPathInput)) 
{
    Console.Write("Please enter a back up path. E.g. C:/MyBackupFolder: ");
    backupPathInput = Console.ReadLine();
}

var workingDirectory = Path.GetFullPath(backupPathInput);

Directory.CreateDirectory(workingDirectory);

var serviceCollection = new ServiceCollection();
var loggerConfiguration = new LoggerConfiguration();
loggerConfiguration.WriteTo.Console();
Log.Logger = loggerConfiguration.CreateLogger();

serviceCollection.AddLogging(builder => builder.AddSerilog());

serviceCollection.AddSingleton<BtCloudTokenStore>();
serviceCollection.AddSingleton<BtCloudAuthorizationClient>();

serviceCollection.AddSingleton<BtCloudClient>();
serviceCollection.AddSingleton<BtCloudTokenStoreOptions>(new BtCloudTokenStoreOptions() { WorkingDirectory = workingDirectory });

var copyDirectory = Path.Combine(workingDirectory, "Copy");
Directory.CreateDirectory(copyDirectory);

serviceCollection.AddSingleton<BackupOptions>(new BackupOptions() { BackupDirectory = copyDirectory });

serviceCollection.AddSingleton<DocumentCheckpointStore>();
serviceCollection.AddSingleton<AudioCheckpointStore>();
serviceCollection.AddSingleton<PhotosVideoCheckpointStore>();

serviceCollection.AddSingleton<BackupService>();

var serviceProvider = serviceCollection.BuildServiceProvider();

var tokenStore = serviceProvider.GetRequiredService<BtCloudTokenStore>();
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
var backupService = serviceProvider.GetRequiredService<BackupService>();

if (string.IsNullOrWhiteSpace(tokenStore.GetToken())) 
{
    string? token = null;
    while (string.IsNullOrWhiteSpace(token)) 
    {
        Console.Write("Please enter token: ");
        token = Console.ReadLine();
    }
    tokenStore.SaveToken(token);
}

logger.LogInformation("Beginning backup procedure");

await backupService.PerformBackup();

logger.LogInformation("All backups complete");
