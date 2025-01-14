﻿using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using M3u8Downloader_H.M3U8.Infos;

namespace M3u8Downloader_H.Core.M3uCombiners
{
    internal class M3uCombiner : IM3uCombiner
    {
        private readonly string cacheFullPath;
        private readonly FileStream videoFileStream;
        public M3uCombiner(string dirPath, string videoName, FileMode fileMode = FileMode.Create) : this(dirPath, File.Open(videoName, fileMode, FileAccess.Write, FileShare.Read))
        {

        }

        public M3uCombiner(string dirPath, FileStream videoFileStream)
        {
            this.videoFileStream = videoFileStream;
            cacheFullPath = dirPath;
        }

        protected virtual Stream HandleData(string path) => File.OpenRead(path);

        protected string GetTsPath(M3UMediaInfo m3UMediaInfo)
        {
            return m3UMediaInfo.Uri.IsFile ? m3UMediaInfo.Uri.OriginalString : Path.Combine(cacheFullPath, m3UMediaInfo.Title);
        }

        public virtual void Initialization() { }

        public async ValueTask MegerVideoHeader(M3UMediaInfo? m3UMapInfo)
        {
            if (m3UMapInfo is null)
                return;

            await MegerVideoInternalAsync(m3UMapInfo);
        }

        public async ValueTask Start(M3UFileInfo m3UFileInfo, bool forceMerge)
        {

            for (int i = 0; i < m3UFileInfo.MediaFiles.Count; i++)
            {
                try
                {
                    await MegerVideoInternalAsync(m3UFileInfo.MediaFiles[i]);
                }catch(CryptographicException)
                {
                    throw new CryptographicException("解密失败,请确认key,iv是否正确");
                }
                catch (Exception) when (forceMerge)
                {
                    continue;
                }
            }
       }

        private async ValueTask MegerVideoInternalAsync(M3UMediaInfo m3UMediaInfo)
        {
            string tsFileName = GetTsPath(m3UMediaInfo);
            using Stream tsStream = HandleData(tsFileName);
            await tsStream.CopyToAsync(videoFileStream);
        }

        public void Dispose()
        {
            videoFileStream.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
