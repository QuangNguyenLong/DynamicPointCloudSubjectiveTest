using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;    // For Task

/// <summary>
/// Plays one of 42 deterministic frame-rate-variation stimuli over a 30 fps source.
/// Each stimulus is defined by a StrideForFrame(int f) function that tells us
/// how many source frames we should *advance* after we render frame f.
/// A stride of 1 → show every frame; 2 → drop every second source frame, etc.
/// </summary>
public class FrameRateVariation : BasePlayer
{
    // ───────────────────── inspector ─────────────────────
    [Header("Experiment setup")]
    [SerializeField, Range(0, 41)] private int _variantIndex = 0;
    [SerializeField] private string _nextScene = "";

    [Header("Content")]
    [SerializeField] private string _contentName = "longdress";
    [SerializeField] private int _contentRate = 5;      // #points / frame in M
    [SerializeField] private int _startFrame = 0;
    [SerializeField] private int _lastFrame = 299;
    [SerializeField] private int _frameRate = 30;

    [Header("UI")]
    [SerializeField] private GameObject UI;

    // Add a lock object for thread synchronization
    private readonly object _importerLock = new object();

    // ───────────────────── internals ─────────────────────
    private readonly List<FRVVariant> _variants = new();
    private int[] _importFrames;
    private int _importCursor;
    private int _testNo;

    private DPCHandler _content;
    private string _mosPath;

    private ComputeBuffer _posBuffer, _colorBuffer;
    private RenderParams _rp;

    #region Unity lifecycle ------------------------------------------------------

    protected override void Initialize()
    {
        /* one-time parameter taken from your Stall.Init() ---------------------- */
        _offset = Vector3.zero;
        _rotation = Vector3.zero;
        _scale = Vector3.one * 0.002f;
        _tint = new Color(0.6f, 0.6f, 0.6f, 1.0f);
        if (_shader == null)
            _shader = Shader.Find("Point_BothEyes");
        _material = new Material(_shader);
        _material.EnableKeyword("_COMPUTE_BUFFER");
        _material.SetColor("_Tint", _tint);
        _material.SetMatrix("_Transform", Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));

        /* build ALL patterns once --------------------------------------------- */
        BuildVariantTable();

        if (_variantIndex < 0 || _variantIndex >= _variants.Count)
            throw new ArgumentOutOfRangeException(nameof(_variantIndex),
                "Illegal variant index (0-41 expected)");

        _importFrames = BuildPlayedSequence(_variants[_variantIndex]);
        _content = new DPCHandler(_contentName, _contentRate, _startFrame,
                         _lastFrame, _frameRate,
                         (float)TOTAL_FRAMES / SOURCE_FPS); // Set duration explicitly

