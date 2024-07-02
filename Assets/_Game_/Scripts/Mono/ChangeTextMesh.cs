using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class ChangeTextMesh : MonoBehaviour
{
    public List<TextData> textDatas;
    private bool _isInitEvent;

    public void Update()
    {
        if (!_isInitEvent)
        {
            UpdateHybrid playerSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<UpdateHybrid>();
            if(playerSystem == null) return;
            playerSystem.UpdateText += ChangeText;
            _isInitEvent = true;
        }
    }

    private void ChangeText(int idText, int value)
    {
        foreach (var textData in textDatas)
        {
            if (textData.id == idText)
            {
                textData.textMesh.text = value.ToString();
                if (value == 0)
                {
                    textData.textMesh.gameObject.SetActive(false);
                    textDatas.Remove(textData);
                }
                break;
            }
        }
    }
}

[Serializable]
public struct TextData
{
    public int id;
    public TextMesh textMesh;
}