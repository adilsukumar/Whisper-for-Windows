using NAudio.Wave;
using NAudio.Utils;
using System.IO;

namespace WhisperClient.Core;

/// <summary>Bounded push-to-talk capture. Finalize WAV only after the device stops.</summary>
public sealed class AudioRecorderService : IDisposable
{
    private readonly object _gate = new();
    private readonly Func<IWaveIn> _factory;
    private Capture? _capture;
    private bool _disposed;
    private const int MaxPcmBytes = 8 * 1024 * 1024;

    private sealed class Capture(IWaveIn device)
    {
        public readonly IWaveIn Device = device;
        public readonly MemoryStream Buffer = new();
        public WaveFileWriter Writer = null!;
        public readonly TaskCompletionSource<Exception?> Stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Accepting = true;
        public bool Stopping;
        public int Bytes;
        public bool TooLarge;
        public int Cleaned;
    }

    public AudioRecorderService(Func<IWaveIn>? factory = null) => _factory = factory ?? (() =>
        new WaveInEvent { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 50 });

    public bool IsRecording { get { lock (_gate) return _capture is { Stopping: false }; } }

    public void StartRecording()
    {
        Capture capture;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_capture is not null) throw new InvalidOperationException("A capture is already active.");
            capture = new Capture(_factory());
            try { capture.Writer = new WaveFileWriter(new IgnoreDisposeStream(capture.Buffer), capture.Device.WaveFormat); }
            catch { capture.Device.Dispose(); capture.Buffer.Dispose(); throw; }
            _capture = capture;
            capture.Device.DataAvailable += (_, e) =>
            {
                lock (_gate)
                {
                    if (!capture.Accepting) return;
                    if (e.BytesRecorded > MaxPcmBytes - capture.Bytes)
                    { capture.TooLarge = true; capture.Accepting = false; return; }
                    capture.Writer.Write(e.Buffer, 0, e.BytesRecorded);
                    capture.Bytes += e.BytesRecorded;
                }
            };
            capture.Device.RecordingStopped += (_, e) =>
            {
                lock (_gate) capture.Accepting = false;
                capture.Stopped.TrySetResult(e.Exception);
            };
        }
        try { capture.Device.StartRecording(); }
        catch { Cleanup(capture); throw; }
    }

    public async Task<string?> StopRecordingAsync(CancellationToken cancellationToken = default)
    {
        Capture capture;
        lock (_gate)
        {
            if (_capture is null || _capture.Stopping) return null;
            capture = _capture;
            capture.Stopping = true;
        }
        try
        {
            capture.Device.StopRecording();
            var error = await capture.Stopped.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (error is not null) throw new IOException("Microphone capture failed.", error);
            lock (_gate)
            {
                if (capture.Cleaned != 0) throw new OperationCanceledException();
                if (capture.TooLarge) throw new IOException("Voice question is too long.");
                capture.Writer.Dispose(); // Writes RIFF/data lengths before copying.
                return capture.Bytes == 0 ? null : Convert.ToBase64String(capture.Buffer.ToArray());
            }
        }
        finally { Cleanup(capture); }
    }

    public void CancelRecording()
    {
        Capture? capture;
        lock (_gate)
        {
            capture = _capture;
            if (capture is null) return;
            _capture = null;
            capture.Accepting = false;
        }
        try { capture.Device.StopRecording(); }
        finally { capture.Stopped.TrySetCanceled(); Cleanup(capture); }
    }

    private void Cleanup(Capture capture)
    {
        lock (_gate)
        {
            if (Interlocked.Exchange(ref capture.Cleaned, 1) != 0) return;
            capture.Accepting = false;
            if (ReferenceEquals(_capture, capture)) _capture = null;
        }
        // Native device disposal must not hold the callback lock.
        try { capture.Device.Dispose(); }
        finally { capture.Writer.Dispose(); capture.Buffer.Dispose(); }
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
        CancelRecording();
    }
}
