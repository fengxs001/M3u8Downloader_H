﻿using M3u8Downloader_H.M3U8.Attributes;
using M3u8Downloader_H.M3U8.Core;
using M3u8Downloader_H.M3U8.Utilities;
using M3u8Downloader_H.M3U8.Infos;
using System;

namespace M3u8Downloader_H.M3U8.AttributeReaders
{
    [M3U8Reader("#EXT-X-VERSION", typeof(VersionAttributeReader))]
    internal class VersionAttributeReader : AttributeReader
    {
        protected override void Write(M3UFileInfo fileInfo,string value, LineReader reader, Uri baseUri)
        {
            fileInfo.Version = To.Value<int>(value);
        }
    }
}