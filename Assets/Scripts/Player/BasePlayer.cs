using System.Diagnostics;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;

public abstract class BasePlayer : MonoBehaviour
{
    [SerializeField] protected int _bufferSize = 20;

    protected Vector3 _offset;
    protected Vector3 _scale;
    protected Vector3 _rotation;
    protected Color _tint;

    protected ConcurrentQueue<DPCFrameBuffer> _buffer;
    protected int _currentImportFrame = -1;
    protected int _currentRenderFrame;

    [SerializeField] protected Shader _shader;
    protected Material _material;

    protected bool _isPlaying = false;
    protected bool _isRotating = false;
    protected bool _isClockwise = true;

    // Timer display paramms
    private float _playStartedAt = -1f;          // wall-clock start
    private float _pausedAccum = 0f;          // time spent in Pause()

    public float ElapsedTime =>                  // expose to UI
           _playStartedAt < 0 ? 0 :
           (_isPlaying ? Time.time              // live
                        : _pauseStamp)           // frozen
           - _playStartedAt                      // since Play()
           - _pausedAccum;                       // minus pauses

    private float _pauseStamp;                   // when Pause began

    protected virtual void Start()
    {
        Initialize();
        _buffer = new ConcurrentQueue<DPCFrameBuffer>();
        _currentImportFrame = _currentRenderFrame = GetStartFrame();
        DeleteBuffers();
        Buffering();
        SetCurrentFrameBuffer();
    }
    protected Stopwatch stopwatch = new();
    protected float _timer = 0.0f;
    protected virtual void Update()
    {
        var inPlayContent = GetCurrentContent();
        float targetFrameTime = 1.0f / inPlayContent.GetFrameRate();
        UpdatePosition();
        if (_isPlaying)
        {
            _timer += Time.deltaTime;
            if (_timer + stopwatch.ElapsedMilliseconds / 1000.0f >= targetFrameTime)
            {
                stopwatch.Restart();

                if (_buffer.Count < _bufferSize)
                {
                    Thread import = new(ImporterNextFrame);
                    import.Start();
                }

                // Donot Dequeue if buffer only have 1 frame left, stall
                if (_buffer.Count > 1)
                {
                    if (_buffer.TryDequeue(out DPCFrameBuffer frameToDiscard))
                    {
                        DeleteBuffers();
                        SetCurrentFrameBuffer();

                        _currentImportFrame = (_currentImportFrame + 1) % (GetCurrentContent().GetLastFrame() + 1 - GetCurrentContent().GetStartFrame());
                        // _currentRenderFrame++; // <-- FIX: REMOVE this line from here.
                    }
                }
                else
                    UnityEngine.Debug.Log("Stall, Buffer runs out. Try increase buffer size.");

                // FIX: ADD the increment here. This ensures the render counter always
                // progresses, even during a stall, allowing the clip to finish.
                _currentRenderFrame++;

                stopwatch.Stop();

                if (_timer + stopwatch.ElapsedMilliseconds / 1000.0f < targetFrameTime)
                    Thread.Sleep((int)((targetFrameTime - _timer) * 1000.0f - stopwatch.ElapsedMilliseconds));

                _timer -= targetFrameTime; // Reset the timer
            }

            int totalFrame = GetCurrentContent().GetDuration() <= 0 ? inPlayContent.GetLastFrame() : (int)(GetCurrentContent().GetDuration() * inPlayContent.GetFrameRate());

            // FIX: Use >= for robustness. This check is now reachable.
            if (_currentRenderFrame >= totalFrame)
            {
                EndOfContent();
            }
        }
        RenderCurrentFrame();
    }
    protected virtual void OnDisable()
    {
        DeleteBuffers();
    }
    protected virtual void OnDestroy()
    {
        DeleteBuffers();
    }
    protected virtual void Buffering()
    {
        for (int i = 0; i < _bufferSize; i++, _currentImportFrame++)
        {
            ImporterNextFrame();
        }
    }
    protected virtual void EndOfContent() { Pause(); }
    protected abstract void Initialize();
    protected abstract int GetStartFrame();
    protected abstract VVHandler GetCurrentContent();
    protected abstract void UpdatePosition();
    protected abstract void RenderCurrentFrame();
    protected abstract void DeleteBuffers();
    protected abstract void SetCurrentFrameBuffer();
    protected abstract void ImporterNextFrame();

    public void Play()
    {
        if (_playStartedAt < 0) _playStartedAt = Time.time; // first ever Play()
        else _pausedAccum += Time.time - _pauseStamp;
        _isPlaying = true;
    }
    public bool IsPlaying => _isPlaying;
    public void Pause()
    {
        _isPlaying = false;
        _pauseStamp = Time.time;
    }
    public void Replay()
    {
        _currentImportFrame = _currentRenderFrame = GetCurrentContent().GetStartFrame();
        _buffer = new ConcurrentQueue<DPCFrameBuffer>();
        DeleteBuffers();
        Buffering();
        SetCurrentFrameBuffer();
        Play();
    }
    public Vector3 Offset => _offset;
    public float Duration => GetCurrentContent().GetDuration() <= 0 ? (float)(GetCurrentContent().GetLastFrame() - GetCurrentContent().GetStartFrame() + 1) / GetCurrentContent().GetFrameRate() : GetCurrentContent().GetDuration();
    public int FramesLeft { get { return GetCurrentContent().GetLastFrame() - _currentRenderFrame; } }
}
