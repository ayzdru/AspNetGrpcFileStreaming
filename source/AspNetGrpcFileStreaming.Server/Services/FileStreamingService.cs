using AspNetGrpcFileStreaming.V1.FileStreaming;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.AspNetCore.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace AspNetGrpcFileStreaming.Server.Services
{
    public class FileStreamingService : V1.FileStreaming.FileStreamingService.FileStreamingServiceBase
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        public FileStreamingService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }
        private string GetPath()
        {
            var contentRootPath = "";

#if DEBUG
            contentRootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
#else
                    contentRootPath = webHostEnvironment.ContentRootPath;
#endif
            return Path.Combine(contentRootPath, "Files");
        }
        public override async Task<Empty> Upload(IAsyncStreamReader<BytesContent> requestStream, ServerCallContext context)
        {
            var path = GetPath();
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            
            FileStream fileStream = null;

            try
            {
                int count = 0;

                decimal chunkSize = 0;

                while (await requestStream.MoveNext())
                {
                    if (count++ == 0)
                    {
                        fileStream = new FileStream($"{path}/{requestStream.Current.Info.FileName}{requestStream.Current.Info.FileExtension}", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

                        fileStream.SetLength(requestStream.Current.FileSize);
                    }

                    var buffer = requestStream.Current.Buffer.ToByteArray();

                    await fileStream.WriteAsync(buffer, 0, requestStream.Current.ReadedByte);

                    Console.WriteLine($"{Math.Round(((chunkSize += requestStream.Current.ReadedByte) * 100) / requestStream.Current.FileSize)}%");
                }              

            }
            catch (Exception ex)
            {
            }
            await fileStream.DisposeAsync();
            fileStream.Close();
            return new Empty();
        }
        public override async Task Download(V1.FileStreaming.FileInfo request, IServerStreamWriter<BytesContent> responseStream, ServerCallContext context)
        {

            var path = GetPath();
            try
            {
                using FileStream fileStream = new FileStream($"{path}/{request.FileName}{request.FileExtension}", FileMode.Open, FileAccess.Read);

                byte[] buffer = new byte[2048];

                BytesContent content = new BytesContent
                {
                    FileSize = fileStream.Length,
                    Info = new V1.FileStreaming.FileInfo { FileName = "video", FileExtension = ".mp4" },
                    ReadedByte = 0
                };

                while ((content.ReadedByte = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    content.Buffer = ByteString.CopyFrom(buffer);
                    await responseStream.WriteAsync(content);
                }

                fileStream.Close();
            }
            catch (Exception ex)
            {

              
            }
       
        }
    }
}
