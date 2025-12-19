using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;

namespace Kiyonami
{
    public partial class AzureTTSWinUI
    {
        private static readonly string EmbeddedSpeechSynthesisVoicePath = "./TTSmodels";

        public enum AzureTtsStatus
        {
            NOT_INITIAL,
            READY,
            ERROR_INITIAL,

            SYNTHESIZING,
            SYNTHESIZED,
            NOT_SYNTHESIZED,
            CANCELED,
            SESSION_START,
            SESSION_STOP
        }

        public enum AzureTtsVoiceName
        {
            // english voices
            ARIA,
            JENNY,
            GUY,

            // chinese voices
            XIAOXIAO,
            YUNXI,

            // italian voices
            DIEGO,
            ISABELLA,
        }

        public struct AzureTtsOutput
        {
            public AzureTtsStatus status;
            public string message;
        }

        public class ProcessEventArgs : EventArgs
        {
            public AzureTtsOutput Output { get; set; }
        }

        private AzureTtsStatus _status = AzureTtsStatus.NOT_INITIAL;
        public AzureTtsStatus Status
        {
            get { return _status; }
        }

        private readonly SpeechSynthesizer _synthesizer;
        private AzureTtsOutput _output;

        public delegate void ProcessEventHandler(object mObjct, ProcessEventArgs mArgs);
        public event ProcessEventHandler? OnProcessed;

        public AzureTTSWinUI(IntPtr EmbeddedSpeechSynthesisVoiceKey, AzureTtsVoiceName voice, string deviceID)
        {
            try
            {
                string EmbeddedSpeechSynthesisVoiceName = "";
                switch (voice)
                {
                    case AzureTtsVoiceName.ARIA:
                        EmbeddedSpeechSynthesisVoiceName = "en-US-AriaNeural";
                        break;
                    case AzureTtsVoiceName.JENNY:
                        EmbeddedSpeechSynthesisVoiceName = "en-US-JennyNeural";
                        break;
                    case AzureTtsVoiceName.GUY:
                        EmbeddedSpeechSynthesisVoiceName = "en-US-GuyNeural";
                        break;
                    case AzureTtsVoiceName.XIAOXIAO:
                        EmbeddedSpeechSynthesisVoiceName = "zh-CN-XiaoxiaoNeural";
                        break;
                    case AzureTtsVoiceName.YUNXI:
                        EmbeddedSpeechSynthesisVoiceName = "zh-CN-YunxiNeural";
                        break;
                    case AzureTtsVoiceName.DIEGO:
                        EmbeddedSpeechSynthesisVoiceName = "it-IT-DiegoNeural";
                        break;
                    case AzureTtsVoiceName.ISABELLA:
                        EmbeddedSpeechSynthesisVoiceName = "it-IT-IsabellaNeural";
                        break;
                }
                
                string? entryAssemblyLocation = Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrEmpty(entryAssemblyLocation))
                    throw new InvalidOperationException("Entry assembly location is null. Cannot determine current path.");
                string CurrentPath = Path.GetDirectoryName(entryAssemblyLocation)!;
                EmbeddedSpeechConfig embConfig = EmbeddedSpeechConfig.FromPath(Path.Combine(CurrentPath, EmbeddedSpeechSynthesisVoicePath));
                embConfig.SetSpeechSynthesisVoice(EmbeddedSpeechSynthesisVoiceName, Marshal.PtrToStringAuto(EmbeddedSpeechSynthesisVoiceKey));
                if (EmbeddedSpeechSynthesisVoiceName.Contains("Neural"))
                {
                    // Embedded neural voices only support 24kHz and the engine has no ability to resample.
                    embConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Riff24Khz16BitMonoPcm);
                }

                AudioConfig audioOutput = AudioConfig.FromSpeakerOutput(deviceID);
                _synthesizer = new SpeechSynthesizer(embConfig, audioOutput);

                _synthesizer.SynthesisStarted += HandleSessionStart;
                _synthesizer.Synthesizing += HandleSynthesizing;
                _synthesizer.SynthesisCompleted += HandleSynthesisCompleted;

                _status = AzureTtsStatus.READY;
            }
            catch (Exception ex)
            {
                _status = AzureTtsStatus.ERROR_INITIAL;
                throw new ApplicationException(ex.Message);
            }
        }

        public void Release()
        {
            _synthesizer.SynthesisStarted -= HandleSessionStart;
            _synthesizer.Synthesizing -= HandleSynthesizing;
            _synthesizer.Dispose();
        }

        public void SynthesisToSpeakerAsync(string text)
        {
            if (_synthesizer == null) throw new ApplicationException("Speech synthesizer is not initialized.");

            ProcessEventArgs eventArgs = new();

            if (_status == AzureTtsStatus.READY)
            {
                Task<SpeechSynthesisResult> task = _synthesizer.SpeakTextAsync(text);
                task.Wait();
                var result = task.Result;
                {
                    if (result.Reason == ResultReason.SynthesizingAudioCompleted)
                    {
                        _output.message = $"Speech synthesized to speaker for text [{text}]";
                        _output.status = AzureTtsStatus.SYNTHESIZED;
                        eventArgs.Output = _output;
                        if (OnProcessed != null) OnProcessed(this, eventArgs);
                    }
                    else if (result.Reason == ResultReason.Canceled)
                    {
                        var cancellation = SpeechSynthesisCancellationDetails.FromResult(result);

                        _output.status = AzureTtsStatus.CANCELED;
                        _output.message = $"CANCELED: Reason={cancellation.Reason} ErrorCode={cancellation.ErrorCode} ErrorDetails=\"{cancellation.ErrorDetails}\"";
                        eventArgs.Output = _output;
                        if (OnProcessed != null) OnProcessed(this, eventArgs);
                    }
                }
            }
            else
            {
                _output.status = AzureTtsStatus.ERROR_INITIAL;
                _output.message = "AzureTTs initial failed";
                eventArgs.Output = _output;
                OnProcessed?.Invoke(this, eventArgs);
            }
        }

        public void StopSynthesis()
        {
            _synthesizer.StopSpeakingAsync();
        }

        private void HandleSessionStart(object? sender, SpeechSynthesisEventArgs e)
        {
            _output.status = AzureTtsStatus.SESSION_START;
            _output.message = "Synthesis started event.";

            ProcessEventArgs eventArgs = new ProcessEventArgs
            {
                Output = _output
            };
            if(OnProcessed != null) OnProcessed(this, eventArgs);
        }

        private void HandleSynthesizing(object? sender, SpeechSynthesisEventArgs e)
        {
            _output.status = AzureTtsStatus.SYNTHESIZING;
            _output.message = $"Synthesizing, received an audio chunk of {e.Result.AudioData.Length} bytes.";

            ProcessEventArgs eventArgs = new ProcessEventArgs { Output = _output };
            OnProcessed?.Invoke(this, eventArgs);
        }

        private void HandleSynthesisCompleted(object? sender, SpeechSynthesisEventArgs e)
        {
            _output.status = AzureTtsStatus.SYNTHESIZED;
            _output.message = $"Synthesized.";

            ProcessEventArgs eventArgs = new ProcessEventArgs { Output = _output };
            OnProcessed?.Invoke(this, eventArgs);
        }
    }
}