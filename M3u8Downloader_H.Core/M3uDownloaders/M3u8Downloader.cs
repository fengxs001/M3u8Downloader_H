﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using M3u8Downloader_H.Core.Utils;
using M3u8Downloader_H.Core.Utils.Extensions;
using M3u8Downloader_H.M3U8.Infos;

namespace M3u8Downloader_H.Core.M3uDownloaders
{
    internal class M3u8Downloader : IM3uDownloader
    {
        private readonly HttpClient http;
        private readonly IEnumerable<KeyValuePair<string, string>>? headers;
        private readonly object balanceLock = new();
        private readonly IProgress<double> progress;
        private int downloadedCount;
        private double recordDuration;
        private int CurIndex = -1;

        public int TimeOut { get; set; } = 5 * 1000;
        public M3u8Downloader(HttpClient http, IEnumerable<KeyValuePair<string, string>>? headers, IProgress<double> progress)
        {
            this.http = http;
            this.headers = headers;
            this.progress = progress;
        }

        public virtual ValueTask Initialization(CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public async ValueTask DownloadMapInfoAsync(M3UMediaInfo? m3UMapInfo, string savePath, bool skipRequestError = false, CancellationToken cancellationToken = default)
        {
            if (m3UMapInfo is null)
                return;

            string mediaPath = Path.Combine(savePath, m3UMapInfo.Title);
            FileInfo fileInfo = new(mediaPath);
            if (fileInfo.Exists && fileInfo.Length > 0)
                return;

            bool isSuccessful = await DownloadAsynInternal(m3UMapInfo.Uri, headers, m3UMapInfo.RangeValue, mediaPath, skipRequestError, cancellationToken);
            if (isSuccessful == false)
                throw new InvalidOperationException($"获取map失败,地址为:{m3UMapInfo.Uri.OriginalString}");
        }

        public async Task Start(M3UFileInfo m3UFileInfo, int TaskNumber, string filePath, int reserve0, bool skipRequestError = false, CancellationToken cancellationToken = default)
        {
            Task[] Tasks = new Task[TaskNumber];
            try
            {
                for (int i = 0; i < TaskNumber; i++)
                {
                    //Tasks[i] = Task.Run(async () => await DownloadCallBack(m3UFileInfo, filePath, headers, skipRequestError, cancellationToken), cancellationToken);
                    Tasks[i] = DownloadCallBack(m3UFileInfo, filePath, headers, skipRequestError, cancellationToken);
                }

                await Task.WhenAll(Tasks);
            }
            finally
            {
                foreach (Task item in Tasks)
                {
                    item?.Dispose();
                }
            }

            if (skipRequestError == false && downloadedCount != m3UFileInfo.MediaFiles.Count)
                throw new DataMisalignedException($"下载数量不完整,完成数{downloadedCount}/{m3UFileInfo.MediaFiles.Count}");
        }


        public async Task<double> Start(M3UFileInfo m3UFileInfo, string savePath, int reserve0, bool skipRequestError = false, CancellationToken cancellationToken = default)
        {
            foreach (var mediaFile in m3UFileInfo.MediaFiles)
            {
                string mediaPath = Path.Combine(savePath, mediaFile.Title);
                bool isSuccessful = await DownloadAsynInternal(mediaFile.Uri, headers, mediaFile.RangeValue, mediaPath, skipRequestError, cancellationToken);
                if(isSuccessful)
                {
                    recordDuration += mediaFile.Duration;
                    progress.Report(recordDuration);
                }
            }
            return recordDuration;
        }


        private async Task DownloadCallBack(M3UFileInfo m3UFileInfo, string savePath, IEnumerable<KeyValuePair<string, string>>? headers, bool skipRequestError, CancellationToken token)
        {
            int localIndex = 0;
            while (true)
            {
                lock (balanceLock)
                {
                    localIndex = ++CurIndex;
                }

                if (localIndex >= m3UFileInfo.MediaFiles.Count)
                {
                    break;
                }

                M3UMediaInfo mediaInfo = m3UFileInfo.MediaFiles[localIndex];
                string mediaPath = Path.Combine(savePath, mediaInfo.Title);
                FileInfo fileInfo = new(mediaPath);
                if (fileInfo.Exists && fileInfo.Length > 0)
                {
                    _ = Interlocked.Increment(ref downloadedCount);
                    continue;
                }


                bool isSuccessful = await DownloadAsynInternal(mediaInfo.Uri, headers, mediaInfo.RangeValue, mediaPath, skipRequestError, token);
                if(isSuccessful)
                {
                    _ = Interlocked.Increment(ref downloadedCount);
                    progress.Report(downloadedCount / (double)m3UFileInfo.MediaFiles.Count);
                }
            }
        }

        protected async Task<bool> DownloadAsynInternal(Uri uri, IEnumerable<KeyValuePair<string, string>>? headers, RangeHeaderValue? rangeHeaderValue, string mediaPath, bool skipRequestError, CancellationToken token)
        {
            bool IsSuccessful = false;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    (Stream tmpstream, string contentType) = await http.GetResponseContentAsync(uri, headers, rangeHeaderValue, token);
                    using Stream stream = DownloadAfter(tmpstream, contentType, token);

                    await WriteToFileAsync(mediaPath, stream, token);
                    IsSuccessful = true;
                    break;
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    await Task.Delay(2000, token);
                    continue;
                }
                catch (AggregateException ex) when(ex.InnerException is not InvalidDataException)
                {
                    if (ex.InnerException is CryptographicException)
                    {
                        throw new CryptographicException("解密失败,请确认key,iv是否正确");
                    }
                    await Task.Delay(2000, token);
                    continue;
                }
                catch (IOException)
                {
                    await Task.Delay(2000, token);
                    continue;
                }
                catch (HttpRequestException) when (skipRequestError)
                {
                    await Task.Delay(2000, token);
                    continue;
                }
            }
            if (!IsSuccessful)
                DeleteFileWhenTimeOut(mediaPath);
            return IsSuccessful;
        }

        //参数传入带类型的值 在其他操作上来判断是否要调用这个基类
        protected virtual Stream DownloadAfter(Stream stream, string contentType, CancellationToken cancellationToken)
        {
            if (contentType.StartsWith("image", StringComparison.CurrentCultureIgnoreCase) || contentType.StartsWith("text", StringComparison.CurrentCultureIgnoreCase))
            {
                HandleImageStream handleImageStream = new(stream, 2000);
                Task t = handleImageStream.InitializePositionAsync(cancellationToken);
                return !t.Wait(TimeOut, cancellationToken) ? throw new IOException() : handleImageStream;
            }
            return stream;
        }

        protected Task WriteToFileAsync(string file, Stream stream, CancellationToken token)
        {
            using FileStream fileobject = File.Create(file);
            Task t = stream.CopyToAsync(fileobject, token);
            return !t.Wait(TimeOut, token) ? throw new IOException() : Task.CompletedTask;
        }

        protected static void DeleteFileWhenTimeOut(string file)
        {
            FileInfo fileInfo = new(file);
            if (fileInfo.Exists && fileInfo.Length > 0)
                fileInfo.Delete();
        }
    }
}
