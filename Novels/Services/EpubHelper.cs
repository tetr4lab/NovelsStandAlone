﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AngleSharp.Dom;

namespace QuickEPUB {
    /// <summary>
    /// Helper class for Epub operations.
    /// </summary>
    public static class EpubHelper {
        /// <summary>
        /// Adds a chapter to the book.
        /// </summary>
        /// <param name="book"></param>
        /// <param name="heading1"></param>
        /// <param name="heading2"></param>
        /// <param name="content"></param>
        /// <param name="note"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public static void AddChapter (this Epub book, string? heading1, string? heading2, string? heading3, string? content, string? note = null, string? preface = null) {
            if (string.IsNullOrEmpty (content)) {
                throw new ArgumentNullException (nameof (content));
            }
            if (string.IsNullOrEmpty (heading1) && string.IsNullOrEmpty (heading2) && string.IsNullOrEmpty (heading3)) {
                throw new ArgumentNullException ($"{nameof (heading1)} or {nameof (heading2)} or {nameof (heading3)}");
            }
            var heading = new List<string> ();
            if (!string.IsNullOrEmpty (heading1)) { heading.Add (heading1); }
            if (!string.IsNullOrEmpty (heading2)) { heading.Add (heading2); }
            if (!string.IsNullOrEmpty (heading3)) { heading.Add (heading3); }
            book.AddSection (string.Join ('／', heading), string.Join ('\n', [
                string.IsNullOrEmpty (heading1) ? "" : $"<h1>{heading1}</h1>",
                string.IsNullOrEmpty (heading2) ? "" : $"<h2>{heading2}</h2>",
                string.IsNullOrEmpty (heading3) ? "" : $"<h3>{heading3}</h3>",
                string.IsNullOrEmpty (preface) ? "" : $"<div class=\"preface\">{preface}</div>",
                string.IsNullOrEmpty (content) ? "" : $"<div class=\"chapter-body\">{content}</div>",
                string.IsNullOrEmpty (note) ? "" : $"<div class=\"note\">{note}</div>",
            ]), "book-style.css");
        }
        /// <summary>Adds a title page to the book.</summary>
        /// <param name="book"></param>
        public static void AddTitle (this Epub book) {
            book.AddSection ( "本扉", $"""
                <div class="title-page">
                    <div class="title">{book.Title}</div>
                    <div class="author">{book.Author}</div>
                </div>
                """, "book-style.css");
        }

        /// <summary>指定された画像をリソースに加える</summary>
        /// <param name="doc">EPUB</param>
        /// <param name="imageUri">画像URL</param>
        /// <returns>画像のファイル名</returns>
        public static async Task<string> AddImageResource (this Epub doc, HttpClient HttpClient, Uri imageUri, string userAgent, bool isCover = false) {
            HttpClient.DefaultRequestHeaders.Add ("User-Agent", userAgent);
            using (var response = await HttpClient.GetAsync (imageUri, HttpCompletionOption.ResponseHeadersRead)) {
                response.EnsureSuccessStatusCode (); // HTTPエラーコードが返された場合に例外をスロー
                var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                var resourceType = contentType switch {
                    "image/jpeg" => EpubResourceType.JPEG,
                    "image/png" => EpubResourceType.PNG,
                    "image/gif" => EpubResourceType.GIF,
                    "image/ttf" => EpubResourceType.TTF,
                    "image/otf" => EpubResourceType.OTF,
                    "image/svg+xml" => EpubResourceType.SVG,
                    _ => EpubResourceType.JPEG,
                };
                var fileName = $"img_{Guid.NewGuid ().ToString ("N")}.{resourceType.ToString ().ToLower ()}"; // ユニークな名前を生成
                using (var stream = await response.Content.ReadAsStreamAsync ()) {
                    doc.AddResource (fileName, resourceType, stream, isCover);
                }
                return fileName;
            }
        }

        /// <summary>指定された画像をリソースに加える</summary>
        /// <param name="doc">EPUB</param>
        /// <param name="image">画像</param>
        /// <returns>画像のファイル名</returns>
        public static async Task<string> AddImageResource (this Epub doc, byte [] image, string type, bool isCover = false) {
            var fileName = $"img_{Guid.NewGuid ().ToString ("N")}.{type}"; // ユニークな名前を生成
            using (var stream = new MemoryStream (image)) {
                doc.AddResource (fileName, EpubResourceType.JPEG, stream, isCover);
            }
            return await Task.FromResult (fileName);
        }
    }
}
