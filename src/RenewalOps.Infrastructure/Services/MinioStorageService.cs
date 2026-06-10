using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;
using RenewalOps.Application.Interfaces;

namespace RenewalOps.Infrastructure.Services;

public class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioStorageService> _logger;
    private readonly string _bucketName;
    private bool _bucketEnsured;

    public MinioStorageService(
        IMinioClient minioClient,
        IConfiguration config,
        ILogger<MinioStorageService> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
        _bucketName = config["MinIO:BucketName"] ?? "renewalops";
    }

    public async Task<string> UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        if (!content.CanSeek)
        {
            var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            buffer.Position = 0;
            content = buffer;
        }

        var args = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(args, ct);
        _logger.LogInformation("Uploaded object {Key} to bucket {Bucket}", key, _bucketName);

        return key;
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        var memoryStream = new MemoryStream();

        var args = new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream));

        await _minioClient.GetObjectAsync(args, ct);
        memoryStream.Position = 0;

        return memoryStream;
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(key);

        await _minioClient.RemoveObjectAsync(args, ct);
        _logger.LogInformation("Deleted object {Key} from bucket {Bucket}", key, _bucketName);
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        if (_bucketEnsured) return;

        var exists = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucketName), ct);

        if (!exists)
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_bucketName), ct);
            _logger.LogInformation("Created MinIO bucket {Bucket}", _bucketName);
        }

        _bucketEnsured = true;
    }
}
