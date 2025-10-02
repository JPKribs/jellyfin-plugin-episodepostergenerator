using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.EpisodePosterGenerator.Services
{
    public class HardwareValidationService : IDisposable
    {
        private readonly ILogger<HardwareValidationService> _logger;
        private readonly Dictionary<HardwareAccelerationType, bool> _validationCache = new();
        private readonly SemaphoreSlim _cacheLock = new(1, 1);
        private bool _disposed;

        public HardwareValidationService(ILogger<HardwareValidationService> logger)
        {
            _logger = logger;
        }

        // MARK: ValidateHardwareAcceleration
        public async Task<bool> ValidateHardwareAcceleration(
            HardwareAccelerationType hwAccel,
            EncodingOptions encodingOptions,
            CancellationToken cancellationToken = default)
        {
            if (hwAccel == HardwareAccelerationType.none)
                return false;

            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (_validationCache.TryGetValue(hwAccel, out var cached))
                    return cached;

                var isValid = await TestHardwareDevice(hwAccel, encodingOptions, cancellationToken).ConfigureAwait(false);
                _validationCache[hwAccel] = isValid;
                return isValid;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        // MARK: TestHardwareDevice
        private async Task<bool> TestHardwareDevice(
            HardwareAccelerationType hwAccel,
            EncodingOptions encodingOptions,
            CancellationToken cancellationToken)
        {
            var ffmpegPath = string.IsNullOrWhiteSpace(encodingOptions.EncoderAppPath)
                ? "ffmpeg"
                : encodingOptions.EncoderAppPath;

            var initArgs = GetHardwareInitArgs(hwAccel);
            if (string.IsNullOrEmpty(initArgs))
                return false;

            var testArgs = $"{initArgs} -f lavfi -i nullsrc=s=64x64:d=0.1 -frames:v 1 -f null -";

            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = testArgs,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardError = true,
                    }
                };

                process.Start();
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

                if (process.ExitCode == 0)
                {
                    _logger.LogInformation("Hardware validation passed for {HwAccel}", hwAccel);
                    return true;
                }

                _logger.LogWarning("Hardware validation failed for {HwAccel}: {Error}", hwAccel, stderr);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hardware validation threw exception for {HwAccel}", hwAccel);
                return false;
            }
        }

        // MARK: GetHardwareInitArgs
        private string GetHardwareInitArgs(HardwareAccelerationType hwAccel)
        {
            return hwAccel switch
            {
                HardwareAccelerationType.qsv => "-init_hw_device qsv=hw",
                HardwareAccelerationType.nvenc => "-init_hw_device cuda=cu:0",
                HardwareAccelerationType.amf => "-init_hw_device opencl=ocl",
                HardwareAccelerationType.vaapi => "-init_hw_device vaapi=va:/dev/dri/renderD128",
                HardwareAccelerationType.videotoolbox => "-init_hw_device videotoolbox=vt",
                _ => string.Empty
            };
        }

        // MARK: ClearCache
        public async Task ClearCache()
        {
            await _cacheLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _validationCache.Clear();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        // MARK: Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _cacheLock?.Dispose();
            }

            _disposed = true;
        }
    }
}