using System;
using Unity.Entities;
using UnityEngine;

public class ChangeTextMesh : MonoBehaviour
{
    public TextData[] textDatas;
    private bool _isInitEvent;

    public void Update()
    {
        Debug.Log("Hellvoealksdfjlakjsdlkfáldkfj1111");
        if (!_isInitEvent)
        {
            UpdateHybrid playerSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<UpdateHybrid>();
            if(playerSystem == null) return;
            playerSystem.UpdateText += ChangeText;
            Debug.Log("Hellvoealksdfjlakjsdlkfáldkf9999");
            _isInitEvent = true;
        }
    }

    private void ChangeText(int idText, int value)
    {
        Debug.Log("Hellvoealksdfjlakjsdlkfáldkfj");
        foreach (var textData in textDatas)
        {
            if (textData.id == idText)
            {
                textData.textMesh.text = value.ToString();
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