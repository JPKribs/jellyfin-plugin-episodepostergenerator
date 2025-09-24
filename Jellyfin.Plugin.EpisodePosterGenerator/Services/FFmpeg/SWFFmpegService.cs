using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.EpisodePosterGenerator.Models;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services.FFmpeg
{
    public class SWFFmpegService : IFFmpegService
    {
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private bool _disposed;

        public async Task<string?> ExtractFrameAsync(string videoPath, TimeSpan timestamp, string outputPath, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var args = $"-ss {timestamp.TotalSeconds.ToString(CultureInfo.InvariantCulture)} -i \"{videoPath}\" -frames:v 1 \"{outputPath}\"";
                await ExecuteProcessAsync("ffmpeg", args, cancellationToken).ConfigureAwait(false);
                return outputPath;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<List<BlackInterval>> DetectBlackScenesAsync(string videoPath, TimeSpan totalDuration, double pixelThreshold = 0.1, double durationThreshold = 0.1, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var intervals = new List<BlackInterval>();
                string args = $"-i \"{videoPath}\" -vf blackdetect=d={durationThreshold}:pix_th={pixelThreshold} -an -f null -";
                string output = await ExecuteProcessAsync("ffmpeg", args, cancellationToken).ConfigureAwait(false);

                var matches = Regex.Matches(output, @"black_start:(\d+(\.\d+)?)\s+black_end:(\d+(\.\d+)?)");
                foreach (Match match in matches)
                {
                    var start = TimeSpan.FromSeconds(double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));
                    var end = TimeSpan.FromSeconds(double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
                    intervals.Add(new BlackInterval { Start = start, End = end });
                }

                return intervals;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<TimeSpan?> GetVideoDurationAsync(string videoPath, CancellationToken cancellationToken = default)
        {
            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                string args = $"-i \"{videoPath}\"";
                string output = await ExecuteProcessAsync("ffprobe", args, cancellationToken).ConfigureAwait(false);
                var match = Regex.Match(output, @"Duration: (\d+):(\d+):(\d+(\.\d+)?)");
                if (!match.Success) return null;

                int hours = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                int minutes = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                double seconds = double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
                return new TimeSpan(0, hours, minutes, 0).Add(TimeSpan.FromSeconds(seconds));
            }
            finally
            {
                _semaphore.Release();
            }
        }

        protected virtual async Task<string> ExecuteProcessAsync(string exe, string args, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<string>();
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.Start();

            string output = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return output;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _semaphore.Dispose();
            }
            _disposed = true;
        }
    }
}