        /* create result file --------------------------------------------------- */
        var dir = "MOS_FRV";
        System.IO.Directory.CreateDirectory(dir); // Ensures "MOS_FRV" directory exists
        var id = 1;
        while (System.IO.File.Exists($"{dir}/MOS.user{id}.txt")) id++;
        _mosPath = $"{dir}/MOS.user{id}.txt";
        // New header with Duration and PatternSequence columns
        string fileHeader = $"ContentName={_contentName}\nVariant,MOS,Duration(s),PatternSequence\n";
        System.IO.File.AppendAllText(_mosPath, fileHeader);
    }

    protected override int GetStartFrame() => _startFrame;
    protected override VVHandler GetCurrentContent() => _content;
    protected override void UpdatePosition() { /* not needed */ }

    protected override void EndOfContent()
    {
        Pause();
        UI.SetActive(true);
    }

    protected override void RenderCurrentFrame()
    {
        _material.SetBuffer("_Positions", _posBuffer);
        _material.SetBuffer("_Colors", _colorBuffer);
        _material.SetMatrix("_Transform", Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));
        _material.SetPass(0);

        _rp = new RenderParams(_material) { worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one) };
        if (_buffer.TryPeek(out var frame))
        {
            Graphics.RenderPrimitives(_rp, MeshTopology.Points, frame.NumVerts);
        }
    }

    protected override void DeleteBuffers()
    {
        _posBuffer?.Release(); _posBuffer = null;
        _colorBuffer?.Release(); _colorBuffer = null;
    }

    protected override void SetCurrentFrameBuffer()
    {
        if (_buffer.TryPeek(out var frame))
        {
            _posBuffer = new ComputeBuffer(frame.NumVerts, 12);
            _colorBuffer = new ComputeBuffer(frame.NumVerts, 4);
            _posBuffer.SetData(frame.vertex);
            _colorBuffer.SetData(frame.color);
        }
    }

    // In FrameRateVariation.cs

    protected override void Buffering()
    {
        // _importCursor is reset to 0 by GoToNextVariant before this method is called.
        // We aim to load _bufferSize frames, or fewer if not enough frames are left in _importFrames.
        int framesToLoadCount = Mathf.Min(_bufferSize, _importFrames.Length - _importCursor);

        if (framesToLoadCount <= 0)
        {
            UnityEngine.Debug.Log("No frames to load in Buffering.");
            return;
        }

        DPCFrameBuffer[] loadedFrames = new DPCFrameBuffer[framesToLoadCount];
        List<Task> loadingTasks = new List<Task>();

        for (int i = 0; i < framesToLoadCount; i++)
        {
            // Capture loop variables for the task closure
            int currentTaskIndex = i; // This is the index in the _importFrames array relative to the current _importCursor
            int frameBufferSlot = i;  // This is the index in the local loadedFrames array

            // Calculate the actual index in the _importFrames array
            int frameSequenceIndex = _importCursor + currentTaskIndex;

            if (frameSequenceIndex >= _importFrames.Length)
            {
                UnityEngine.Debug.LogWarning($"Attempted to load frame beyond _importFrames bounds. Index: {frameSequenceIndex}");
                continue; // Should ideally not happen due to Mathf.Min
            }

            int frameIdToLoad = _importFrames[frameSequenceIndex];

            loadingTasks.Add(Task.Run(() =>
            {
                string filePath = $"{_content.GetFullPath()}\\{_content.GetContentName()}{frameIdToLoad:D4}.ply\0";
                int vertexCount = FrameIO.PCreader.CountVertices(filePath);

                if (vertexCount <= 0)
                {
                    UnityEngine.Debug.LogError($"Cannot import frame {frameIdToLoad} from {filePath}. Vertex count: {vertexCount}");
                    // loadedFrames[frameBufferSlot] will remain null, handled later
                    return;
                }

                DPCFrameBuffer frameBuffer = new DPCFrameBuffer(vertexCount);
                bool success = FrameIO.PCreader.LoadPlyFileData(filePath, frameBuffer.vertex, frameBuffer.color);
                if (success)
                {
                    loadedFrames[frameBufferSlot] = frameBuffer;
                }
                else
                {
                    UnityEngine.Debug.LogError($"Failed to load data for frame {frameIdToLoad} from {filePath}.");
                    // loadedFrames[frameBufferSlot] will remain null
                }
            }));
        }

        // Wait for all loading tasks to complete
        // This line makes the Buffering method synchronous overall, but I/O operations run in parallel.
        // The game will pause here until all initial frames are loaded, but it should be faster.
        Task.WhenAll(loadingTasks).Wait();

        // Enqueue the loaded frames in order to maintain sequence and for thread safety with the custom Queue
        for (int i = 0; i < framesToLoadCount; i++)
        {
            if (loadedFrames[i] != null)
            {
                _buffer.Enqueue(loadedFrames[i]);
            }
            else
            {
                // Handle cases where a frame failed to load if necessary (e.g., enqueue a placeholder or skip)
                UnityEngine.Debug.LogWarning($"Frame for slot {i} (expected frame ID: {_importFrames[_importCursor + i]}) was not loaded successfully and will not be enqueued.");
            }
        }

        // Update _importCursor to reflect the frames that have been buffered
        _importCursor += framesToLoadCount;

        // Note: BasePlayer._currentImportFrame is managed differently and not directly used by
        // FrameRateVariation's ImporterNextFrame logic, which relies on _importFrames and _importCursor.
        // The original BasePlayer.Buffering incremented _currentImportFrame.
        // GoToNextVariant resets _currentImportFrame = _importFrames[0].
        // If _currentImportFrame needs specific management for BasePlayer's background importer logic to remain consistent
        // with FrameRateVariation's scheme, that would be a more complex adjustment.
        // However, the primary loading in FrameRateVariation.ImporterNextFrame uses _importCursor.
    }
    protected override void Update()
    {
        base.Update();                          // existing call

        // crude debug print, once per rendered frame
        // if (_isPlaying)
        //     Debug.Log($"[{_variantIndex}] t = {ElapsedTime:F2}s");
    }

    protected override void ImporterNextFrame()
    {
        int cursor;

        // Lock the critical section where the cursor is read and incremented
        lock (_importerLock)
        {
            if (_importCursor >= _importFrames.Length) return; // Sequence finished

            cursor = _importCursor;
            _importCursor++;
        }

        // The rest of the method now uses the local 'cursor' variable, which is safe
        var path = $"{_content.GetFullPath()}\\{_content.GetContentName()}{_importFrames[cursor]:D4}.ply\0";
        var cnt = FrameIO.PCreader.CountVertices(path);
        if (cnt <= 0)
        {
            Debug.LogError($"Cannot import {path}");
            return;
        }

        var fbuf = new DPCFrameBuffer(cnt);
        FrameIO.PCreader.LoadPlyFileData(path, fbuf.vertex, fbuf.color);
        _buffer.Enqueue(fbuf); // This is now thread-safe if you implemented the ConcurrentQueue
    }

    #endregion ------------------------------------------------------------------

    #region MOS buttons ----------------------------------------------------------
    public void SaveMOS1() { Save(1); }
    public void SaveMOS2() { Save(2); }
    public void SaveMOS3() { Save(3); }
    public void SaveMOS4() { Save(4); }
    public void SaveMOS5() { Save(5); }

    private void Save(int score)
    {
        // _importFrames holds the sequence for the variant that was just played and is now being scored.
        // _variantIndex also refers to this just-completed variant.

        // Calculate duration
        float durationSeconds = (float)TOTAL_FRAMES / SOURCE_FPS;
        if (SOURCE_FPS > 0 && _importFrames != null && _importFrames.Length > 0)
        {
            // The last frame in _importFrames is a sentinel, so actual played frames might be Length-1.
            // However, the original BuildPlayedSequence added a sentinel *after* the main loop.
            // The duration in the old DumpPattern was based on played.Count, which included the sentinel.
            // For consistency, if _importFrames includes the sentinel, use its length directly.
            // If your duration calculation logic from BuildPlayedSequence was more nuanced, replicate it here.
            // Assuming _importFrames.Length accurately reflects the number of rendered frames for FPS calculation:
            durationSeconds = (float)_importFrames.Length / SOURCE_FPS;
        }

        // Get pattern sequence string
        string patternSequenceString = _importFrames != null ? string.Join(",", _importFrames) : "N/A";

        try
        {
            // Append the extended data to the MOS file
            string mosEntry = $"{_variantIndex},{score},{durationSeconds:F2},{patternSequenceString}\n\n";
            System.IO.File.AppendAllText(_mosPath, mosEntry);
            Debug.Log($"MOS score {score}, Duration {durationSeconds:F2}s, for variant {_variantIndex} saved with pattern.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Failed to save MOS score for variant {_variantIndex}. Error: {ex.Message}");
        }

        UI.SetActive(false);
        GoToNextVariant();

    }
    #endregion ------------------------------------------------------------------

    #region Variant bookkeeping --------------------------------------------------
    private void GoToNextVariant()
    {
        // Debug.LogError($"Start going to next variant");
        if (++_variantIndex >= _variants.Count)
        {
            if (!string.IsNullOrEmpty(_nextScene))
                SceneManager.LoadScene($"{_nextScene}.unity");
            return;
        }

        // Debug.LogError($"Next variant: {_variantIndex}");
        _importFrames = BuildPlayedSequence(_variants[_variantIndex]);
        _importCursor = 0;
        _currentImportFrame = _currentRenderFrame = _importFrames[0];

        _content = new DPCHandler(_contentName, _contentRate, _startFrame,
                          _lastFrame, _frameRate,
                          (float)TOTAL_FRAMES / SOURCE_FPS); // Set duration explicitly

        // Debug.LogError($"Start dequeueing");
        while (_buffer.TryDequeue(out DPCFrameBuffer dummy)) { }
        // Debug.LogError($"Done dequeueing");
        DeleteBuffers();
        Buffering();
        SetCurrentFrameBuffer();
        Play();
    }
    #endregion ------------------------------------------------------------------

    // ────────────────────────────────────────────────────────────────────────────
    // ░░  VARIANT  DEFINITION  SECTION
    // ────────────────────────────────────────────────────────────────────────────
    #region Hand-rolled deterministic patterns

    private const int TOTAL_FRAMES = 300;
    private const int SOURCE_FPS = 30;

    private sealed class FRVVariant
    {
        public Func<int, int> FpsForFrame;   // NEW – returns the intended fps at frame f
        public string Name;
        public Func<int, int> StrideForFrame;  // f -> stride (≥1)
    }

    private void BuildVariantTable()
    {
        // ─── 0-3  Baseline (constant) ──────────────────────────────────────────
        _variants.AddRange(new[]{
            Baseline(30),  // 0
            Baseline(25),  // 1
            Baseline(20),  // 2
            Baseline(15)   // 3
        });

        // ─── 4-9  Single macro-drop (centre 135-164) ───────────────────────────
        _variants.AddRange(new[]{
            SingleDip(30,15),  // 4
            SingleDip(30,20),  // 5
            SingleDip(30,25),  // 6
            SingleDip(25,15),  // 7
            SingleDip(25,20),  // 8
            SingleDip(20,15)   // 9
        });

        // ─── 10-21  Micro-stutter: fixed width=3f (100 ms) ─────────────────────
        _variants.AddRange(MicroGrid(fixedWidth: true));

        // ─── 22-33  Micro-stutter: variable width 1/3/8f ───────────────────────
        _variants.AddRange(MicroGrid(fixedWidth: false));

        // ─── 34-39  Jitter (bounded random walk) ───────────────────────────────
        _variants.AddRange(new[]{
            Jitter(28,2,3,  seed:101),   // 34
            Jitter(28,2,15, seed:102),   // 35
            Jitter(25,5,3,  seed:103),   // 36
            Jitter(25,5,15, seed:104),   // 37
            Jitter(20,10,3, seed:105),   // 38
            Jitter(20,10,15,seed:106)    // 39
        });

        // ─── 40-41  Hard jitter controls (drop 10 % / 20 %) ────────────────────
        _variants.Add(HardJitter(0.10f, seed: 201));  // 40
        _variants.Add(HardJitter(0.20f, seed: 202));  // 41
    }

    // ---------------- helper builders -----------------------------------------

    private FRVVariant Baseline(int fps)
    {
        var stride = Mathf.RoundToInt((float)SOURCE_FPS / fps);
        return new FRVVariant
        {
            FpsForFrame = _ => fps,
            Name = $"Baseline_{fps}fps",
            StrideForFrame = _ => stride
        };
    }

    private FRVVariant SingleDip(int baseFps, int dipFps)
    {
        // centre window 135-164 inclusive = 30 frames
        var baseStride = Mathf.RoundToInt((float)SOURCE_FPS / baseFps);
        var dipStride = Mathf.RoundToInt((float)SOURCE_FPS / dipFps);

        return new FRVVariant
        {
            FpsForFrame = f => (f >= 135 && f <= 164) ? dipFps : baseFps,
            Name = $"SingleDip_{baseFps}to{dipFps}_30f",
            StrideForFrame = f => (f >= 135 && f <= 164) ? dipStride : baseStride
        };
    }

    private IEnumerable<FRVVariant> MicroGrid(bool fixedWidth)
    {
        int[] bases = { 30, 30, 30, 30, 25, 25, 25, 25, 20, 20, 20, 20 };
        int[] dips = { 25, 20, 25, 20, 20, 15, 20, 15, 15, 15, 15, 15 };
        int[] ints = { 60, 60, 120, 120, 60, 60, 120, 120, 60, 60, 120, 120 };

        for (int i = 0; i < bases.Length; i++)
        {
            int baseFps = bases[i], dipFps = dips[i], interval = ints[i];
            yield return MicroStutter(baseFps, dipFps, interval, fixedWidth);
        }
    }

    private FRVVariant MicroStutter(int baseFps, int dipFps, int intervalFrames,
                                    bool fixedWidth)
    {
        var baseStride = Mathf.RoundToInt((float)SOURCE_FPS / baseFps);
        var dipStride = Mathf.RoundToInt((float)SOURCE_FPS / dipFps);
        int[] widths = fixedWidth ? new[] { 3 } : new[] { 1, 3, 8 };
        string name = fixedWidth
            ? $"Micro_{baseFps}to{dipFps}_Every{intervalFrames}f_W3f"
            : $"MicroVar_{baseFps}to{dipFps}_Every{intervalFrames}f_W1-3-8f";

        return new FRVVariant
        {
            FpsForFrame = f =>
            {
                int cycle = f / intervalFrames;
                int w = widths[cycle % widths.Length];
                return w == 0 ? baseFps : dipFps;
            },
            Name = name,
            StrideForFrame = f =>
            {
                int cycle = f / intervalFrames;
                int off = f % intervalFrames;
                int w = widths[cycle % widths.Length];
                return off < w ? dipStride : baseStride;
            }
        };
    }

    private FRVVariant Jitter(int targetFps, int range, int stepPeriod, int seed)
    {
        var r = new System.Random(seed);
        // pre-compute stride table (one entry per step)
        int steps = Mathf.CeilToInt((float)TOTAL_FRAMES / stepPeriod) + 1;
        int[] table = new int[steps];
        int curFps = targetFps;

        for (int i = 0; i < steps; i++)
        {
            table[i] = Mathf.RoundToInt((float)SOURCE_FPS / curFps);

            int delta = r.Next(3) - 1; // {-1,0,+1}
            curFps = Mathf.Clamp(curFps + delta, targetFps - range, targetFps + range);
        }

        return new FRVVariant
        {
            FpsForFrame = _ => targetFps,
            Name = $"Jitter_{targetFps}_±{range}_Every{stepPeriod}f",
            StrideForFrame = f => table[f / stepPeriod]
        };
    }

    private FRVVariant HardJitter(float dropRatio, int seed)
    {
        var rnd = new System.Random(seed);
        var drops = new HashSet<int>();
        int totalDrops = Mathf.RoundToInt(dropRatio * TOTAL_FRAMES);
        while (drops.Count < totalDrops)
            drops.Add(rnd.Next(TOTAL_FRAMES));

        return new FRVVariant
        {
            FpsForFrame = _ => SOURCE_FPS,
            Name = $"HardJitter_Drop{dropRatio:P0}",
            StrideForFrame = f => drops.Contains(f) ? 2 : 1  // skip this frame? stride=2
        };
    }

    #endregion ------------------------------------------------------------------

    private int[] BuildPlayedSequence(FRVVariant v)
    {
        var schedule = new List<int>(TOTAL_FRAMES); // The schedule will have TOTAL_FRAMES entries
        float deliveryClock = 0.0f;
        int lastDeliveredFrameIndex = -1;
        int lastShownFrame = _startFrame; // Initialize with the first frame

        for (int displayFrame = 0; displayFrame < TOTAL_FRAMES; ++displayFrame)
        {
            float targetFps = v.FpsForFrame(displayFrame);

            // The ideal "live" frame we should be seeing if there were no issues.
            // Assuming DISPLAY_FPS is the same as SOURCE_FPS for simplicity in this calculation,
            // as the python script implies TOTAL_DISPLAY_FRAMES corresponds to the source content length.
            // If TOTAL_DISPLAY_FRAMES represents actual display frames and SOURCE_LAST_FRAME is based on SOURCE_FPS,
            // then ideal_live_frame = (displayFrame / (float)TOTAL_DISPLAY_FRAMES) * (SOURCE_LAST_FRAME + 1);
            // However, the python script uses ideal_live_frame = int(display_frame * (SOURCE_FPS / DISPLAY_FPS))
            // where DISPLAY_FPS is 30.0 and SOURCE_FPS is 30.0, so ideal_live_frame = display_frame.
            // Let's stick to the direct translation from the python script's intent for ideal_live_frame:
            int idealLiveFrame = displayFrame; // Because SOURCE_FPS (30) / DISPLAY_FPS (30) = 1

            // Advance our delivery clock based on the current target FPS relative to display FPS.
            // delivery_clock += target_fps / DISPLAY_FPS;
            // The display FPS is implicitly 30 (because TOTAL_FRAMES is 300 over 30 FPS content)
            deliveryClock += targetFps / SOURCE_FPS; // Use SOURCE_FPS as the reference for calculation from python script

            int currentDeliveredFrameIndex = Mathf.FloorToInt(deliveryClock);

            int frameToShow;
            if (currentDeliveredFrameIndex > lastDeliveredFrameIndex)
            {
                // A new frame arrived! Show the current IDEAL "live" frame. This creates the skip.
                frameToShow = idealLiveFrame;
                lastShownFrame = frameToShow;
            }
            else
            {
                // No new frame was delivered. We must hold the last frame we showed. This creates the stall.
                frameToShow = lastShownFrame;
            }

            // Ensure we don't go beyond the last available source frame
            schedule.Add(Mathf.Min(frameToShow, _lastFrame));
            lastDeliveredFrameIndex = currentDeliveredFrameIndex;
        }

        // Python script's finalization: ensure the very last frame is shown if needed
        // This part ensures that if the simulation was supposed to finish but stalled at the end,
        // the very last source frame (SOURCE_LAST_FRAME) is shown.
        // In our context, _lastFrame is equivalent to SOURCE_LAST_FRAME.
        if (lastShownFrame < _lastFrame)
        {
            if (deliveryClock >= _lastFrame) // Check if the content delivery clock has reached the end
            {
                schedule[TOTAL_FRAMES - 1] = _lastFrame;
            }
        }

        // The original C# code adds a sentinel for BasePlayer. Let's keep that.
        // This sentinel is a duplicate of the last frame, which is used by BasePlayer for buffering logic.
        // schedule.Add(schedule[^1]);

        // For debugging, you might want to uncomment this to see the generated sequence
        // Debug.Log($"Generated Sequence for {v.Name}: {string.Join(",", schedule)}");

        return schedule.ToArray();
    }
}
