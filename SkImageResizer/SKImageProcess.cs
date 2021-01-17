using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SkiaSharp;

namespace SkImageResizer
{
    public class SKImageProcess
    {
        /// <summary>
        /// 進行圖片的縮放作業
        /// </summary>
        /// <param name="sourcePath">圖片來源目錄路徑</param>
        /// <param name="destPath">產生圖片目的目錄路徑</param>
        /// <param name="scale">縮放比例</param>
        public void ResizeImages(string sourcePath, string destPath, double scale, CancellationToken ctsToken)
        {
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            var allFiles = FindImages(sourcePath);
            foreach (var filePath in allFiles)
            {
                ctsToken.ThrowIfCancellationRequested();
                var bitmap = SKBitmap.Decode(filePath);
                var imgPhoto = SKImage.FromBitmap(bitmap);
                var imgName = Path.GetFileNameWithoutExtension(filePath);

                var sourceWidth = imgPhoto.Width;
                var sourceHeight = imgPhoto.Height;

                var destinationWidth = (int)(sourceWidth * scale);
                var destinationHeight = (int)(sourceHeight * scale);

                ctsToken.ThrowIfCancellationRequested();
                using var scaledBitmap = bitmap.Resize(
                    new SKImageInfo(destinationWidth, destinationHeight),
                    SKFilterQuality.High);
                using var scaledImage = SKImage.FromBitmap(scaledBitmap);
                using var data = scaledImage.Encode(SKEncodedImageFormat.Jpeg, 100);
                using var s = File.OpenWrite(Path.Combine(destPath, imgName + ".jpg"));
                data.SaveTo(s);
            }
        }

        /*
        // 讓應用端給個非同步方法
        // 但其實內部也沒非同步=>也沒能變快
        public Task ResizeImages2Async(string sourcePath, string destPath, double scale)
        {
            return Task.Run(() => ResizeImages(sourcePath, destPath, scale));
        }

        // 沒必要的寫法
        // 裝套件避免這樣寫!
        public async Task ResizeImages3Async(string sourcePath, string destPath, double scale)
        {
            await Task.Run(() => ResizeImages(sourcePath, destPath, scale));
        }

        // 這根本沒用到非同步
        // 還是一條Thread跑天下
        public Task ResizeImages4Async(string sourcePath, string destPath, double scale)
        {
            ResizeImages(sourcePath, destPath, scale);
            return Task.CompletedTask;
        }
        */

        // 內容也非同步
        // 讚讚
        public Task ResizeImagesAsync(string sourcePath, string destPath, double scale, CancellationToken ctsToken)
        {
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }

            var allFiles = FindImages(sourcePath);
            var tasks = new List<Task>();
            foreach (var filePath in allFiles)
            {
                tasks.Add(ProcessImageAsync(filePath, destPath, scale, ctsToken));
            }
            return Task.WhenAll(tasks);
        }

        public static Task ProcessImageAsync(string filePath, string destPath, double scale, CancellationToken ctsToken)
        {
            return Task.Run(async () =>
            {
                var imgName = Path.GetFileNameWithoutExtension(filePath);

                var task1 = Task.Run(() =>
                {
                    var bitmap = SKBitmap.Decode(filePath);
                    var imgPhoto = SKImage.FromBitmap(bitmap);

                    var sourceWidth = imgPhoto.Width;
                    var sourceHeight = imgPhoto.Height;

                    var destinationWidth = (int)(sourceWidth * scale);
                    var destinationHeight = (int)(sourceHeight * scale);

                    using var scaledBitmap = bitmap.Resize(
                        new SKImageInfo(destinationWidth, destinationHeight),
                        SKFilterQuality.High);
                    using var scaledImage = SKImage.FromBitmap(scaledBitmap);
                    return scaledImage.Encode(SKEncodedImageFormat.Jpeg, 100);
                });
                ctsToken.ThrowIfCancellationRequested();
                var task2 = Task.Run(() =>
                {
                    return File.OpenWrite(Path.Combine(destPath, imgName + ".jpg"));
                });
                ctsToken.ThrowIfCancellationRequested();
                List<Task> tasks = new List<Task>();
                tasks.Add(task1);
                tasks.Add(task2);
                await Task.WhenAll(tasks);
                using var data = task1.Result;
                using var s = task2.Result;
                data.SaveTo(s);
                ctsToken.ThrowIfCancellationRequested();
            });
        }

        /// <summary>
        /// 清空目的目錄下的所有檔案與目錄
        /// </summary>
        /// <param name="destPath">目錄路徑</param>
        public void Clean(string destPath)
        {
            if (!Directory.Exists(destPath))
            {
                Directory.CreateDirectory(destPath);
            }
            else
            {
                var allImageFiles = Directory.GetFiles(destPath, "*", SearchOption.AllDirectories);

                foreach (var item in allImageFiles)
                {
                    File.Delete(item);
                }
            }
        }

        /// <summary>
        /// 找出指定目錄下的圖片
        /// </summary>
        /// <param name="srcPath">圖片來源目錄路徑</param>
        /// <returns></returns>
        public List<string> FindImages(string srcPath)
        {
            List<string> files = new List<string>();
            files.AddRange(Directory.GetFiles(srcPath, "*.png", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(srcPath, "*.jpg", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles(srcPath, "*.jpeg", SearchOption.AllDirectories));
            return files;
        }
    }
}