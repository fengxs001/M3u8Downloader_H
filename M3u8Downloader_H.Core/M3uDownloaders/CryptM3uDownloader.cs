﻿using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using M3u8Downloader_H.Extensions;
using M3u8Downloader_H.M3U8.Infos;

namespace M3u8Downloader_H.Core.M3uDownloaders
{
    internal class CryptM3uDownloader : M3u8Downloader
    {
        private readonly HttpClient http;
        private readonly IEnumerable<KeyValuePair<string, string>>? headers;
        private readonly M3UFileInfo m3UFileInfo;
        public CryptM3uDownloader(HttpClient http, IEnumerable<KeyValuePair<string, string>>? headers, M3UFileInfo m3UFileInfo, IProgress<double> progress) : base(http, headers, progress)
        {
            this.http = http;
            this.headers = headers;
            this.m3UFileInfo = m3UFileInfo;
        }

        public override async ValueTask Initialization(CancellationToken cancellationToken)
        {
            if (m3UFileInfo.Key is null)
                throw new InvalidDataException("没有可用的密钥信息");

            if(m3UFileInfo.Key.Uri != null && m3UFileInfo.Key.BKey == null)
            {
                try
                {
                    byte[] data = m3UFileInfo.Key.Uri.IsFile
                        ? await File.ReadAllBytesAsync(m3UFileInfo.Key.Uri.OriginalString, cancellationToken)
                        : await http.GetByteArrayAsync(m3UFileInfo.Key.Uri, headers, cancellationToken);

                    m3UFileInfo.Key.BKey = data.TryParseKey(m3UFileInfo.Key.Method);
                }catch(HttpRequestException e) when(e.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new HttpRequestException("获取密钥失败，没有找到任何数据",e.InnerException,e.StatusCode);
                }
            }else
            {
                m3UFileInfo.Key.BKey = m3UFileInfo.Key.BKey != null
                    ? m3UFileInfo.Key.BKey.TryParseKey(m3UFileInfo.Key.Method)
                    : throw new InvalidDataException("密钥为空");
            }
        }

        protected override Stream DownloadAfter(Stream stream, string contentType, CancellationToken cancellationToken)
        {
            Stream Decryptstream = stream.AesDecrypt(m3UFileInfo.Key.BKey, m3UFileInfo.Key.IV);
            return base.DownloadAfter(Decryptstream, contentType, cancellationToken);
        }
    }
}
