using System;
using System.IO;
using System.Net;
using Fsp;

namespace LuckyDrive
{
    public class LuckyWebDavFileSystem : FileSystemBase
    {
        private readonly string _url;
        private readonly string _authHeader;

        public LuckyWebDavFileSystem(string url, string user, string pass)
        {
            _url = url.EndsWith("/") ? url : url + "/";
            var rawToken = System.Text.Encoding.UTF8.GetBytes($"{user}:{pass}");
            _authHeader = "Basic " + Convert.ToBase64String(rawToken);
        }

        // 👇 1. 严格对齐：使用 nint 替代 IntPtr，完美契合基类虚方法
        public override int Read(object fileDesc, nint buffer, long offset, uint length, out uint bytesRead)
        {
            string fileName = (string)fileDesc;
            bytesRead = 0;

            try
            {
                string targetUrl = _url + fileName.TrimStart('\\').Replace('\\', '/');
                var request = (HttpWebRequest)WebRequest.Create(targetUrl);
                request.Method = "GET";
                request.Headers["Authorization"] = _authHeader;
                
                long rangeEnd = offset + (long)length - 1;
                request.AddRange(offset, rangeEnd);

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.PartialContent)
                    {
                        using (var responseStream = response.GetResponseStream())
                        using (var ms = new MemoryStream())
                        {
                            responseStream.CopyTo(ms);
                            byte[] data = ms.ToArray();

                            if (data.Length > 0)
                            {
                                System.Runtime.InteropServices.Marshal.Copy(data, 0, buffer, data.Length);
                                bytesRead = (uint)data.Length;
                                return STATUS_SUCCESS;
                            }
                        }
                    }
                }
                return STATUS_UNSUCCESSFUL;
            }
            catch
            {
                return STATUS_UNSUCCESSFUL;
            }
        }

        // 👇 2. 严格对齐：显式指定 Fsp.VolumeInfo 命名空间，大小写和结构体彻底锁死
        public override int GetVolumeInfo(out Fsp.VolumeInfo volumeInfo)
        {
            volumeInfo = default;
            volumeInfo.TotalSize = 50UL * 1024 * 1024 * 1024;
            volumeInfo.FreeSize = 25UL * 1024 * 1024 * 1024;
            return STATUS_SUCCESS;
        }

        // 👇 3. 严格对齐：Open 签名参数
        public override int Open(string fileName, uint createOptions, uint grantedAccess, out object fileDesc)
        {
            fileDesc = fileName;
            return STATUS_SUCCESS;
        }
    }
}
