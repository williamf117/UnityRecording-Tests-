using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
/// <summary>
/// a class to hold and maintain a dictionary of all recording names and file names 
/// </summary>
public static class RecordingDictionaryClass 
{
    public static Dictionary<string, string> RecordingDictionary;
    private static bool init = false;
    // Start is called before the first frame update

   

    public static void Init() {
        if (init == false)
        {
            RecordingDictionary = new Dictionary<string, string>();
            load();
            init = true;
        }
    
    }
    /// <summary>
    /// add an entry to the dictionary 
    /// </summary>
    /// <param name="name">name that shoud appear on buttons </param>
    /// <param name="recordFile">name of the .record </param>
    /// <param name="audioFile">name of wav </param>
    public static void addRecording(string name, string recordFile, string audioFile)
    {
        if (RecordingDictionary.ContainsKey(name)) 
        { 
            Debug.Log("name already exists ");
            return;
        }
        RecordingDictionary.Add(name, recordFile + "," + audioFile);
    }
    /// <summary>
    /// saves the dictionary to a csv named Dictionary.csv
    /// </summary>
    public static void SaveDictionary()
    {
        StreamWriter sw = new StreamWriter(Application.streamingAssetsPath+ "/Dictionary.csv");
        
        foreach(KeyValuePair<string,string> i in RecordingDictionary)
        {
            sw.WriteLine(i.Key + "," + i.Value);

        }
        sw.Close();
    }
    /// <summary>
    /// loads the dictionary from the csv 
    /// </summary>
    public static void load()
    {
        try
        {
            StreamReader sr = new StreamReader(Application.streamingAssetsPath + "/Dictionary.csv");
            //read through the file line by line each line is an entery in the dic 
            while (!sr.EndOfStream)
            {
                string line = sr.ReadLine();
                string[] input = line.Split(',');
                RecordingDictionaryClass.addRecording(input[0], input[1], input[2]);
            }
            sr.Close();
        }
        catch
        {
            Debug.Log("file dose not exist");
        }
    }

}
