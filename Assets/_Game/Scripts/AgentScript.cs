using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AgentScript : MonoBehaviour
{
    private void OnEnable()
    {
        if (MovermentManagerJOB.Instance)
        {
            Debug.Log("Hello1");
            MovermentManagerJOB.Instance.AddAgent(transform);
        }
        else
        {
            Debug.Log("Hello");
        }
    }
}
