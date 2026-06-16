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

        // 👇 1. 终极对齐：官方最新定义中，buffer 依然保持 IntPtr，但 fileDesc 必须是不带可空标记的纯 object
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
                return STATUS_SUCCESS;
            }
            catch
            {
                return STATUS_UNSUCCESSFUL;
            }
        }

        // 👇 2. 完美过关的卷信息（保持不动）
        public override int GetVolumeInfo(out Fsp.Interop.VolumeInfo volumeInfo)
        {
            volumeInfo = default;
            volumeInfo.TotalSize = 100UL * 1024 * 1024 * 1024; // 100GB
            volumeInfo.FreeSize = 50UL * 1024 * 1024 * 1024;
            return STATUS_SUCCESS;
        }

        // 👇 3. 终极对齐：官方接口中最后一个参数的名字必须叫 fileDescription，且必须是 object 类型
        public override int Open(string fileName, uint createOptions, uint grantedAccess, out object fileDescription)
        {
            fileDescription = fileName;
            return STATUS_SUCCESS;
        }
    }
}
