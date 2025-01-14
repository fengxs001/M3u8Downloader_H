﻿using System;
using M3u8Downloader_H.M3U8.Attributes;
using M3u8Downloader_H.M3U8.Core;
using M3u8Downloader_H.M3U8.Infos;
using M3u8Downloader_H.M3U8.Utilities;

namespace M3u8Downloader_H.M3U8.AttributeReaders
{
    [M3U8Reader("#EXT-X-ALLOW-CACHE", typeof(AllowCacheAttributeReader))]
    internal class AllowCacheAttributeReader : AttributeReader
    {
        protected override void Write(M3UFileInfo fileInfo, string value, LineReader reader, Uri baseUri)
        {
            fileInfo.AllowCache = To.Value<bool>(value);
        }
    }
}