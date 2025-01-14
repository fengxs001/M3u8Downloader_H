﻿using System;
using System.IO;
using M3u8Downloader_H.M3U8.Infos;

namespace M3u8Downloader_H.M3U8.Adapters
{
    internal class TextAdapter : Adapter
    {
        public string Text { get; }

        public TextAdapter(string text)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
        }

        protected override Stream CreateStream()
        {
            return new MemoryStream(Configuration.Default.Encoding.GetBytes(Text), false);
        }
    }
}