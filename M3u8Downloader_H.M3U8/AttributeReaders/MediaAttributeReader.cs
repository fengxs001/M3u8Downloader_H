﻿using M3u8Downloader_H.M3U8.Core;
using M3u8Downloader_H.M3U8.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using M3u8Downloader_H.M3U8.Attributes;
using M3u8Downloader_H.M3U8.Infos;

namespace M3u8Downloader_H.M3U8.AttributeReaders
{
    [M3U8Reader("#EXTINF", typeof(MediaAttributeReader))]
    internal class MediaAttributeReader : AttributeReader
    {
     
        private static string GetNextValue(LineReader reader)
        {
            if (!reader.MoveNext())
                throw new InvalidDataException("Invalid M3U file. Missing a media URI.");

            if (reader.Current == null)
                throw new InvalidDataException("无效的m3u8文件,没有任何有用的视频地址");

            return reader.Current;
        }

        protected override void Write(M3UFileInfo fileInfo, string value, LineReader reader, Uri baseUri)
        {
            fileInfo.MediaFiles ??= new List<M3UMediaInfo>();
            var m3UmediaInfo = new M3UMediaInfo();
            var strArray = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (strArray.Length != 0)
            {
                m3UmediaInfo.Duration = To.Value<float>(strArray[0]) ?? 0;
                //m3UmediaInfo.Title = strArray.Length > 1 ? strArray[1].Trim() : string.Empty;
            }

            string CurrentValue = GetNextValue(reader);
            if (reader.Current.StartsWith("#EXT-X-BYTERANGE", StringComparison.InvariantCultureIgnoreCase))
            {
                HandleRangeValue(m3UmediaInfo, CurrentValue);
                CurrentValue = GetNextValue(reader);
            }
        // https://p2.bdstatic.com/rtmp.liveshow.lss-user.baidubce.com/live/stream_bduid_5053309598_7538660179/merged_1658753298319_318594_681_2732.m3u8
            if (reader.Current.StartsWith("#EXT-X-PROGRAM-DATE-TIME", StringComparison.InvariantCultureIgnoreCase))
            {
                CurrentValue = GetNextValue(reader);
            }

            var relativeUri = new Uri(CurrentValue.Trim(), UriKind.RelativeOrAbsolute);
            if (!relativeUri.IsAbsoluteUri && !relativeUri.IsWellFormedOriginalString())
                throw new InvalidDataException("Invalid M3U file. Include a invalid media URI.",
                    new UriFormatException(CurrentValue));

            if (relativeUri.IsAbsoluteUri)
            {
                m3UmediaInfo.Uri = relativeUri;
                m3UmediaInfo.Title = reader.CurrentIndex; 
            }
            else
            {
                if(baseUri == null)
                    throw new InvalidDataException("baseuri为空");
      
                m3UmediaInfo.Uri = new Uri(baseUri, relativeUri);
                m3UmediaInfo.Title = reader.CurrentIndex;
            }
            
            fileInfo.MediaFiles.Add(m3UmediaInfo);
        }

        private static void HandleRangeValue(M3UMediaInfo m3UMediaInfo,string CurrentValue)
        {
            var ByteRangeArray = CurrentValue.Split(':', 2,StringSplitOptions.RemoveEmptyEntries);
            if (ByteRangeArray.Length < 2)
                throw new InvalidCastException("无效的BYTERANGE字段");

            var ByteRangeContent = ByteRangeArray[1].Split('@',2, StringSplitOptions.RemoveEmptyEntries);
            if (ByteRangeContent.Length < 2)
                throw new InvalidCastException("无效的BYTERANGE字段");

            long? from = To.Value<long>(ByteRangeContent[1]);
            long to = To.Value<long>(ByteRangeContent[0]) ?? 0;
            m3UMediaInfo.RangeValue = new System.Net.Http.Headers.RangeHeaderValue(from, from + to - 1);
        }
    }
}