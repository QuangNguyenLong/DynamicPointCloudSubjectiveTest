using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

public abstract class BasePlayer : MonoBehaviour
{
    [SerializeField] protected int _bufferSize = 20;

    protected Vector3 _offset;
    protected Vector3 _scale;
    protected Vector3 _rotation;
    protected Color _tint;

    protected MyMath.Queue<DPCFrameBuffer> _buffer;
    protected int _currentImportFrame = -1;
    protected int _currentRenderFrame;

    [SerializeField] protected Shader _shader;
    protected Material _material;

    protected bool _isPlaying = false;
    protected bool _isRotating = false;
    protected bool _isClockwise = true;

    // Threading improvements
    private Thread _importerThread;
    private ConcurrentQueue<int> _framesToImport = new ConcurrentQueue<int>();
    private volatile bool _threadRunning = false; // Make this volatile
    private AutoResetEvent _importSignal;
    private volatile bool _threadShouldExit = false;

    // Double-buffering to avoid constant buffer recreation
    protected bool _buffersInitialized = false;
    protected int _currentBufferSize = 0;

    protected Stopwatch stopwatch = new();
    protected float _timer = 0.0f;
    protected float _nextFrameTime = 0.0f;
    protected float _targetFrameTime = 0.033f; // Default 30fps

    // ComputeBuffers - explicitly defined here to ensure proper cleanup
    protected ComputeBuffer _posBuffer;
    protected ComputeBuffer _colorBuffer;

    protected virtual void Start()
    {
        Initialize();
        _buffer = new MyMath.Queue<DPCFrameBuffer>(_bufferSize);
        _currentImportFrame = _currentRenderFrame = GetStartFrame();

        // Create a new AutoResetEvent - ensure this happens before thread starts
        _importSignal = new AutoResetEvent(false);

        // Start the worker thread
        StartImporterThread();

        // Initial buffer fill
        Buffering();

        // Only initialize buffers once we have data
        if (_buffer.Count > 0)
        {
            SetCurrentFrameBuffer();
            _currentBufferSize = _buffer.Peek().NumVerts;
            _buffersInitialized = true;
        }

        // Initialize target frame rate
        var inPlayContent = GetCurrentContent();
        _targetFrameTime = 1.0f / inPlayContent.GetFrameRate();
    }

    private void StartImporterThread()
    {
        if (_importerThread != null && _importerThread.IsAlive)
            return;

        _threadShouldExit = false;
        _threadRunning = true;
        _importerThread = new Thread(ImporterThreadFunc);
        _importerThread.IsBackground = true; // Ensure thread stops when app closes
        _importerThread.Priority = System.Threading.ThreadPriority.Highest;
        _importerThread.Start();
    }

