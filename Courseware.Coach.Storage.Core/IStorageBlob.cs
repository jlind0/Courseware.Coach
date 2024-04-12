using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Courseware.Coach.Storage.Core
{
    public interface IStorageBlob
    {
        Task<byte[]?> GetData(string id, CancellationToken token = default);
        Task<Stream> GetStream(string id, CancellationToken token = default);
        Task SetData(string id, byte[] data, CancellationToken token = default);
    }
}
