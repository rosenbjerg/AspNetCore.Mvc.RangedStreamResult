using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace AspNetCore.Mvc.RangedStream
{
    public class RangedStreamResult : IActionResult
    {
        private static readonly FileExtensionContentTypeProvider FileExtensionContentTypeProvider = new FileExtensionContentTypeProvider();
        private readonly Func<long, long?, Task<(Stream?, long Length, string Filename, string? ContentType)>> _streamFunc;
        private readonly bool _attachment;

        public RangedStreamResult(Func<long, long?, Task<(Stream? Stream, long FullLength, string Filename, string? ContentType)>> streamFunc, bool attachment = false)
        {
            _streamFunc = streamFunc;
            _attachment = attachment;
        }
        public RangedStreamResult(Func<long, long?, Task<(Stream? Stream, long FullLength, string Filename)>> streamFunc, bool attachment = false)
        {
            _streamFunc = (from, to) => streamFunc(from, to).ContinueWith(task => (task.Result.Stream, task.Result.FullLength, task.Result.Filename, default(string?)), TaskContinuationOptions.OnlyOnRanToCompletion);
            _attachment = attachment;
        }
        public RangedStreamResult(Func<long, long?, Task<(Stream? Stream, long FullLength)>> streamFunc, string filename, string? contentType = null, bool attachment = false)
        {
            _streamFunc = (from, to) => streamFunc(from, to).ContinueWith(task => (task.Result.Stream, task.Result.FullLength, filename, contentType), TaskContinuationOptions.OnlyOnRanToCompletion);
            _attachment = attachment;
        }
        public RangedStreamResult(Func<long, long?, Task<Stream?>> streamFunc, long fullLength, string filename, string? contentType = null, bool attachment = false)
        {
            _streamFunc = (from, to) => streamFunc(from, to).ContinueWith(task => (task.Result, fullLength, filename, contentType), TaskContinuationOptions.OnlyOnRanToCompletion);
            _attachment = attachment;
        }

        public RangedStreamResult(Func<long, long?, (Stream? Stream, long FullLength, string Filename, string? ContentType)> streamFunc, bool attachment = false)
        {
            _streamFunc = (from, to) =>
            {
                var (stream, fullLength, filename, contentType) = streamFunc(from, to);
                return Task.FromResult((stream, fullLength, filename, contentType));
            };
            _attachment = attachment;
        }
        public RangedStreamResult(Func<long, long?, (Stream? Stream, long FullLength)> streamFunc, string filename, string? contentType = null, bool attachment = false)
        {
            _streamFunc = (from, to) =>
            {
                var (stream, fullLength) = streamFunc(from, to);
                return Task.FromResult((stream, fullLength, filename, contentType));
            };
            _attachment = attachment;
        }
        public RangedStreamResult(Func<long, long?, Stream?> streamFunc, long fullLength, string filename, string? contentType = null, bool attachment = false)
        {
            _streamFunc = (from, to) => Task.FromResult((streamFunc(from, to), fullLength, filename, contentType));
            _attachment = attachment;
        }
        
        public async Task ExecuteResultAsync(ActionContext context)
        {
            var requestedRange = context.HttpContext.Request.GetTypedHeaders().Range?.Ranges.FirstOrDefault();
            var ranged = (requestedRange?.From != null && requestedRange.From.Value > 0) || requestedRange?.To != null;
            var (stream, fullLength, filename, contentType) = await _streamFunc(requestedRange?.From ?? 0, requestedRange?.To);

            if (stream == null)
            {
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            
            try
            {
                if (requestedRange?.To != null && requestedRange.To > fullLength)
                {
                    context.HttpContext.Response.StatusCode = (int)HttpStatusCode.RequestedRangeNotSatisfiable;
                }
                else
                {
                    var from = requestedRange?.From ?? 0;
                    var to = requestedRange?.To ?? fullLength - 1;
                    context.HttpContext.Response.StatusCode = (int)(ranged ? HttpStatusCode.PartialContent : HttpStatusCode.OK);
                    context.HttpContext.Response.ContentLength = to - from + 1;
                    context.HttpContext.Response.Headers["Accept-Ranges"] = "bytes";
                    context.HttpContext.Response.Headers["Content-Type"] = DetermineContentType(filename, contentType);
                    context.HttpContext.Response.Headers["Content-Range"] = ranged ? $"bytes {from}-{to}/{fullLength}" : null;
                    if (from != 0)
                    {
                        Console.WriteLine("do");
                    }
                    context.HttpContext.Response.Headers["Content-Disposition"] = $"{(_attachment ? "attachment" : "inline")}; filename=\"{WebUtility.UrlEncode(filename)}\"";

                    await using (stream)
                        await stream.CopyToAsync(context.HttpContext.Response.Body);
                }
            }
            finally
            {
                await stream.DisposeAsync();
            }
        }

        private static string DetermineContentType(string filename, string? contentType)
        {
            if (!string.IsNullOrWhiteSpace(contentType))
                return contentType;
            if (FileExtensionContentTypeProvider.TryGetContentType(filename, out var m))
                return m;
            return "application/octet-stream";
        }
    }
}