﻿namespace Firebase.Storage
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    public class FirebaseStorageTask
    {
        private const int ProgressReportDelayMiliseconds = 500;

        private readonly Task<string> uploadTask;
        private readonly Stream stream;

        public FirebaseStorageTask(FirebaseStorageOptions options, string url, string downloadUrl, Stream stream, CancellationToken cancellationToken, TimeSpan timeOutRequest)
        {
            this.TargetUrl = url;
            this.uploadTask = this.UploadFile(options, url, downloadUrl, stream, cancellationToken, timeOutRequest);
            this.stream = stream;
            this.Progress = new Progress<FirebaseStorageProgress>();

            Task.Factory.StartNew(() => ReportProgressLoop());
        }

        public Progress<FirebaseStorageProgress> Progress
        {
            get;
            private set;
        }


        public string TargetUrl
        {
            get;
            private set;
        }

        public TaskAwaiter<string> GetAwaiter()
        {
            return this.uploadTask.GetAwaiter();
        }

        private async Task<string> UploadFile(FirebaseStorageOptions options, string url, string downloadUrl, Stream stream, CancellationToken cancellationToken, TimeSpan timeOutRequest)
        {
            var responseData = "N/A";

            try
            {
                using (var client = await options.CreateHttpClientAsync())
                {
                    client.Timeout = timeOutRequest;
                    var request = new HttpRequestMessage(HttpMethod.Post, url)
                    {
                        Content = new StreamContent(stream)
                    };

                    var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    responseData = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();
                    var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseData);

                    return downloadUrl + data["downloadTokens"];
                }
            }
            catch (TaskCanceledException)
            {
                if (options.ThrowOnCancel)
                {
                    throw;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                throw new FirebaseStorageException(url, responseData, ex);
            }
        }

        private async void ReportProgressLoop()
        {
            while (!this.uploadTask.IsCompleted)
            {
                await Task.Delay(ProgressReportDelayMiliseconds);

                try
                { 
                    this.OnReportProgress(new FirebaseStorageProgress(this.stream.Position, this.stream.Length));
                }
                catch (ObjectDisposedException)
                {
                    // there is no 100 % way to prevent ObjectDisposedException, there are bound to be concurrency issues.
                    return;
                } 
            }
        }

        private void OnReportProgress(FirebaseStorageProgress progress)
        {
            (this.Progress as IProgress<FirebaseStorageProgress>).Report(progress);
        }
    }
}
