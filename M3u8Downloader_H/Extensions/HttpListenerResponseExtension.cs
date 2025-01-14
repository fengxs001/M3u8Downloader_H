﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace M3u8Downloader_H.Extensions
{
    public static class HttpListenerResponseExtension
    {
        public static void Json(this HttpListenerResponse response,string message)
        {
            response.StatusCode = 200;
            response.ContentType = "application/json;charset=UTF-8";
            response.ContentEncoding = Encoding.UTF8;
            var retText = Encoding.UTF8.GetBytes(message);
            response.ContentLength64 = retText.Length;

            using var stream = response.OutputStream;
            stream.Write(retText, 0, retText.Length);
            response.Close();
        }

        public static void Text(this HttpListenerResponse response, string message)
        {
            response.StatusCode = 200;
            response.ContentType = "text/plain;charset=utf-8";
            response.ContentEncoding = Encoding.UTF8;
            var retText = Encoding.UTF8.GetBytes(message);
            response.ContentLength64 = retText.Length;

            using var stream = response.OutputStream;
            stream.Write(retText, 0, retText.Length);
            response.Close();
        }
    }
}
