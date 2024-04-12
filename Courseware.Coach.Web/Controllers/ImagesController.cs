using Courseware.Coach.Storage.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MimeDetective;

namespace Courseware.Coach.Web.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImagesController : ControllerBase
    {
        protected IStorageBlob StorageBlob { get; }
        protected ContentInspector MimeDetector { get; }
        public ImagesController(IStorageBlob storageBlob, ContentInspector mimeDetector)
        {
            StorageBlob = storageBlob;
            MimeDetector = mimeDetector;
        }
        [HttpGet("{id}")]
        public async Task<IActionResult> GetImage(string id, CancellationToken token = default)
        {
            byte[]? data = await StorageBlob.GetData(id, token);
            if (data == null)
                return NotFound();
            var results = MimeDetector.Inspect(data);
            string mimeType = "application/octet-stream";
            if (results.Length > 0)
                mimeType = results.ByMimeType().FirstOrDefault()?.MimeType ?? mimeType;
            return File(data, mimeType);
        }
    }
}
