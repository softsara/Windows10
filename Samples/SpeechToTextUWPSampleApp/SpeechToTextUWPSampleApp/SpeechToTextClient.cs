﻿//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************
using System;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.IO;

namespace SpeechToTextUWPSampleApp
{
    /// <summary>
    /// class SpeechToTextClient: SpeechToText UWP Client
    /// </summary>
    /// <info>
    /// Event data that describes how this page was reached.
    /// This parameter is typically used to configure the page.
    /// </info>
    public class SpeechToTextClient
    {
        private string SubscriptionKey;
        private string Token;
        private SpeechToTextStream STTStream;
        private const string AuthUrl = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken";
        private const string SpeechUrl = "https://speech.platform.bing.com/recognize";

        private bool isRecordingInitialized;
        private bool isRecording;
        private Windows.Media.Capture.MediaCapture mediaCapture;

        /// <summary>
        /// class SpeechToTextClient constructor
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.
        /// This parameter is typically used to configure the page.</param>
        public SpeechToTextClient()
        {
            SubscriptionKey = string.Empty;
            Token = string.Empty;
            isRecordingInitialized = false;
            isRecording = false;
        }

        /// <summary>
        /// GetToken method
        /// </summary>
        /// <param name="subscriptionKey">SubscriptionKey associated with the SpeechToText 
        /// Cognitive Service subscription.
        /// </param>
        /// <return>Token which is used to all the SpeechToText REST API.
        /// </return>
        public async System.Threading.Tasks.Task<string> GetToken(string subscriptionKey )
        {
            if (string.IsNullOrEmpty(subscriptionKey))
                return string.Empty;
            SubscriptionKey = subscriptionKey;
            try
            {
                Token = string.Empty;
                Windows.Web.Http.HttpClient hc = new Windows.Web.Http.HttpClient();
                hc.DefaultRequestHeaders.TryAppendWithoutValidation("Ocp-Apim-Subscription-Key", SubscriptionKey);
                Windows.Web.Http.HttpStringContent content = new Windows.Web.Http.HttpStringContent(String.Empty);
                Windows.Web.Http.HttpResponseMessage hrm = await hc.PostAsync(new Uri(AuthUrl), content);
                if (hrm != null)
                {
                    switch (hrm.StatusCode)
                    {
                        case Windows.Web.Http.HttpStatusCode.Ok:
                            var b = await hrm.Content.ReadAsBufferAsync();
                            string result = System.Text.UTF8Encoding.UTF8.GetString(b.ToArray());
                            if (!string.IsNullOrEmpty(result))
                            {
                                Token = "Bearer  " + result;
                                return Token;
                            }
                            break;

                        default:
                            System.Diagnostics.Debug.WriteLine("Http Response Error:" + hrm.StatusCode.ToString() + " reason: " + hrm.ReasonPhrase.ToString());
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception while getting the token: " + ex.Message);
            }
            return string.Empty;
        }
        /// <summary>
        /// HasToken method
        /// </summary>
        /// <param>Check if a Token has been acquired
        /// </param>
        /// <return>true if a Token has been acquired to use the SpeechToText REST API.
        /// </return>
        public bool HasToken()
        {
            if (string.IsNullOrEmpty(Token))
                return false;
            return true;
        }

        /// <summary>
        /// SendBuffer method
        /// </summary>
        /// <param name="locale">language associated with the current buffer/recording.
        /// for instance en-US, fr-FR, pt-BR, ...
        /// </param>
        /// <return>The result of the SpeechToText REST API.
        /// </return>
        public async System.Threading.Tasks.Task<SpeechToTextResponse> SendBuffer(string locale)
        {
            try
            {
                string os = "Windows" + Information.SystemInformation.SystemVersion;
                string deviceid = "b2c95ede-97eb-4c88-81e4-80f32d6aee54";
                string speechUrl = SpeechUrl + "?scenarios=ulm&appid=D4D52672-91D7-4C74-8AD8-42B1D98141A5&version=3.0&device.os=" + os + "&locale=" + locale + "&format=json&requestid=" + Guid.NewGuid().ToString() + "&instanceid=" + deviceid + "&result.profanitymarkup=1&maxnbest=3";
                Windows.Web.Http.HttpClient hc = new Windows.Web.Http.HttpClient();
                System.Threading.CancellationTokenSource cts = new System.Threading.CancellationTokenSource();
                hc.DefaultRequestHeaders.TryAppendWithoutValidation("Authorization", Token);
                Windows.Web.Http.HttpResponseMessage hrm = null;
                Windows.Web.Http.HttpStreamContent content;
                if (STTStream != null)
                {
                    content = new Windows.Web.Http.HttpStreamContent(STTStream.AsStream().AsInputStream());
                    content.Headers.ContentLength = STTStream.GetLength();
                    System.Diagnostics.Debug.WriteLine("REST API Post Content Length: " + content.Headers.ContentLength.ToString());
                    content.Headers.TryAppendWithoutValidation("ContentType", "audio/wav; codec=\"audio/pcm\"; samplerate=16000");
                    IProgress<Windows.Web.Http.HttpProgress> progress = new Progress<Windows.Web.Http.HttpProgress>(ProgressHandler);
                    hrm = await hc.PostAsync(new Uri(speechUrl), content).AsTask(cts.Token, progress);
                }
                if (hrm != null)
                {
                    SpeechToTextResponse r = null;
                    switch (hrm.StatusCode)
                    {
                        case Windows.Web.Http.HttpStatusCode.Ok:
                            var b = await hrm.Content.ReadAsBufferAsync();
                            string result = System.Text.UTF8Encoding.UTF8.GetString(b.ToArray());
                            if (!string.IsNullOrEmpty(result))
                                r = new SpeechToTextResponse(result);
                            break;

                        default:
                            int code = (int)hrm.StatusCode;
                            string HttpError = "Http Response Error: " + code.ToString() + " reason: " + hrm.ReasonPhrase.ToString();
                            System.Diagnostics.Debug.WriteLine(HttpError);
                            r = new SpeechToTextResponse(string.Empty, HttpError);
                            break;
                    }
                    return r;
                }
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("http POST canceled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("http POST exception: " + ex.Message);
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine("http POST done" );
            }
            return null;
        }
        /// <summary>
        /// SendStorageFile method
        /// </summary>
        /// <param name="wavFile">StorageFile associated with the audio file which 
        /// will be sent to the SpeechToText Services.
        /// </param>
        /// <param name="locale">language associated with the current buffer/recording.
        /// for instance en-US, fr-FR, pt-BR, ...
        /// </param>
        /// <return>The result of the SpeechToText REST API.
        /// </return>
        public async System.Threading.Tasks.Task<SpeechToTextResponse> SendStorageFile(Windows.Storage.StorageFile wavFile, string locale)
        {
            try
            {
                string os = "Windows" + Information.SystemInformation.SystemVersion;
                string deviceid = "b2c95ede-97eb-4c88-81e4-80f32d6aee54";
                string speechUrl = SpeechUrl + "?scenarios=ulm&appid=D4D52672-91D7-4C74-8AD8-42B1D98141A5&version=3.0&device.os=" + os + "&locale=" + locale + "&format=json&requestid=" + Guid.NewGuid().ToString() + "&instanceid=" + deviceid + "&result.profanitymarkup=1&maxnbest=3";
                Windows.Web.Http.HttpClient hc = new Windows.Web.Http.HttpClient();

                hc.DefaultRequestHeaders.TryAppendWithoutValidation("Authorization", Token);
                hc.DefaultRequestHeaders.TryAppendWithoutValidation("ContentType", "audio/wav; codec=\"audio/pcm\"; samplerate=16000");
                Windows.Web.Http.HttpResponseMessage hrm = null;

                Windows.Storage.StorageFile file = wavFile;
                if (file != null)
                {
                    using (var fileStream = await file.OpenAsync(Windows.Storage.FileAccessMode.Read))
                    {
                        if (STTStream != null)
                        {
                            STTStream.AudioLevel -= STTStream_AudioLevel;
                            STTStream.Dispose();
                            STTStream = null;
                        }
                        STTStream = SpeechToTextStream.Create();
                        if (STTStream != null)
                        {
                            byte[] byteArray = new byte[fileStream.Size];
                            fileStream.ReadAsync(byteArray.AsBuffer(), (uint)fileStream.Size, Windows.Storage.Streams.InputStreamOptions.Partial).AsTask().Wait();
                            STTStream.WriteAsync(byteArray.AsBuffer()).AsTask().Wait();

                            Windows.Web.Http.HttpStreamContent content = new Windows.Web.Http.HttpStreamContent(STTStream.AsStream().AsInputStream());
                            content.Headers.ContentLength = STTStream.GetLength();
                            System.Diagnostics.Debug.WriteLine("REST API Post Content Length: " + content.Headers.ContentLength.ToString() + " bytes");
                            System.Threading.CancellationTokenSource cts = new System.Threading.CancellationTokenSource();
                            IProgress<Windows.Web.Http.HttpProgress> progress = new Progress<Windows.Web.Http.HttpProgress>(ProgressHandler);
                            hrm = await hc.PostAsync(new Uri(speechUrl), content).AsTask(cts.Token, progress);
                        }
                    }
                }
                if (hrm != null)
                {
                    SpeechToTextResponse r = null;
                    switch (hrm.StatusCode)
                    {
                        case Windows.Web.Http.HttpStatusCode.Ok:
                            var b = await hrm.Content.ReadAsBufferAsync();
                            string result = System.Text.UTF8Encoding.UTF8.GetString(b.ToArray());
                            if (!string.IsNullOrEmpty(result))
                                r = new SpeechToTextResponse(result);
                            break;

                        default:
                            int code = (int)hrm.StatusCode;
                            string HttpError = "Http Response Error: " + code.ToString() + " reason: " + hrm.ReasonPhrase.ToString();
                            System.Diagnostics.Debug.WriteLine(HttpError);
                            r = new SpeechToTextResponse(string.Empty, HttpError);
                            break;
                    }
                    return r;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Exception while sending the audio file:" + ex.Message);
            }
            return null;
        }
        /// <summary>
        /// SaveBuffer method
        /// </summary>
        /// <param name="wavFile">StorageFile where the audio buffer 
        /// will be stored.
        /// </param>
        /// <return>true if successful.
        /// </return>
        public async System.Threading.Tasks.Task<bool> SaveBuffer(Windows.Storage.StorageFile wavFile)
        {
            bool bResult = false;
            if (wavFile != null)
            {
                try
                {
                    using (Stream stream = await wavFile.OpenStreamForWriteAsync())
                    {
                        if ((stream != null) && (STTStream != null))
                        {
                            stream.SetLength(0);
                            await STTStream.AsStream().CopyToAsync(stream);
                            System.Diagnostics.Debug.WriteLine("Audio Stream stored in: " + wavFile.Path);
                            bResult = true;
                        }
                    }
                }
                catch(Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Exception while saving the Audio Stream stored in: " + wavFile.Path + " Exception: " + ex.Message);

                }
            }
            return bResult;
        }
        /// <summary>
        /// IsRecording method
        /// </summary>
        /// <param>Return the length of the audio buffer
        /// </param>
        /// <return>the length of the WAV buffer in uint.
        /// </return>
        public bool IsRecording()
        {
            return isRecording;
        }
        /// <summary>
                 /// GetBufferLength method
                 /// </summary>
                 /// <param>Return the length of the audio buffer
                 /// </param>
                 /// <return>the length of the WAV buffer in uint.
                 /// </return>
        public uint GetBufferLength()
        {
            if (STTStream != null)
            {
                return STTStream.GetLength();
            }
            return 0;
        }
        /// <summary>
        /// StartRecording method
        /// </summary>
        /// <param>
        /// Start to record audio using the microphone.
        /// The audio stream in stored in memory
        /// </param>
        /// <return>return true if successful.
        /// </return>
        public async System.Threading.Tasks.Task<bool> StartRecording()
        {
            bool bResult = false;
            if (isRecordingInitialized != true)
                await InitializeRecording();
            if(STTStream != null)
            {
                STTStream.AudioLevel -= STTStream_AudioLevel;
                STTStream.Dispose();
                STTStream = null;
            }
            STTStream = SpeechToTextStream.Create();
            STTStream.AudioLevel += STTStream_AudioLevel;

            if ((STTStream != null) && (isRecordingInitialized == true))
            {
                try
                {
                    Windows.Media.MediaProperties.MediaEncodingProfile MEP = Windows.Media.MediaProperties.MediaEncodingProfile.CreateWav(Windows.Media.MediaProperties.AudioEncodingQuality.Auto);
                    if (MEP != null)
                    {
                        if (MEP.Audio != null)
                        {
                            uint framerate = 16000;
                            uint bitsPerSample = 16;
                            uint numChannels = 1;
                            uint bytespersecond = 32000;
                            MEP.Audio.Properties[WAVAttributes.MF_MT_AUDIO_SAMPLES_PER_SECOND] = framerate;
                            MEP.Audio.Properties[WAVAttributes.MF_MT_AUDIO_NUM_CHANNELS] = numChannels;
                            MEP.Audio.Properties[WAVAttributes.MF_MT_AUDIO_BITS_PER_SAMPLE] = bitsPerSample;
                            MEP.Audio.Properties[WAVAttributes.MF_MT_AUDIO_AVG_BYTES_PER_SECOND] = bytespersecond;
                            foreach (var Property in MEP.Audio.Properties)
                            {
                                System.Diagnostics.Debug.WriteLine("Property: " + Property.Key.ToString());
                                System.Diagnostics.Debug.WriteLine("Value: " + Property.Value.ToString());
                                if (Property.Key == new Guid("5faeeae7-0290-4c31-9e8a-c534f68d9dba"))
                                    framerate = (uint)Property.Value;
                                if (Property.Key == new Guid("f2deb57f-40fa-4764-aa33-ed4f2d1ff669"))
                                    bitsPerSample = (uint)Property.Value;
                                if (Property.Key == new Guid("37e48bf5-645e-4c5b-89de-ada9e29b696a"))
                                    numChannels = (uint)Property.Value;

                            }
                        }
                        if (MEP.Container != null)
                        {
                            foreach (var Property in MEP.Container.Properties)
                            {
                                System.Diagnostics.Debug.WriteLine("Property: " + Property.Key.ToString());
                                System.Diagnostics.Debug.WriteLine("Value: " + Property.Value.ToString());
                            }
                        }
                    }
                    await mediaCapture.StartRecordToStreamAsync(MEP, STTStream);
                    bResult = true;
                    isRecording = true;
                    System.Diagnostics.Debug.WriteLine("Recording in audio stream...");
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("Exception while recording in audio stream:" + e.Message);
                }
            }
            return bResult;
        }
        /// <summary>
        /// StopRecording method
        /// </summary>
        /// <param>
        /// Stop to record audio .
        /// The audio stream is still in stored in memory
        /// </param>
        /// <return>return true if successful.
        /// </return>
        public async System.Threading.Tasks.Task<bool> StopRecording()
        {
            // Stop recording and dispose resources
            if (mediaCapture != null)
            {
                if (isRecording == true)
                {
                    await mediaCapture.StopRecordAsync();
                    isRecording = false;
                }
            }
            return true;
        }

        /// <summary>
        /// Cleans up the microphone resources and the stream and unregisters from MediaCapture events
        /// </summary>
        /// <returns>true if successful</returns>
        public async System.Threading.Tasks.Task<bool> CleanupRecording()
        {
            if (isRecordingInitialized)
            {
                // If a recording is in progress during cleanup, stop it to save the recording
                if (isRecording)
                {
                    await StopRecording();
                }
                isRecordingInitialized = false;
            }

            if (mediaCapture != null)
            {
                mediaCapture.RecordLimitationExceeded -= mediaCapture_RecordLimitationExceeded;
                mediaCapture.Failed -= mediaCapture_Failed;
                mediaCapture.Dispose();
                mediaCapture = null;
            }
            if (STTStream != null)
            {
                STTStream.AudioLevel -= STTStream_AudioLevel;
                STTStream.Dispose();
                STTStream = null;
            }
            return true;
        }
        /// <summary>
        /// Event which return the Audio Level of the audio samples
        /// being stored in the audio buffer
        /// </summary>
        /// <returns>true if successful</returns>
        public delegate void AudioLevelEventHandler(object sender, double level);
        public event AudioLevelEventHandler AudioLevel;

        #region private
        private async System.Threading.Tasks.Task<bool> InitializeRecording()
        {
            isRecordingInitialized = false;
            try
            {
                // Initialize MediaCapture
                mediaCapture = new Windows.Media.Capture.MediaCapture();

                await mediaCapture.InitializeAsync(new Windows.Media.Capture.MediaCaptureInitializationSettings
                {
                    //VideoSource = screenCapture.VideoSource,
                    //      AudioSource = screenCapture.AudioSource,
                    StreamingCaptureMode = Windows.Media.Capture.StreamingCaptureMode.Audio,
                    MediaCategory = Windows.Media.Capture.MediaCategory.Other,
                    AudioProcessing = Windows.Media.AudioProcessing.Raw

                });
                mediaCapture.RecordLimitationExceeded += mediaCapture_RecordLimitationExceeded;
                mediaCapture.Failed += mediaCapture_Failed;
                System.Diagnostics.Debug.WriteLine("Device Initialized Successfully...");
                isRecordingInitialized = true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("Exception while initializing the device: " + e.Message);
            }
            return isRecordingInitialized;
        }
        async void mediaCapture_Failed(Windows.Media.Capture.MediaCapture sender, Windows.Media.Capture.MediaCaptureFailedEventArgs errorEventArgs)
        {
            System.Diagnostics.Debug.WriteLine("Fatal Error " + errorEventArgs.Message);
            await StopRecording();
        }

        async void mediaCapture_RecordLimitationExceeded(Windows.Media.Capture.MediaCapture sender)
        {
            System.Diagnostics.Debug.WriteLine("Stopping Record on exceeding max record duration");
            await StopRecording();
        }
        private  void STTStream_AudioLevel(object sender, double level)
        {
            //System.Diagnostics.Debug.WriteLine("STTStream_AmplitudeReading")
            if (AudioLevel != null)
                AudioLevel(sender, level);
        }

        private void ProgressHandler(Windows.Web.Http.HttpProgress progress)
        {
            System.Diagnostics.Debug.WriteLine("Http progress: " + progress.Stage.ToString() + " " + progress.BytesSent.ToString() + "/" + progress.TotalBytesToSend.ToString());
        }
        #endregion private
    }

}