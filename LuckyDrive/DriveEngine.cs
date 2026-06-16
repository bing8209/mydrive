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

        // 1. 严格对齐最新版核心：nint 缓冲区、uint 长度
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
                return STATUS_SUCCESS; // 网络读取空或结束也属于成功
            }
            catch
            {
                return STATUS_UNSUCCESSFUL;
            }
        }

        // 2. 严格对齐最新版底层 Interop 空间下的卷结构体
        public override int GetVolumeInfo(out Fsp.Interop.VolumeInfo volumeInfo)
        {
            volumeInfo = default;
            volumeInfo.TotalSize = 100UL * 1024 * 1024 * 1024; // 固定的 100GB 虚拟驱动盘
            volumeInfo.FreeSize = 50UL * 1024 * 1024 * 1024;
            return STATUS_SUCCESS;
        }

        // 3. 严格对齐最新版 Open 签名，支持非托管对象映射
        public override int Open(string fileName, uint createOptions, uint grantedAccess, out object? fileDesc)
        {
            fileDesc = fileName;
            return STATUS_SUCCESS;
        }
    }
}
