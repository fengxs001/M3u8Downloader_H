﻿using MaterialDesignThemes.Wpf;
using Stylet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using M3u8Downloader_H.Exceptions;
using M3u8Downloader_H.Models;
using M3u8Downloader_H.Services;
using M3u8Downloader_H.Utils;
using M3u8Downloader_H.ViewModels.FrameWork;
using M3u8Downloader_H.M3U8.Infos;

namespace M3u8Downloader_H.ViewModels
{
    public class MainWindowViewModel : Screen
    {
        private readonly IVIewModelFactory viewModelFactory;
        private readonly DialogManager dialogManager;
        private readonly SettingsService settingsService;
        private readonly HttpListenService httpListenService;
        private readonly PluginService pluginService;

        public ISnackbarMessageQueue Notifications { get; } = new SnackbarMessageQueue(TimeSpan.FromSeconds(5));
        public BindableCollection<DownloadViewModel> Downloads { get; } = new BindableCollection<DownloadViewModel>();
        public VideoDownloadInfo VideoDownloadInfo { get; } = new VideoDownloadInfo();

        public string[] Methods { get; } = { "AES-128", "AES-192", "AES-256" };
        public bool IsBusy { get; private set; }

        public bool IsShowDialog { get; private set; }

        public MainWindowViewModel(IVIewModelFactory viewModelFactory, DialogManager dialogManager, SettingsService settingsService,HttpListenService httpListenService ,PluginService pluginService)
        {
            this.viewModelFactory = viewModelFactory;
            this.dialogManager = dialogManager;
            this.settingsService = settingsService;
            this.httpListenService = httpListenService;
            this.pluginService = pluginService;
        }

        protected override void OnViewLoaded()
        {
            base.OnViewLoaded();
            DisplayName = $"m3u8视频下载器 v{App.VersionString} by:Harlan";

            _ = Task.Run(() =>
            {
                settingsService.Load();
                settingsService.ServiceUpdate();

                pluginService.Load();
                for (int i = 65432; i > 1024; i--)
                {
                    try
                    {
                        httpListenService.Run($"http://+:{i}/");
                        httpListenService.Initialization(ProcessDownload, ProcessDownload, ProcessDownload);
                        Notifications.Enqueue($"http服务初始化成功\n监听在 {i} 端口");
                        break;
                    }
                    catch (HttpListenerException)
                    {
                        continue;
                    }
                }
            });
        }

        protected override void OnClose()
        {
            base.OnClose();

            foreach (var item in Downloads)
                item.OnCancel();
            
            settingsService.Save();
        }


        private void EnqueueDownload(DownloadViewModel download)
        {
            var existingDownloads = Downloads.Where(d => d.VideoName == download.VideoName).ToArray();
            foreach (var existingDownload in existingDownloads)
            {
                existingDownload.OnCancel();
                Downloads.Remove(existingDownload);
            }

            download.OnStart();
            Downloads.Insert(0, download);
        }


        public bool CanProcessDownload => !IsBusy;
        public void ProcessDownload(VideoDownloadInfo obj)
        {
            IsBusy = true;
            
            try
            {
                Uri uri = new(obj.RequestUrl, UriKind.Absolute);
                if(uri.IsFile)
                {
                    string ext = Path.GetExtension(uri.OriginalString).Trim('.');
                    switch (ext)
                    {
                        case "txt":
                            HandleTxt(uri);
                            break;
                        case "":
                            FileAttributes fileAttribute = File.GetAttributes(uri.OriginalString);
                            if (fileAttribute != FileAttributes.Directory)
                                throw new InvalidOperationException("请确认是否为文件夹");

                            ProcessDownload(uri, obj.VideoName, obj.Method, obj.Key, obj.Iv);
                            break;
                        case "json":
                        case "xml":
                        case "m3u8":
                            ProcessDownload(uri, obj.VideoName, obj.Method, obj.Key, obj.Iv);
                            break;
                        default:
                            throw new InvalidOperationException("请确认是否为.m3u8或.txt或.json或.xml或文件夹");
                    }
                }
                else
                {
                    ProcessDownload(uri, obj.VideoName,obj.Method,obj.Key,obj.Iv);
                }

                //只有操作成功才会清空
                if (settingsService.IsResetAddress) obj.RequestUrl = string.Empty;
                if (settingsService.IsResetName) obj.VideoName = string.Empty;
                obj.Key = null;
                obj.Method = "AES-128";
                obj.Iv = null;

            }
            catch (Exception e)
            {
                Notifications.Enqueue(e.ToString());
            }
            finally 
            {
                IsBusy = false;
            }
        }

