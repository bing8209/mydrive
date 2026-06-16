using System;
using System.IO;
using System.Net;
using Fsp;

namespace LuckyDrive
{
    // 👇 绝杀：不写 override，不继承任何可能引发签名冲突的基类，直接做成一个平铺的独立类
    public class LuckyWebDavFileSystem
    {
        private readonly string _url;
        private readonly string _authHeader;

        public LuckyWebDavFileSystem(string url, string user, string pass)
        {
            _url = url.EndsWith("/") ? url : url + "/";
            var rawToken = System.Text.Encoding.UTF8.GetBytes($"{user}:{pass}");
            _authHeader = "Basic " + Convert.ToBase64String(rawToken);
        }

        // 把原本容易冲突的内核回调，做成纯粹的业务函数
        public int WebDavRead(string fileName, nint buffer, long offset, uint length, out uint bytesRead)
        {
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
                                return 0; // STATUS_SUCCESS
                            }
                        }
                    }
                }
                return 0;
            }
            catch
            {
                return -1;
            }
        }
    }
}
