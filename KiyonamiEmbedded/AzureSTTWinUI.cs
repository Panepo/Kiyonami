using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Kiyonami
{
    public partial class AzureSTTWinUI
    {
        private static readonly string modelPath = "STTmodels";

        public enum AzureSttStatus
        {
            NOT_INITIAL,
            READY,
            ERROR_INITIAL,
            ERROR_INI_PARAMETER,

            RECOGNIZING,
            RECOGNIZED,
            RECOGNIZING_KEYWORD,
            RECOGNIZED_KEYWORD,
            NOT_RECOGNIZED,
            CANCELED,
            SESSION_START,
            SESSION_STOP,

            SPEECH_START,
            SPEECH_END,
        }

        public enum AzureSttPhase
        {
            KEYWORD,
            SPEECH,
        }

        public enum AzureSttLanguage
        {
            ENGLISH,
            CHINESE,
            KOREAN,
            ITIALIAN,
        }

        private static string GetLanguageName(AzureSttLanguage language)
        {
            return language switch
            {
                AzureSttLanguage.ENGLISH => "en-US",
                AzureSttLanguage.CHINESE => "zh-TW",
                AzureSttLanguage.KOREAN => "ko-KR",
                AzureSttLanguage.ITIALIAN => "it-IT",
                _ => throw new ApplicationException("language error"),
            };
        }

        public struct AzureSttOutput
        {
            public AzureSttPhase phase;
            public AzureSttStatus status;
            public string message;
        }

        public class ProcessEventArgs : EventArgs
        {
            public AzureSttOutput Output { get; set; }
        }

        private AzureSttStatus _status = AzureSttStatus.NOT_INITIAL;
        public AzureSttStatus Status
        {
            get { return _status; }
        }
        private AzureSttPhase _phase = AzureSttPhase.SPEECH;
        public AzureSttPhase Phase
        {
            get { return _phase; }
        }

        private readonly SpeechRecognizer _recognizer;
        private AzureSttOutput _output;
        private TaskCompletionSource<int>? _stopRecognition;

        private readonly bool _useKeyword = false;
        private readonly KeywordRecognitionModel _keywordModel = null!;

        public delegate void ProcessEventHandler(object mObjct, ProcessEventArgs mArgs);
        public event ProcessEventHandler? OnProcessed;

        public AzureSTTWinUI(IntPtr modelKey, AzureSttLanguage language, string deviceID, bool useKeyword, string KeywordModelFileName)
        {
            try
            {
                string? entryAssemblyLocation = Assembly.GetEntryAssembly()?.Location;
                if (string.IsNullOrEmpty(entryAssemblyLocation))
                {
                    throw new ApplicationException("Entry assembly location is null.");
                }
                string CurrentPath = Path.GetDirectoryName(entryAssemblyLocation) ?? throw new ApplicationException("Failed to get directory name from entry assembly location.");

                EmbeddedSpeechConfig embConfig = EmbeddedSpeechConfig.FromPath(Path.Combine(CurrentPath, modelPath));
                string modelName = GetLanguageName(language);

                embConfig.SetSpeechRecognitionModel(modelName, Marshal.PtrToStringAuto(modelKey));
                embConfig.SetProfanity(ProfanityOption.Raw);

                AudioConfig audioInput = AudioConfig.FromMicrophoneInput(deviceID);
                _recognizer = new SpeechRecognizer(embConfig, audioInput);

                if (useKeyword)
                {
                    this._useKeyword = useKeyword;
                    this._keywordModel = KeywordRecognitionModel.FromFile(Path.Combine(CurrentPath, KeywordModelFileName));
                }

                _status = AzureSttStatus.READY;

                // Subscribes to events.
                _recognizer.Recognizing += HandleRecognizing;
                _recognizer.Recognized += HandleRecognized;
                _recognizer.Canceled += HandleCanceled;

                _recognizer.SessionStarted += HandleSessionStart;
                _recognizer.SessionStopped += HandleSessionStop;

                _recognizer.SpeechStartDetected += HandleSpeechStart;
                _recognizer.SpeechEndDetected += HandleSpeechEnd;
            }
            catch (Exception ex)
            {
                _status = AzureSttStatus.ERROR_INITIAL;
                throw new ApplicationException(ex.Message);
            }
        }

        ~AzureSTTWinUI()
        {
            Release();
        }

        public void Release()
        {
            while (_phase == AzureSttPhase.SPEECH)
            {
                Thread.Sleep(1000);
            }

            _stopRecognition?.SetResult(0);

            _recognizer.Recognizing -= HandleRecognizing;
            _recognizer.Recognized -= HandleRecognized;
            _recognizer.Canceled -= HandleCanceled;

            _recognizer.SessionStarted -= HandleSessionStart;
            _recognizer.SessionStopped -= HandleSessionStop;

            _recognizer.SpeechStartDetected -= HandleSpeechStart;
            _recognizer.SpeechEndDetected -= HandleSpeechEnd;

            _recognizer.Dispose();
			_status = AzureSttStatus.NOT_INITIAL;
        }

        public void StopRecognition()
        {   
            if (_useKeyword)
            {
                while (_phase == AzureSttPhase.SPEECH)
                {
                    Thread.Sleep(1000);
                }
            }

            _stopRecognition?.SetResult(0);
        }

        // Speech recognition using microphone.
        public async Task RecognitionAsync()
        {
            if (_recognizer == null) throw new ApplicationException("Recognizer is not initialized.");

            _stopRecognition = new TaskCompletionSource<int>();

            if (_status == AzureSttStatus.READY)
            {
                if (this._useKeyword)
                {
                    // start,wait,stop recognition
                    _phase = AzureSttPhase.KEYWORD;
                    await _recognizer.StartKeywordRecognitionAsync(_keywordModel).ConfigureAwait(false);
                }
                else
                {
                    // start,wait,stop recognition
                    _phase = AzureSttPhase.SPEECH;
                    await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
                }
				
				Task.WaitAny(new[] { _stopRecognition.Task });
                await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
        }

        public async Task<string> RecognitionOnceAsync()
        {
            if (_recognizer == null) throw new ApplicationException("Recognizer is not initialized.");

            _stopRecognition = new TaskCompletionSource<int>();

            string result = "";
            EventHandler<SpeechRecognitionEventArgs> OnceHandler = new EventHandler<SpeechRecognitionEventArgs>((obj, e) =>
            {
                if (result == "" && e.Result.Text != "")
                {
                    result = e.Result.Text;
                }
                _stopRecognition.SetResult(0);
            });
            if (_status == AzureSttStatus.READY)
            {
                _recognizer.Recognized += OnceHandler;

                if (this._useKeyword)
                {
                    // start,wait,stop recognition
                    _phase = AzureSttPhase.KEYWORD;
                    await _recognizer.StartKeywordRecognitionAsync(_keywordModel).ConfigureAwait(false);
                }
                else
                {
                    // start,wait,stop recognition
                    _phase = AzureSttPhase.SPEECH;
                    await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
                }

                Task.WaitAny(new[] { _stopRecognition.Task });
                _recognizer.Recognized -= OnceHandler;
                await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
            }
            return result;
        }

        private void HandleRecognizing(object? sender, SpeechRecognitionEventArgs e)
        {
            _output.status = AzureSttStatus.NOT_RECOGNIZED;
            _output.message = "";
            _output.phase = _phase;

            ProcessEventArgs eventArgs = new ProcessEventArgs();

            if (e.Result.Reason == ResultReason.RecognizingSpeech)
            {
                _output.status = AzureSttStatus.RECOGNIZING;
                _output.message = e.Result.Text;

                eventArgs.Output = _output;
                if (OnProcessed != null) OnProcessed(this, eventArgs);
            }
            else if (e.Result.Reason == ResultReason.RecognizingKeyword)
            {
                _output.status = AzureSttStatus.RECOGNIZING_KEYWORD;
                _output.message = e.Result.Text;

                eventArgs.Output = _output;
                if (OnProcessed != null) OnProcessed(this, eventArgs);
            }
        }

        private void HandleRecognized(object? sender, SpeechRecognitionEventArgs e)
        {
            _output.status = AzureSttStatus.NOT_RECOGNIZED;
            _output.message = "";
            _output.phase = _phase;

            ProcessEventArgs eventArgs = new ProcessEventArgs();

            if (e.Result.Reason == ResultReason.RecognizedSpeech)
            {
                _output.status = AzureSttStatus.RECOGNIZED;
                _output.message = e.Result.Text;

                eventArgs.Output = _output;
                if(OnProcessed != null) OnProcessed(this, eventArgs);
            }
            else if (e.Result.Reason == ResultReason.RecognizedKeyword)
            {
                _output.status = AzureSttStatus.RECOGNIZED_KEYWORD;
                _output.message = e.Result.Text;
                
                _phase = AzureSttPhase.SPEECH;
                _output.phase = _phase;

                eventArgs.Output = _output;
                if (OnProcessed != null) OnProcessed(this, eventArgs);
            }
            else if (e.Result.Reason == ResultReason.NoMatch)
            {
                _output.status = AzureSttStatus.NOT_RECOGNIZED;
                _output.message = "";

                eventArgs.Output = _output;
                if (OnProcessed != null) OnProcessed(this, eventArgs);
            }
        }

        private void HandleCanceled(object? sender, SpeechRecognitionCanceledEventArgs e)
        {
            _output.status = AzureSttStatus.CANCELED;
            _output.message = $"CANCELED: Reason={e.Reason} Code={e.ErrorCode} Details={e.ErrorDetails}";
            _output.phase = _phase;

            ProcessEventArgs eventArgs = new ProcessEventArgs { Output = _output };
            if (OnProcessed != null) OnProcessed(this, eventArgs);
        }

        private void HandleSessionStart(object? sender, SessionEventArgs e)
        {
            _output.status = AzureSttStatus.SESSION_START;
            _output.message = "Session started event.";
            _output.phase = _phase;

            ProcessEventArgs eventArgs = new ProcessEventArgs { Output = _output };
            if (OnProcessed != null) OnProcessed(this, eventArgs);
        }

        private void HandleSessionStop(object? sender, SessionEventArgs e)
        {
            _output.status = AzureSttStatus.SESSION_STOP;
            _output.message = "Stop recognition.";

            if (_useKeyword) _phase = AzureSttPhase.KEYWORD;

            _output.phase = _phase;

            ProcessEventArgs eventArgs = new ProcessEventArgs { Output = _output };
            if (OnProcessed != null) OnProcessed(this, eventArgs);
        }

        private void HandleSpeechStart(object? sender, RecognitionEventArgs e)
        {
            _output.status = AzureSttStatus.SPEECH_START;
            _output.message = "Speech start detect.";
            _output.phase = _phase;

            ProcessEventArgs eventArgs = new ProcessEventArgs { Output = _output };
            if (OnProcessed != null) OnProcessed(this, eventArgs);
        }
        
        private void HandleSpeechEnd(object? sender, RecognitionEventArgs e)
        {
            _output.status = AzureSttStatus.SPEECH_END;
            _output.message = "Speech end detect.";
            _output.phase = _phase;

            ProcessEventArgs eventArgs = new ProcessEventArgs { Output = _output };
            if (OnProcessed != null) OnProcessed(this, eventArgs);
        }   
    }
}