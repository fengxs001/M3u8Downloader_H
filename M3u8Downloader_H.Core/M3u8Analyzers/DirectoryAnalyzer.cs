﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using M3u8Downloader_H.Core.Utils;
using M3u8Downloader_H.M3U8.Infos;

namespace M3u8Downloader_H.Core.M3u8Analyzers
{
    internal class DirectoryAnalyzer : AnalyzerBase
    {
        private readonly Uri uri;

        public DirectoryAnalyzer(Uri uri) : base(uri)
        {
            this.uri = uri;
        }

        public override M3UFileInfo Read()
        {
            DirectoryInfo mydir = new(uri.OriginalString);
            var fileinfos = mydir.EnumerateFiles();
            if (!fileinfos.Any())
                throw new InvalidOperationException("此文件夹中没有任何文件");

            M3UFileInfo m3UFileInfo = new()
            {
                Key = GetM3UKeyInfo(fileinfos)!,
                MediaFiles = GetM3UMediaInfos(fileinfos),
                PlaylistType = "VOD"
            };
            return m3UFileInfo;
        }

        private M3UKeyInfo? GetM3UKeyInfo(IEnumerable<FileInfo> fileInfos)
        {
            var keyInfos = fileInfos.FirstOrDefault(f => f.Name.EndsWith(".key", StringComparison.CurrentCultureIgnoreCase));
            if (keyInfos != null)
            {
                return GetM3UKeyInfo(null, keyInfos.FullName, null, null);
            }
            return null;
        }

        public List<M3UMediaInfo> GetM3UMediaInfos(IEnumerable<FileInfo> fileInfos)
        {
            var tmpFileInfos = fileInfos
                .Where(f => f.Name.EndsWith(".ts", StringComparison.CurrentCultureIgnoreCase) || f.Name.EndsWith(".tmp", StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(f => f.Name, new SpecialComparer());

            List<M3UMediaInfo> m3UMediaInfos = new();
            foreach (var fileinfo in tmpFileInfos)
            {
                var m3umediainfo = GetM3UMediaInfo(fileinfo.FullName, fileinfo.Name);
                m3UMediaInfos.Add(m3umediainfo);
            }
            return m3UMediaInfos;
        }
    }
}
