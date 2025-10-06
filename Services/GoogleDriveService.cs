using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Microsoft.Extensions.Options;

namespace Vonage_App_POC.Services;

public class GoogleDriveService : IDisposable
{
    private readonly DriveService _driveService;
    private readonly IOptions<WorkshopOptions> _workshopOptions;
    private readonly ILogger<GoogleDriveService> _logger;

    public GoogleDriveService(IOptions<WorkshopOptions> workshopOptions, ILogger<GoogleDriveService> logger)
    {
        _workshopOptions = workshopOptions ?? throw new ArgumentNullException(nameof(workshopOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _driveService = InitializeDriveService();
    }

    private DriveService InitializeDriveService()
    {
        var credentialsPath = _workshopOptions.Value.GoogleCredentialsPath;
        if (string.IsNullOrEmpty(credentialsPath) || !File.Exists(credentialsPath))
            throw new FileNotFoundException($"Google credentials file not found at: {credentialsPath}");

        try
        {
            using var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read);
            var googleClientSecrets = GoogleClientSecrets.FromStream(stream).Secrets;

            var scopes = new[] { DriveService.Scope.DriveFile };

            var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                googleClientSecrets,
                scopes,
                "user",
                CancellationToken.None,
                new Google.Apis.Util.Store.FileDataStore("TokenStore", true)
            ).Result;

            return new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Vonage_App_POC"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Google Drive service");
            throw;
        }
    }

    public async Task<string> CreateCallFolderAsync(string conversationUuid)
    {
        try
        {
            var folderMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = $"Call_{conversationUuid}_{DateTime.UtcNow:yyyyMMdd_HHmmss}",
                MimeType = "application/vnd.google-apps.folder",
                Parents = new[] { _workshopOptions.Value.GoogleDriveFolderId }
            };

            var request = _driveService.Files.Create(folderMetadata);
            request.Fields = "id, webViewLink";
            var folder = await request.ExecuteAsync();

            await MakePublicAsync(folder.Id);

            _logger.LogInformation("Created Google Drive folder {FolderId} for conversation {ConversationUuid}",
                folder.Id, conversationUuid);

            return folder.WebViewLink;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Google Drive folder for conversation {ConversationUuid}", conversationUuid);
            throw;
        }
    }

    public async Task<string> UploadFileAsync(string localFilePath, string fileName, string contentType, string parentFolderId)
    {
        if (!File.Exists(localFilePath))
            throw new FileNotFoundException($"File not found: {localFilePath}");

        try
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = fileName,
                Parents = new[] { parentFolderId }
            };

            await Task.Delay(50);

            using var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var request = _driveService.Files.Create(fileMetadata, fs, contentType);
            request.Fields = "id, webViewLink";

            var result = await request.UploadAsync();

            if (result.Status != UploadStatus.Completed)
                throw new InvalidOperationException($"Upload failed: {result.Exception?.Message}");

            var file = request.ResponseBody!;
            await MakePublicAsync(file.Id);

            _logger.LogInformation("Uploaded file {FileName} to Google Drive folder {FolderId}",
                fileName, parentFolderId);

            return file.WebViewLink;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file {FileName} to Google Drive", fileName);
            throw;
        }
    }

    public async Task<string> UploadTextFileAsync(string content, string fileName, string parentFolderId)
    {
        var tempPath = string.Empty;
        try
        {
            var uniqueFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid()}{Path.GetExtension(fileName)}";
            tempPath = Path.Combine(Path.GetTempPath(), uniqueFileName);

            await File.WriteAllTextAsync(tempPath, content, System.Text.Encoding.UTF8);
            await Task.Delay(100);

            var result = await UploadFileAsync(tempPath, fileName, "text/plain", parentFolderId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload text file {FileName} to Google Drive", fileName);
            throw;
        }
        finally
        {
            if (!string.IsNullOrEmpty(tempPath) && File.Exists(tempPath))
            {
                try
                {
                    await Task.Delay(100);
                    File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file {TempPath}", tempPath);
                }
            }
        }
    }

    private async Task MakePublicAsync(string fileId)
    {
        try
        {
            var permission = new Google.Apis.Drive.v3.Data.Permission
            {
                Type = "anyone",
                Role = "reader"
            };
            await _driveService.Permissions.Create(permission, fileId).ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to make file {FileId} public", fileId);
        }
    }

    public string? ExtractFolderIdFromUrl(string webViewLink)
    {
        try
        {
            var uri = new Uri(webViewLink);
            var segments = uri.AbsolutePath.Split('/');
            return segments.LastOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract folder ID from URL: {Url}", webViewLink);
            return null;
        }
    }

    public void Dispose()
    {
        _driveService?.Dispose();
        GC.SuppressFinalize(this);
    }
}