﻿using System.IO;
using System.Net;

namespace M3u8Downloader_H.Extensions
{
    public static class HttpListenerRequestExtension
    {
        public static string ReadText(this HttpListenerRequest httpListenerRequest)
        {
            using StreamReader streamReader = new(httpListenerRequest.InputStream);
            string result = streamReader.ReadToEnd().Trim();
            return string.IsNullOrEmpty(result) ? throw new InvalidDataException("不能是空的内容") : result;
        }
    }
}
