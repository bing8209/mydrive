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

            var rawToken = System.Text.Encoding.UTF8.GetBytes($"{_user}:{_pass}");
            _authHeader = "Basic " + Convert.ToBase64String(rawToken);
        }

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

        public override int GetVolumeInfo(out VolumeInfo volumeInfo)
        {
            volumeInfo = default;
            // 👇 终极修复：把 50ULL 改为标准的 C# 64位无符号长整型后缀 50UL
            volumeInfo.TotalSize = 50UL * 1024 * 1024 * 1024;
            volumeInfo.FreeSize = 25UL * 1024 * 1024 * 1024;
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