    private void ImporterThreadFunc()
    {
        try
        {
            while (_threadRunning && !_threadShouldExit)
            {
                // Safely check if signal is null before using it
                if (_importSignal == null)
                {
                    UnityEngine.Debug.LogError("Import signal is null, exiting thread");
                    break;
                }

                try
                {
                    // Wait with timeout to check exit conditions periodically
                    if (_importSignal.WaitOne(20))
                    {
                        // Process frames in queue
                        while (!_threadShouldExit && _framesToImport.TryDequeue(out int frameToImport))
                        {
                            try
                            {
                                _currentImportFrame = frameToImport;
                                ImporterNextFrame();
                            }
                            catch (System.Exception e)
                            {
                                UnityEngine.Debug.LogError($"Error importing frame {frameToImport}: {e.Message}");
                            }

                            if (_threadShouldExit) break;
                        }
                    }
                }
                catch (ObjectDisposedException)
                {
                    UnityEngine.Debug.LogWarning("Import signal was disposed, exiting thread");
                    break;
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Importer thread crashed: {e.Message}\n{e.StackTrace}");
        }

        UnityEngine.Debug.Log("Importer thread exited cleanly");
    }

    protected virtual void OnDisable()
    {
        // Stop thread first, before releasing resources
        StopImporterThread();

        // Then clean up resources
        SafeDeleteBuffers();
    }

    protected virtual void OnDestroy()
    {
        // Same pattern as OnDisable
        StopImporterThread();
        SafeDeleteBuffers();
    }

    // Safe wrapper for DeleteBuffers to prevent errors
    private void SafeDeleteBuffers()
    {
        try
        {
            DeleteBuffers();
            _buffersInitialized = false;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Error deleting buffers: {e.Message}");
        }
    }

    private void StopImporterThread()
    {
        // Signal thread to exit
        _threadShouldExit = true;
        _threadRunning = false;

        // Safely signal thread - check if null first
        try
        {
            if (_importSignal != null)
            {
                _importSignal.Set();
            }
        }
        catch (ObjectDisposedException)
        {
            // Already disposed, just log
            UnityEngine.Debug.LogWarning("Import signal already disposed");
        }

        // Wait for thread to exit
        if (_importerThread != null && _importerThread.IsAlive)
        {
            if (!_importerThread.Join(3000)) // 3 seconds timeout
            {
                UnityEngine.Debug.LogWarning("Importer thread did not terminate gracefully");
            }
            else
            {
                UnityEngine.Debug.Log("Importer thread terminated successfully");
            }
        }

        // Dispose of signal AFTER thread has terminated
        try
        {
            if (_importSignal != null)
            {
                _importSignal.Dispose();
                _importSignal = null;
            }
        }
        catch (System.ObjectDisposedException)
        {
            // Already disposed
        }
    }

    protected virtual void Update()
    {
        var inPlayContent = GetCurrentContent();
        _targetFrameTime = 1.0f / inPlayContent.GetFrameRate();
        UpdatePosition();

        if (_isPlaying)
        {
            _timer += Time.deltaTime;

            // Check if it's time to show the next frame
            if (_timer >= _nextFrameTime)
            {
                // Queue more frames if needed
                if (_buffer.Count < _bufferSize * 0.75f && _framesToImport.Count < 10) // Limit queue size
                {
                    int frameToImport = (_currentImportFrame + 1) % (inPlayContent.GetLastFrame() + 1 - inPlayContent.GetStartFrame());
                    _framesToImport.Enqueue(frameToImport);

                    // Safely signal thread
                    try
                    {
                        if (_importSignal != null && _threadRunning)
                        {
                            _importSignal.Set();
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        UnityEngine.Debug.LogError("Import signal disposed while trying to signal worker thread");
                    }
                }

                // Dequeue if buffer has frames available
                if (_buffer.Count > 1)
                {
                    DPCFrameBuffer oldFrame = _buffer.Peek(); // Get the frame first
                    _buffer.Dequeue(); // Then remove it from the queue

                    // Only recreate buffers when necessary (size changed)
                    if (_buffer.Count > 0)
                    {
                        if (!_buffersInitialized || _buffer.Peek().NumVerts != _currentBufferSize)
                        {
                            // Clean up old buffers if they exist
                            if (_buffersInitialized)
                            {
                                SafeDeleteBuffers();
                            }

                            // Create new buffers with current size
                            SetCurrentFrameBuffer();
                            _currentBufferSize = _buffer.Peek().NumVerts;
                            _buffersInitialized = true;
                        }
                        else
                        {
                            // Just update existing buffers with new data
                            UpdateBufferData();
                        }
                    }

                    _currentImportFrame = (_currentImportFrame + 1) % (inPlayContent.GetLastFrame() + 1 - inPlayContent.GetStartFrame());
                    _currentRenderFrame++;

                    // Calculate exact time for next frame to maintain frame rate
                    _nextFrameTime += _targetFrameTime;
                    // Give more time to refill buffer when it's getting low
                    if (_buffer.Count < 5)
                    {
                        _nextFrameTime += _targetFrameTime * 0.2f; // Slight artificial delay to let buffer rebuild
                    }

                    // If we've fallen too far behind, reset timing
                    if (_timer > _nextFrameTime + 3 * _targetFrameTime)
                    {
                        _nextFrameTime = _timer + _targetFrameTime;
                    }
                }
                else
                {
                    UnityEngine.Debug.Log("Stall, Buffer runs out. Try increase buffer size.");
                }
            }

            int totalFrame = GetCurrentContent().GetDuration() <= 0 ?
                inPlayContent.GetLastFrame() :
                (int)(GetCurrentContent().GetDuration() * inPlayContent.GetFrameRate());

            if (_currentRenderFrame == totalFrame)
            {
                EndOfContent();
            }
        }

        // Only render if we have valid buffers
        if (_buffersInitialized)
        {
            RenderCurrentFrame();
        }
    }

    protected virtual void UpdateBufferData()
    {
        // Default implementation just updates buffer data without recreating buffers
        if (_buffer.Count > 0 && _posBuffer != null && _colorBuffer != null)
        {
            try
            {
                _posBuffer.SetData(_buffer.Peek().vertex);
                _colorBuffer.SetData(_buffer.Peek().color);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"Error updating buffer data: {e.Message}");
                // Buffer might be in invalid state, force recreation
                _buffersInitialized = false;
            }
        }
    }

    protected virtual void Buffering()
    {
        // Initial frame buffer fill - called at startup
        int startFrame = _currentImportFrame;
        int maxFrames = GetCurrentContent().GetLastFrame() + 1 - GetCurrentContent().GetStartFrame();

        for (int i = 0; i < Math.Min(_bufferSize * 2, maxFrames) && !_threadShouldExit; i++)
        {
            _currentImportFrame = (startFrame + i) % maxFrames;
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
        _isPlaying = true;
        _nextFrameTime = _timer + _targetFrameTime;
    }

    public bool IsPlaying => _isPlaying;

    public void Pause() { _isPlaying = false; }

    public void Replay()
    {
        _currentImportFrame = _currentRenderFrame = GetCurrentContent().GetStartFrame();
        _buffer = new MyMath.Queue<DPCFrameBuffer>(_bufferSize);

        // Clear any pending imports
        while (_framesToImport.TryDequeue(out _)) { }

        // Don't delete buffers yet - we'll do that only if necessary
        Buffering();

        if (_buffer.Count > 0)
        {
            if (_buffersInitialized && _buffer.Peek().NumVerts != _currentBufferSize)
            {
                SafeDeleteBuffers();
                SetCurrentFrameBuffer();
                _currentBufferSize = _buffer.Peek().NumVerts;
                _buffersInitialized = true;
            }
            else if (!_buffersInitialized)
            {
                SetCurrentFrameBuffer();
                _currentBufferSize = _buffer.Peek().NumVerts;
                _buffersInitialized = true;
            }
            else
            {
                // Just update existing buffers
                UpdateBufferData();
            }
        }

        Play();
    }

    public Vector3 Offset => _offset;

    public float Duration => GetCurrentContent().GetDuration() <= 0 ?
        (float)(GetCurrentContent().GetLastFrame() - GetCurrentContent().GetStartFrame() + 1) / GetCurrentContent().GetFrameRate() :
        GetCurrentContent().GetDuration();
}