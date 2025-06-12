using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.SceneManagement;

[System.Serializable]
public class IntArray5
{
    public int segment_1;
    public int segment_2;
    public int segment_3;
    public int segment_4;
    public int segment_5;

    public int[] ToArray()
    {
        return new int[] { segment_1, segment_2, segment_3, segment_4, segment_5 };
    }
}

public class VersionSwitch : BasePlayer
{
    [SerializeField] private string contentName = "longdress";
    [SerializeField] private string nextcontent = "soldier";
    [SerializeField] private List<IntArray5> combinations;
    [SerializeField] private GameObject UI;
    protected ComputeBuffer _posBuffer;
    protected ComputeBuffer _colorBuffer;

    private List<TestHandler.TestSample>[] _combinations;
    private int _TestNo = 0;

    private string _MOSDataPath;
    private string _MOSFolder = "MOS_VERSIONSWITCH";

    protected override void DeleteBuffers()
    {
        if (_posBuffer != null)
        {
            _posBuffer.Release();
            _posBuffer = null;
        }
        if (_colorBuffer != null)
        {
            _colorBuffer.Release();
            _colorBuffer = null;
        }
    }

    protected override VVHandler GetCurrentContent()
    {
        if (_currentImportFrame == -1)
            _currentImportFrame = 0;
        return _combinations[_TestNo][_currentImportFrame % 300 / 60].Content;
    }

    protected override int GetStartFrame() => GetCurrentContent().GetStartFrame();
    protected override void ImporterNextFrame()
    {
        //UnityEngine.Debug.Log($"RenderFrame: {_currentRenderFrame}, ImportFrame: {_currentImportFrame}");

        var inPlayContent = GetCurrentContent();
        string filename = inPlayContent.GetFullPath() + "\\" + inPlayContent.GetContentName() + (_currentImportFrame % (inPlayContent.GetLastFrame() + 1 - inPlayContent.GetStartFrame())).ToString("D4") + ".ply\0";
        int count = FrameIO.PCreader.CountVertices(filename);

        if (count <= 0)
            UnityEngine.Debug.LogError("Fail to import frame. Filename: " + filename);

        DPCFrameBuffer temp = new(count);
        FrameIO.PCreader.LoadPlyFileData(filename, temp.vertex, temp.color);
        _buffer.Enqueue(temp);
    }

    protected override void Initialize()
    {
        _offset = new(0f, 0f, 0f);
        _scale = new(0.002f, 0.002f, 0.002f);
        _rotation = new(0f, 0f, 0f);
        _tint = new(0.6f, 0.6f, 0.6f);

        if (_shader == null)
            _shader = Shader.Find("Point_BothEyes");
        _material = new Material(_shader);

        _material.EnableKeyword("_COMPUTE_BUFFER");
        _material.SetColor("_Tint", _tint);
        _material.SetMatrix("_Transform", Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));

        if (combinations.Count == 0)
        {
            _combinations = new List<TestHandler.TestSample>[29];
            _combinations[0] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 1) };
            _combinations[1] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 2) };
            _combinations[2] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 3) };
            _combinations[3] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 4) };
            _combinations[4] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[5] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[6] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[7] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[8] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[9] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 1) };
            _combinations[10] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 2) };
            _combinations[11] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 3) };
            _combinations[12] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 4) };
            _combinations[13] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[14] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[15] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[16] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[17] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 1) };
            _combinations[18] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 2) };
            _combinations[19] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 3) };
            _combinations[20] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 4) };
            _combinations[21] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[22] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 4), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[23] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[24] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 3), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[25] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[26] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 2), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[27] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 5) };
            _combinations[28] = new List<TestHandler.TestSample> { new TestHandler.TestSample(3, contentName, 5), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 1), new TestHandler.TestSample(3, contentName, 5) };
        }
        else
        {
            _combinations = new List<TestHandler.TestSample>[combinations.Count];
            for (int i = 0; i < combinations.Count; i++)
            {
                var intSet = combinations[i].ToArray();
                _combinations[i] = new List<TestHandler.TestSample>();
                for (int j = 0; j < intSet.Length; j++)
                {
                    _combinations[i].Add(new TestHandler.TestSample(3, contentName, intSet[j] + 1));
                }
            }
        }
        // Create opinion score file 
        int count = 1;
        Directory.CreateDirectory($"{_MOSFolder}");
        while (File.Exists($"{_MOSFolder}/MOS.user{count}.txt"))
        {
            count++;
        }
        _MOSDataPath = $"{_MOSFolder}/MOS.user{count}.txt";
        File.AppendAllText(_MOSDataPath, $"ContentName={contentName}\nId,MOS\n");
        //
    }

    RenderParams _rp;
    protected override void RenderCurrentFrame()
    {
        _material.SetBuffer("_Positions", _posBuffer);
        _material.SetBuffer("_Colors", _colorBuffer);
        _material.SetMatrix("_Transform", Matrix4x4.TRS(_offset, Quaternion.Euler(_rotation), _scale));
        _material.SetPass(0);
        _rp = new RenderParams(_material);
        _rp.worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one);
        Graphics.RenderPrimitives(_rp, MeshTopology.Points, _buffer.Peek().NumVerts);
    }

    protected override void SetCurrentFrameBuffer()
    {
        _posBuffer = new ComputeBuffer(_buffer.Peek().NumVerts, 12);
        _colorBuffer = new ComputeBuffer(_buffer.Peek().NumVerts, 4);

        _posBuffer.SetData(_buffer.Peek().vertex);
        _colorBuffer.SetData(_buffer.Peek().color);
    }

    protected override void UpdatePosition()
    {
        if (_currentRenderFrame == GetCurrentContent().GetLastFrame())
        {
            Pause();
            UI.SetActive(true);
        }
        return;
    }
    public void GoToNextTestSample()
    {
        Pause();
        DeleteBuffers();
        if (_TestNo == _combinations.Length - 1)
        {
            if (nextcontent != null)
                SceneManager.LoadScene($"Assets/Scenes/{nextcontent}VersionSwitch.unity");
            return;
        }
        _TestNo++;
        _currentImportFrame = _currentRenderFrame = GetCurrentContent().GetStartFrame();
        _buffer = new MyMath.Queue<DPCFrameBuffer>(_bufferSize);
        Buffering();
        SetCurrentFrameBuffer();
        Play();
        _startedImport = true;
    }

    public void SaveMOS5() { File.AppendAllText(_MOSDataPath, $"{_TestNo},5\n"); }
    public void SaveMOS4() { File.AppendAllText(_MOSDataPath, $"{_TestNo},4\n"); }
    public void SaveMOS3() { File.AppendAllText(_MOSDataPath, $"{_TestNo},3\n"); }
    public void SaveMOS2() { File.AppendAllText(_MOSDataPath, $"{_TestNo},2\n"); }
    public void SaveMOS1() { File.AppendAllText(_MOSDataPath, $"{_TestNo},1\n"); }

}