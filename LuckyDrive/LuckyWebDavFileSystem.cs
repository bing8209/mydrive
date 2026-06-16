using System;
using System.Net.Http;
using System.Threading.Tasks;
using Fsp;

namespace LuckyDrive
{
    public class LuckyWebDavFileSystem : FileSystemBase
    {
        private readonly string _url;
        private readonly string _user;
        private readonly string _pass;
        private readonly HttpClient _http;

        public LuckyWebDavFileSystem(string url, string user, string pass)
        {
            _url = url.EndsWith("/") ? url : url + "/";
            _user = user;
            _pass = pass;

            _http = new HttpClient();
            var authToken = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{_user}:{_pass}"));
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);
        }

        public override int Read(object fileDesc, IntPtr buffer, long offset, uint length, out uint bytesRead)
        {
            string fileName = (string)fileDesc;
            uint tempBytesRead = 0;

            int result = Task.Run(async () =>
            {
                try
                {
                    string targetUrl = _url + fileName.TrimStart('\\').Replace('\\', '/');
                    var request = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(offset, offset + (long)length - 1);

                    using (var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            var data = await response.Content.ReadAsByteArrayAsync();
                            System.Runtime.InteropServices.Marshal.Copy(data, 0, buffer, data.Length);
                            tempBytesRead = (uint)data.Length;
                            return STATUS_SUCCESS;
                        }
                    }
                    return STATUS_UNSUCCESSFUL;
                }
                catch
                {
                    return STATUS_UNSUCCESSFUL;
                }
            }).GetAwaiter().GetResult();

            bytesRead = tempBytesRead;
            return result;
        }

        public override int GetVolumeInfo(out VolumeInfo volumeInfo)
        {
            volumeInfo = default;
            volumeInfo.TotalSize = 50ULL * 1024 * 1024 * 1024;
            volumeInfo.FreeSize = 25ULL * 1024 * 1024 * 1024;
            volumeInfo.SetVolumeLabel("LuckyDrive");
            return STATUS_SUCCESS;
        }

        public override int Open(string fileName, uint createOptions, uint grantedAccess, out object fileDesc)
        {
            fileDesc = fileName;
            return STATUS_SUCCESS;
        }
    }
}
