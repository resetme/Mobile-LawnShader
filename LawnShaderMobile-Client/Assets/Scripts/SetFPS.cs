using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetFPS : MonoBehaviour
{
    void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
            QualitySettings.vSyncCount = 1;
            #endif  
                
#if UNITY_IOS && !UNITY_EDITOR
            Application.targetFrameRate = 60;
            #endif  
    }
}
