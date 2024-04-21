using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TrainingManager : MonoBehaviour
{
    [SerializeField] private string nextcontent = "longdress";
    [SerializeField] private string TestType = "VersionSwitch";
    [SerializeField] private DPCPlayer[] versions;

    [SerializeField] private GameObject UI;

    private int index = 0;

    private bool _isRunning = false;
    public void Run()
    {
        _isRunning = true;
        versions[index].Play();
    }

    // Start is called before the first frame update
    void Start()
    {
        if (versions == null) UnityEngine.Debug.Log("List of content is unassigned!");

        versions[index].gameObject.SetActive(true);
    }

    // Update is called once per frame
    void Update()
    {
        if (_isRunning)
        {
            if (versions[index].FramesLeft == 0)
            {
                versions[index].gameObject.SetActive(false);
                UI.SetActive(true);
                if (index < versions.Length - 1)
                {
                    index++;
                    versions[index].gameObject.SetActive(true);
                }
                else
                {
                    if (nextcontent != null)
                        SceneManager.LoadScene($"Assets/Scenes/{TestType}/{nextcontent}{TestType}.unity");
                    return;
                }
            }
        }
    }
}
