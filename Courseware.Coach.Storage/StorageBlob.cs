using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Courseware.Coach.Storage.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.Storage
{
    public class StorageBlob : IStorageBlob
    {
        protected static class Containers
        {
            public const string images = nameof(images);
        }
        protected BlobServiceClient Client { get; }
        public StorageBlob(BlobServiceClient client)
        {
            Client = client;
        }
        protected BlobClient GetClient(string container, string id)
        {
            return Client.GetBlobContainerClient(container).GetBlobClient(id);
        }
        public async Task<byte[]?> GetData(string id, CancellationToken token = default)
        {
            var client = GetClient(Containers.images, id);
            var content = await client.DownloadContentAsync(token);
            return content?.Value.Content.ToArray();
        }

        public Task<Stream> GetStream(string id, CancellationToken token = default)
        {
           var client = GetClient(Containers.images, id);
           return client.OpenReadAsync(new BlobOpenReadOptions(false), token);
        }

        public async Task SetData(string id, byte[] data, CancellationToken token = default)
        {
            var client = GetClient(Containers.images, id);
            using (var stream = new MemoryStream(data))
            {
                await client.UploadAsync(stream, true, token);
            }
        }
    }
}
