using AspNetGrpcFileStreaming.V1.FileStreaming;
using Google.Protobuf;
using Grpc.Net.Client;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static AspNetGrpcFileStreaming.V1.FileStreaming.FileStreamingService;

namespace AspNetGrpcFileStreaming.Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
           
            await FileStreamingUpload();
            await FileStreamingDownload();
        }
        private static async Task FileStreamingUpload()
        {
            var channel = GrpcChannel.ForAddress("https://localhost:5317");
            var client = new FileStreamingServiceClient(channel);
            var contentRootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string file = Path.Combine(contentRootPath, "Files", "sample.mp4");
            var fileInfo = new System.IO.FileInfo(file);
            using FileStream fileStream = new FileStream(file, FileMode.Open);

            var content = new BytesContent
            {
                FileSize = fileStream.Length,
                ReadedByte = 0,
                Info = new V1.FileStreaming.FileInfo { FileName = Path.GetFileNameWithoutExtension(fileInfo.Name), FileExtension = fileInfo.Extension }
            };

            var upload = client.Upload();

            byte[] buffer = new byte[2048];

            while ((content.ReadedByte = fileStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                content.Buffer = ByteString.CopyFrom(buffer);
                await upload.RequestStream.WriteAsync(content);
            }
            await upload.RequestStream.CompleteAsync();

            fileStream.Close();
        }
        private static async Task FileStreamingDownload()
        {
            var channel = GrpcChannel.ForAddress("https://localhost:5317");
            var client = new FileStreamingServiceClient(channel);
   var contentRootPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            string downloadPath = Path.Combine(contentRootPath, "DownloadFiles");
            if (!Directory.Exists(downloadPath))
                Directory.CreateDirectory(downloadPath);
            var fileInfo = new V1.FileStreaming.FileInfo
            {
                FileExtension = ".mp4",
                FileName = "sample"
            };

            FileStream fileStream = null;

            var request = client.Download(fileInfo);

            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            int count = 0;
            decimal chunkSize = 0;

            while (await request.ResponseStream.MoveNext(cancellationTokenSource.Token))
            {
                if (count++ == 0)
                {
                    fileStream = new FileStream(@$"{downloadPath}\{request.ResponseStream.Current.Info.FileName}{request.ResponseStream.Current.Info.FileExtension}", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                    fileStream.SetLength(request.ResponseStream.Current.FileSize);
                }

                var buffer = request.ResponseStream.Current.Buffer.ToByteArray();
                await fileStream.WriteAsync(buffer, 0, request.ResponseStream.Current.ReadedByte);

                Console.WriteLine($"{Math.Round(((chunkSize += request.ResponseStream.Current.ReadedByte) * 100) / request.ResponseStream.Current.FileSize)}%");
            }
            Console.WriteLine("İndirildi...");

            await fileStream.DisposeAsync();
            fileStream.Close();
        }
    }
}
