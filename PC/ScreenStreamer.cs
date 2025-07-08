using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;
using Stealth.Shared;

namespace Stealth.PC
{
    /// <summary>
    /// Handles screen capture and streaming for the PC
    /// </summary>
    public class ScreenStreamer : IDisposable
    {
        private bool _isStreaming;
        private readonly object _lockObject = new();
        private Task? _streamingTask;
        private CancellationTokenSource? _cancellationTokenSource;

        public event EventHandler<ScreenDataCapturedEventArgs>? ScreenDataCaptured;

        /// <summary>
        /// Starts screen streaming
        /// </summary>
        public void StartStreaming(int frameRate = 30)
        {
            lock (_lockObject)
            {
                if (_isStreaming) return;

                _isStreaming = true;
                _cancellationTokenSource = new CancellationTokenSource();
                
                var delayMs = 1000 / frameRate;
                _streamingTask = Task.Run(() => StreamingLoop(delayMs, _cancellationTokenSource.Token));
            }
        }

        /// <summary>
        /// Stops screen streaming
        /// </summary>
        public void StopStreaming()
        {
            lock (_lockObject)
            {
                if (!_isStreaming) return;

                _isStreaming = false;
                _cancellationTokenSource?.Cancel();
                _streamingTask?.Wait(5000); // Wait up to 5 seconds
            }
        }

        private async Task StreamingLoop(int delayMs, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isStreaming)
            {
                try
                {
                    var screenData = CaptureScreen();
                    if (screenData != null)
                    {
                        ScreenDataCaptured?.Invoke(this, new ScreenDataCapturedEventArgs { ScreenData = screenData });
                    }

                    await Task.Delay(delayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in streaming loop: {ex.Message}");
                    await Task.Delay(1000, cancellationToken); // Wait before retrying
                }
            }
        }

        /// <summary>
        /// Captures the current screen
        /// </summary>
        public ScreenDataMessage? CaptureScreen()
        {
            try
            {
                var bounds = GetScreenBounds();
                using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);

                // Compress to JPEG
                using var stream = new MemoryStream();
                var encoder = GetJpegEncoder();
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 75L); // 75% quality
                
                bitmap.Save(stream, encoder, encoderParams);
                
                return new ScreenDataMessage
                {
                    Width = bounds.Width,
                    Height = bounds.Height,
                    ImageData = stream.ToArray(),
                    CompressionFormat = "JPEG"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturing screen: {ex.Message}");
                return null;
            }
        }

        private static Rectangle GetScreenBounds()
        {
            var left = int.MaxValue;
            var top = int.MaxValue;
            var right = int.MinValue;
            var bottom = int.MinValue;

            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                left = Math.Min(left, screen.Bounds.Left);
                top = Math.Min(top, screen.Bounds.Top);
                right = Math.Max(right, screen.Bounds.Right);
                bottom = Math.Max(bottom, screen.Bounds.Bottom);
            }

            return new Rectangle(left, top, right - left, bottom - top);
        }

        private static ImageCodecInfo GetJpegEncoder()
        {
            var codecs = ImageCodecInfo.GetImageEncoders();
            return codecs.First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
        }

        /// <summary>
        /// Captures a specific region of the screen
        /// </summary>
        public ScreenDataMessage? CaptureScreenRegion(Rectangle region)
        {
            try
            {
                using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format24bppRgb);
                using var graphics = Graphics.FromImage(bitmap);
                
                graphics.CopyFromScreen(region.X, region.Y, 0, 0, region.Size, CopyPixelOperation.SourceCopy);

                using var stream = new MemoryStream();
                var encoder = GetJpegEncoder();
                var encoderParams = new EncoderParameters(1);
                encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 75L);
                
                bitmap.Save(stream, encoder, encoderParams);
                
                return new ScreenDataMessage
                {
                    Width = region.Width,
                    Height = region.Height,
                    ImageData = stream.ToArray(),
                    CompressionFormat = "JPEG"
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error capturing screen region: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            StopStreaming();
            _cancellationTokenSource?.Dispose();
        }
    }

    public class ScreenDataCapturedEventArgs : EventArgs
    {
        public ScreenDataMessage ScreenData { get; set; } = null!;
    }
}
