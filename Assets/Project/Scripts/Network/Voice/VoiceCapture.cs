using UnityEngine;
using System.Collections.Generic;
using Concentus.Enums;
using Concentus.Structs;

// [로컬 클라] Unity 마이크로 녹음 → 48kHz 20ms 프레임으로 자름 → 음성 감지 → Opus 압축
// 볼륨이 기준을 넘을 때만 패킷이 나옴 (말을 멈춰도 _hangover초 동안은 계속 보냄)
public class VoiceCapture
{
    public const int SampleRate = 48000;
    public const int FrameSamples = SampleRate / 50; // 20ms

    private const int ClipSeconds = 1;
    private const int MaxEncodedBytes = 400;
    private const int Bitrate = 24000;
    private const float InputGain = 3f;

    private readonly float _threshold;
    private readonly float _hangover;
    private readonly OpusEncoder _encoder;
    private readonly byte[] _encodeBuffer = new byte[MaxEncodedBytes];
    private readonly float[] _frame = new float[FrameSamples];
    private readonly Queue<byte[]> _packets = new Queue<byte[]>();

    private string _device;
    private AudioClip _clip;
    private float[] _chunk;
    private int _readPos;
    private int _frameFill;
    private float _gateUntil;
    private bool _send;

    // 마이크가 48kHz를 지원하지 않을 때 선형 보간으로 변환
    private double _step;
    private double _resamplePos;
    private float _prevSample;

    public bool IsRecording => _clip != null;

    public VoiceCapture(float threshold, float hangover)
    {
        _threshold = threshold;
        _hangover = hangover;

        _encoder = new OpusEncoder(SampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        _encoder.Bitrate = Bitrate;
        _encoder.SignalType = OpusSignal.OPUS_SIGNAL_VOICE;
        _encoder.Complexity = 5; // C# 구현이라 기본값 10은 CPU 부담이 큼
    }

    // device가 빈 문자열이거나 지금 연결돼 있지 않으면 시스템 기본 마이크
    public bool Start(string device)
    {
        Stop();

        if (Microphone.devices.Length == 0) return false;
        if (string.IsNullOrEmpty(device) || System.Array.IndexOf(Microphone.devices, device) == -1) device = null;

        // 0, 0이면 어떤 주파수든 지원
        Microphone.GetDeviceCaps(device, out int min, out int max);
        int frequency = min == 0 && max == 0 ? SampleRate : Mathf.Clamp(SampleRate, min, max);

        _clip = Microphone.Start(device, true, ClipSeconds, frequency);
        if (_clip == null) return false;

        _device = device;
        _chunk = new float[Mathf.Max(1, _clip.frequency / 200)]; // 5ms씩 꺼냄
        _readPos = 0;
        _step = (double)_clip.frequency / SampleRate;
        _resamplePos = 0;
        _prevSample = 0f;
        return true;
    }

    public void Stop()
    {
        if (_clip != null)
        {
            Microphone.End(_device);
            Object.Destroy(_clip);
            _clip = null;
        }

        _frameFill = 0;
        _packets.Clear();
    }

    // 매 프레임 호출. send가 false면 (음소거) 녹음된 것을 비우기만 함 (풀었을 때 밀린 음성이 나가지 않게)
    public void Update(bool send)
    {
        if (_clip == null) return;

        _send = send;
        int length = _clip.samples;
        int available = (Microphone.GetPosition(_device) - _readPos + length) % length;

        while (available >= _chunk.Length)
        {
            _clip.GetData(_chunk, _readPos); // 끝을 넘으면 처음부터 이어서 읽음
            _readPos = (_readPos + _chunk.Length) % length;
            available -= _chunk.Length;

            foreach (float sample in _chunk) Resample(sample);
        }
    }

    public bool TryDequeue(out byte[] packet)
    {
        if (_packets.Count == 0)
        {
            packet = null;
            return false;
        }

        packet = _packets.Dequeue();
        return true;
    }

    // _prevSample(0)과 sample(1) 사이에서 48kHz 간격으로 뽑음. 같은 주파수면 한 샘플씩 그대로 나감
    private void Resample(float sample)
    {
        while (_resamplePos < 1.0)
        {
            AddSample(Mathf.LerpUnclamped(_prevSample, sample, (float)_resamplePos));
            _resamplePos += _step;
        }

        _resamplePos -= 1.0;
        _prevSample = sample;
    }

    private void AddSample(float sample)
    {
        _frame[_frameFill++] = sample;
        if (_frameFill < FrameSamples) return;

        _frameFill = 0;
        EncodeFrame();
    }

    private void EncodeFrame()
    {
        float sum = 0f;
        foreach (float s in _frame) sum += s * s;
        if (Mathf.Sqrt(sum / FrameSamples) >= _threshold) _gateUntil = Time.time + _hangover;

        if (!_send || Time.time > _gateUntil) return;

        // Keep the voice gate based on the original mic level, then boost what is sent.
        // Soft limiting avoids hard clipping when a loud input exceeds the sample range.
        for (int i = 0; i < _frame.Length; i++)
            _frame[i] = (float)System.Math.Tanh(_frame[i] * InputGain);

        int written = _encoder.Encode(_frame, 0, FrameSamples, _encodeBuffer, 0, MaxEncodedBytes);
        if (written <= 0) return;

        byte[] packet = new byte[written];
        System.Buffer.BlockCopy(_encodeBuffer, 0, packet, 0, written);
        _packets.Enqueue(packet);
    }
}