        private void HandleTxt(Uri uri)
        {
            foreach (var item in File.ReadLines(uri.OriginalString))
            {
                if (string.IsNullOrWhiteSpace(item)) continue;

                string[] result = item.Trim().Split(settingsService.Separator, 2);
                ProcessDownload(result[0], result.Length > 1 ? result[1] : null);
            }
        }

        private void ProcessDownload(string url, string? name)
        {
            try
            {
                ProcessDownload(new Uri(url, UriKind.Absolute), name,null,null,null);
            }
            catch(UriFormatException)
            {
                Notifications.Enqueue($"{url} 不是正确的地址");
            }
            catch(FileExistsException e)
            {
                Notifications.Enqueue(e);
            }
        }

        private void ProcessDownload(Uri uri, string? name,string? method,string? key,string? iv, string? savePath = default, IEnumerable<KeyValuePair<string,string>>? headers = default)
        {
            string tmpVideoName = PathEx.GenerateFileNameWithoutExtension(name);
            string fileFullPath = Path.Combine(savePath ?? settingsService.SavePath, tmpVideoName);
            FileEx.EnsureFileNotExist(fileFullPath);

            DownloadViewModel download = viewModelFactory.CreateDownloadViewModel(uri, tmpVideoName,method,key,iv, headers, fileFullPath);
            if (download is null) return;

            EnqueueDownload(download);
        }

        private void ProcessDownload(string content, Uri? uri, string? name, string? savePath, IEnumerable<KeyValuePair<string, string>>? headers)
        {
            string tmpVideoName = PathEx.GenerateFileNameWithoutExtension(name);
            string fileFullPath = Path.Combine(savePath ?? settingsService.SavePath, tmpVideoName);
            FileEx.EnsureFileNotExist(fileFullPath);

            DownloadViewModel download = viewModelFactory.CreateDownloadViewModel(uri, content, headers, fileFullPath, tmpVideoName);
            if (download is null) return;

            EnqueueDownload(download);
        }

        private void ProcessDownload(M3UFileInfo m3UFileInfo, string? name, string? savePath = default!, IEnumerable<KeyValuePair<string, string>>? headers = default!)
        {
            if (!m3UFileInfo.MediaFiles.Any())
                throw new ArgumentException("m3u8的数据不能为空");

            string tmpVideoName = PathEx.GenerateFileNameWithoutExtension(name);
            string fileFullPath = Path.Combine(savePath ?? settingsService.SavePath, tmpVideoName);
            FileEx.EnsureFileNotExist(fileFullPath);

            if(string.IsNullOrEmpty(m3UFileInfo.PlaylistType)) m3UFileInfo.PlaylistType = "VOD";
            DownloadViewModel download = viewModelFactory.CreateDownloadViewModel(m3UFileInfo, headers, tmpVideoName, fileFullPath);
            if (download is null) return;

            EnqueueDownload(download);
        }

        public void RemoveDownload(DownloadViewModel download)
        {
            download.OnCancel();
            Downloads.Remove(download);
        }

        public void RemoveInactiveDownloads()
        {
            var inactiveDownloads = Downloads.Where(c => !c.IsActive).ToArray();
            Downloads.RemoveRange(inactiveDownloads);
        }

        public void RemoveSuccessDownloads()
        {
            var successDownloads = Downloads.Where(c => c.Status == DownloadStatus.Completed).ToArray();
            Downloads.RemoveRange(successDownloads);
        }

        public void RemoveFailedDownloads()
        {
            var failedDownloads = Downloads.Where(c => c.Status == DownloadStatus.Failed).ToArray();
            Downloads.RemoveRange(failedDownloads);
        }

        public void RestartFailedDownloads()
        {
            var failedDownloads = Downloads.Where(c => c.Status == DownloadStatus.Failed);
            foreach (var failedDownload in failedDownloads)
                failedDownload.OnRestart();
        }

        public void CopyUrl(DownloadViewModel download) => Clipboard.SetText(download.RequestUrl.OriginalString);

        public void CopyTitle(DownloadViewModel download) => Clipboard.SetText(download.VideoName);

        public void CopyFailReason(DownloadViewModel download) => Clipboard.SetText(download.FailReason);

        public bool CanShowSettings => !IsShowDialog;

        public async void ShowSettings()
        {
            IsShowDialog = true;
            try
            {
                var dialog = viewModelFactory.CreateSettingsViewModel();
                dialog.PluginItems = pluginService.GetPluginItem();
                await dialogManager.ShowDialogAsync(dialog);
            }
            finally
            {
                IsShowDialog = false;
            }
        }
    }
}
