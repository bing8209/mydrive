using System;
using System.IO;
using System.Net;
using Fsp;

namespace LuckyDrive
{
    // 👇 继承自 FileSystemBase 核心驱动类
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

        // 👇 1. 对齐标准的 Read 接口签名
        public override int Read(object fileDesc, IntPtr buffer, long offset, uint length, out uint bytesRead)
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

        // 👇 2. 终极对齐：使用 FileSystemVolumeInfo 替代旧版的 VolumeInfo
        public override int GetVolumeInfo(out FileSystemVolumeInfo volumeInfo)
        {
            volumeInfo = default;
            volumeInfo.TotalSize = 50UL * 1024 * 1024 * 1024; // 伪装 50GB
            volumeInfo.FreeSize = 25UL * 1024 * 1024 * 1024;
            // 最新版 WinFsp.Net 移除了 volumeInfo.SetVolumeLabel，直接在外面用 Host 挂载时指定标签更稳妥
            return STATUS_SUCCESS;
        }

        // 👇 3. 对齐标准的 Open 接口签名
        public override int Open(string fileName, uint createOptions, uint grantedAccess, out object fileDesc)
        {
            fileDesc = fileName;
            return STATUS_SUCCESS;
        }
    }
}
