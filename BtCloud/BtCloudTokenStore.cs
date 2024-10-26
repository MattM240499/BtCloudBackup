namespace BtCloudDownload.BtCloud;

public class BtCloudTokenStore
{
    private string _tokenFilePath;

    public BtCloudTokenStore(BackupOptions backupOptions) 
    {
        _tokenFilePath = Path.Combine(backupOptions.BackupDirectory, "token.txt");
    }

    public void SaveToken(string token) 
    {
        File.WriteAllText(_tokenFilePath, token);
    }

    public string? GetToken() 
    {
        if (!File.Exists(_tokenFilePath)) 
        {
            return null;
        }
        return File.ReadAllText(_tokenFilePath);
    }
}