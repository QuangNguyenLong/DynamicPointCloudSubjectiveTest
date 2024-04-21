using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
public class TestHandler
{
    public class TestSample
    {
        public DPCHandler Content;
        public float Distance;
        public int Id { get; set; }
        public TestSample(float distance, string contentName, int contentRate = 5, int startFrame = 0, int lastFrame = 299, int frameRate = 30)
        {
            this.Distance = distance;
            Content = new DPCHandler(contentName, contentRate, startFrame, lastFrame, frameRate);
        }

        public TestSample(TestSample other)
        {
            this.Distance = other.Distance;
            Content = other.Content;
        }

        public string GetInfo()
        {
            return $"{Content.GetContentName()},{Content.GetContentRate()},{Content.GetFrameRate()},{Distance}";
        }
    }

    public class TestInfo
    {
        public List<List<List<TestSample>>> Test;
        public int Count;
        public TestInfo(List<string> names, List<int> rates, List<float> distances)
        {

            Count = names.Count * rates.Count * distances.Count;

            Test = new List<List<List<TestSample>>>();
            for (int i = 0; i < names.Count; i++)
            {
                Test.Add(new List<List<TestSample>>());
                for (int j = 0; j < rates.Count; j++)
                {
                    Test[i].Add(new List<TestSample>());
                    for (int k = 0; k < distances.Count; k++)
                    {
                        Test[i][j].Add(new TestSample(distances[k], names[i], rates[j]));
                    }
                }
            }
        }
        public List<TestSample> GetTestSequence()
        {
            List<TestSample> seq = new();
            foreach (var nameList in Test)
            {
                foreach (var rateList in nameList)
                {
                    foreach (var sample in rateList)
                    {
                        sample.Id = seq.Count;
                        seq.Add(sample);
                    }
                }
            }
            return seq;
        }

    }
    static public int[] SaveTestSquence(string fileName, List<TestSample> testList)
    {
        int[] shuffle = new int[testList.Count];
        try
        {
            // Check if file dont exists. If yes, create new file.
            if (!File.Exists(fileName))
            {
                shuffle = MyMath.ShuffleArray(0, testList.Count - 1);

                // Create a new file
                FileStream fs = File.Create(fileName);

                // save shuffle Id array
                string shuf = "";
                foreach (var e in shuffle)
                    shuf += e.ToString() + " ";
                Byte[] head = new UTF8Encoding(true).GetBytes(shuf + "\n");
                fs.Write(head, 0, head.Length);

                for (int Id = 0; Id < testList.Count; Id++)
                {
                    Byte[] line = new UTF8Encoding(true).GetBytes($"No{Id}.{testList[Id].GetInfo()}\n");
                    fs.Write(line, 0, line.Length);
                }

            }
            else // else update shuffle
            {
                string[] header = File.ReadLines(fileName).First().Split(' ');
                for (int i = 0; i < header.Length; i++)
                    if (header[i] != "")
                        shuffle[i] = Convert.ToInt32(header[i]);
            }
        }
        catch (Exception Ex)
        {
            UnityEngine.Debug.Log(Ex.ToString());
        }

        return shuffle;
    }
}