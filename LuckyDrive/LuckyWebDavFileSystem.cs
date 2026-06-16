// 彻底清空缓存，重新编译！
using System;
using System.IO;
using System.Net;
using Fsp;

namespace LuckyDrive
{
    public class LuckyWebDavFileSystem : FileSystemBase
    {
        private readonly string _url;
        private readonly string _user;
        private readonly string _pass;
        private readonly string _authHeader;

        public LuckyWebDavFileSystem(string url, string user, string pass)
        {
            _url = url.EndsWith("/") ? url : url + "/";
            _user = user;
            _pass = pass;

            // 提前计算好认证 Token，避免运行时重复计算
            var rawToken = System.Text.Encoding.UTF8.GetBytes($"{_user}:{_pass}");
            _authHeader = "Basic " + Convert.ToBase64String(rawToken);
        }

        // 👇 终极改造：使用纯同步流式搬运，彻底消灭引发编译器玄学罢工的 Task/Async 嵌套
        public override int Read(object fileDesc, IntPtr buffer, long offset, uint length, out uint bytesRead)
        {
            string fileName = (string)fileDesc;
            bytesRead = 0;

            try
            {
                string targetUrl = _url + fileName.TrimStart('\\').Replace('\\', '/');
                
                // 1. 创建标准的同步 HTTP 请求
                var request = (HttpWebRequest)WebRequest.Create(targetUrl);
                request.Method = "GET";
                request.Headers["Authorization"] = _authHeader;
                
                // 2. 注入 Range 头：Windows 读多少，我们精准要多少
                long rangeEnd = offset + (long)length - 1;
                request.AddRange(offset, rangeEnd);

                // 3. 同步获取网络响应（100% 阻塞当前系统线程，WinFsp 原生推荐这样干）
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.PartialContent)
                    {
                        using (var responseStream = response.GetResponseStream())
                        using (var ms = new MemoryStream())
                        {
                            // 4. 同步将网络流抽干到本地内存数组
                            responseStream.CopyTo(ms);
                            byte[] data = ms.ToArray();

                            if (data.Length > 0)
                            {
                                // 5. 零缓存直接写入 Windows 系统缓冲区
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

        public override int GetVolumeInfo(out VolumeInfo volumeInfo)
        {
            volumeInfo = default;
            volumeInfo.TotalSize = 50ULL * 1024 * 1024 * 1024; // 50GB
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
