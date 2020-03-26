using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UltimateReplay;
using UltimateReplay.Storage;
using UnityEngine.SceneManagement;
/// <summary>
/// an over arching controler for the recording 
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    [SerializeField]
    int wait=1;
    /// <summary>
    /// make the game manager a singlton only one may exist 
    /// </summary>
    private void Awake()
    {
        if (GameManager.Instance == null)
        {
            GameManager.Instance = this;
        }
        else
        {
            Destroy(this);
        }
       
    }

    // Start is called before the first frame update
    void Start()
    {
        DontDestroyOnLoad(this);
        RecordingDictionaryClass.Init();
    }

    // Update is called once per frame
    void Update()
    {
        //escape function 
        //if (Input.GetKeyDown(KeyCode.Escape))
        //{
        //    SceneManager.LoadScene(0);

        //}
    }

    //menue methods 
    //this should load into the play scene and then use the replay controler to start or create a recording

    /// <summary>
    /// start a new recording 
    /// </summary>
    /// <param name="name"></param>
    public void NewRecording(string name)
    {
        //load the main scene
        SceneManager.LoadScene(1);
        //call a coroutine so I can wait untill every thing is loaded befor trying to start a recording 
        StartCoroutine(newRecording( name));    
    }
    /// <summary>
    /// Start playing back a recording 
    /// </summary>
    /// <param name="key"></param>
    public void playRecording(string key)
    {
        //load the main scene 
        SceneManager.LoadScene(1);
        //use a coroutine to allow evert thing to load before calling any other methods.
        StartCoroutine(play(key));
    }
    /// <summary>
    /// allows the program to wait untill every thing is loaded  before calling creat recording 
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    IEnumerator newRecording(string name)
    {
       yield return new WaitForSeconds (wait);
        GameObject.Find("Replay").GetComponent<ReplayControler>().createRecording(name);
    }
    /// <summary>
    /// allows the program to wait untill every thing is loaded  before calling start replay
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    IEnumerator play(string name)
    {
        yield return new WaitForSeconds(wait);
        GameObject.Find("Replay").GetComponent<ReplayControler>().SelectandStartReplay(name);
    }

    /// <summary>
    /// changes the filepath for the recording files 
    /// </summary>
    /// <param name="filePath"></param>
    public static void SetTargetFilePath(string filePath)
    {
        SavWav.FilePath = filePath;
        ReplayFileTarget target = ReplayManager.Target as ReplayFileTarget;



        // Set the location/name of the replay file to load

        target.FileOutputDirectory =filePath;
    }

}
