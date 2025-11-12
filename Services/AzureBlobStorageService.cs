using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

public class AzureBlobStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(IConfiguration config)
    {
        var connectionString = config["Storage:ConnectionString"];
        var containerName = config["Storage:ContainerName"];

        var blobServiceClient = new BlobServiceClient(connectionString);
        _container = blobServiceClient.GetBlobContainerClient(containerName);

        // ✅ تأكد أن الكونتينر موجود
        _container.CreateIfNotExists();

        // ✅ اجعل الوصول علني للملفات فقط (وليس للكونتينر بالكامل)
        _container.SetAccessPolicy(PublicAccessType.Blob);
    }

    // ✅ رفع ملف إلى Azure Blob Storage
    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string folder = "")
    {
        var fileId = Guid.NewGuid().ToString() + Path.GetExtension(fileName);
        var blobPath = string.IsNullOrEmpty(folder) ? fileId : $"{folder}/{fileId}";

        var blobClient = _container.GetBlobClient(blobPath);

        await blobClient.UploadAsync(fileStream, new BlobHttpHeaders
        {
            ContentType = contentType
        });

        return blobClient.Uri.ToString(); // 🔗 يرجع رابط مباشر للملف
    }

    // ✅ حذف ملف من Azure Blob Storage
    public async Task DeleteAsync(string blobUrl)
    {
        if (string.IsNullOrEmpty(blobUrl))
            return;

        // 👇 هذا التصحيح يجعل الحذف يعمل مع أي رابط كامل
        var uri = new Uri(blobUrl);
        var blobName = uri.AbsolutePath.TrimStart('/');
        var blobClient = _container.GetBlobClient(blobName);

        await blobClient.DeleteIfExistsAsync();
    }
}
