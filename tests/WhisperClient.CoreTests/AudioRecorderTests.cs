using NAudio.Wave;
using WhisperClient.Core;

internal static class AudioRecorderTests
{
    public static async Task RunAsync(Action<bool, string> assert)
    {
        var device = new FakeMicrophone();
        using var recorder = new AudioRecorderService(() => device);
        recorder.StartRecording();
        device.Emit(new byte[320]);
        var result = recorder.StopRecordingAsync();
        assert(!result.IsCompleted, "capture waits for RecordingStopped");
        device.Emit(new byte[160]); // Final queued buffer must be included.
        device.Finish();
        var bytes = Convert.FromBase64String((await result)!);
        using var reader = new WaveFileReader(new MemoryStream(bytes));
        assert(reader.Length == 480, "WAV data length includes final buffer");
        assert(BitConverter.ToInt32(bytes, 4) == bytes.Length - 8, "RIFF header finalized before transmission");
        assert(device.Disposed && !recorder.IsRecording, "successful stop releases microphone");

        var emptyDevice = new FakeMicrophone { ImmediateStop = true };
        using var empty = new AudioRecorderService(() => emptyDevice);
        empty.StartRecording();
        assert(await empty.StopRecordingAsync() is null, "empty capture is not sent");

        var cancelledDevice = new FakeMicrophone();
        using var cancelled = new AudioRecorderService(() => cancelledDevice);
        cancelled.StartRecording();
        using var source = new CancellationTokenSource();
        var pending = cancelled.StopRecordingAsync(source.Token);
        source.Cancel();
        var wasCancelled = false;
        try { await pending; } catch (OperationCanceledException) { wasCancelled = true; }
        assert(wasCancelled && cancelledDevice.Disposed, "cancelled pending stop releases microphone");
        cancelledDevice.Emit(new byte[320]);
        assert(!cancelled.IsRecording, "late callbacks cannot revive cancelled capture");

        var brokenDevice = new FakeMicrophone { ImmediateStop = true, Error = new IOException("hardware error") };
        using var broken = new AudioRecorderService(() => brokenDevice);
        broken.StartRecording();
        var failed = false;
        try { await broken.StopRecordingAsync(); } catch (IOException) { failed = true; }
        assert(failed && brokenDevice.Disposed, "device errors fail closed and release capture");

        var oversizedDevice = new FakeMicrophone { ImmediateStop = true };
        using var oversized = new AudioRecorderService(() => oversizedDevice);
        oversized.StartRecording();
        oversizedDevice.Emit(new byte[8 * 1024 * 1024 + 1]);
        var rejected = false;
        try { await oversized.StopRecordingAsync(); } catch (IOException) { rejected = true; }
        assert(rejected && oversizedDevice.Disposed, "oversized voice input is rejected and released");
    }

    private sealed class FakeMicrophone : IWaveIn
    {
        public WaveFormat WaveFormat { get; set; } = new(16000, 16, 1);
        public event EventHandler<WaveInEventArgs>? DataAvailable;
        public event EventHandler<StoppedEventArgs>? RecordingStopped;
        public bool ImmediateStop;
        public bool Disposed;
        public Exception? Error;
        public void StartRecording() { }
        public void StopRecording() { if (ImmediateStop) Finish(); }
        public void Emit(byte[] bytes) => DataAvailable?.Invoke(this, new WaveInEventArgs(bytes, bytes.Length));
        public void Finish() => RecordingStopped?.Invoke(this, new StoppedEventArgs(Error));
        public void Dispose() => Disposed = true;
    }
}
