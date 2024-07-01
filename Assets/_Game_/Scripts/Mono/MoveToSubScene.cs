using UnityEngine;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class MoveToSubScene : MonoBehaviour
{
    public GameObject objectToMove; // Đối tượng cần di chuyển
    public string subSceneName = "SubScene"; // Tên của SubScene

    void Start()
    {
        // Bắt đầu kiểm tra và di chuyển đối tượng
        CheckAndMoveObjectToSubScene();
    }

    async void CheckAndMoveObjectToSubScene()
    {
        // Kiểm tra liên tục cho đến khi SubScene được tải
        while (!IsSubSceneLoaded(subSceneName))
        {
            // Chờ 100ms trước khi kiểm tra lại
            await Task.Delay(100);
        }

        // Tìm Scene đích
        Scene subScene = SceneManager.GetSceneByName(subSceneName);

        if (subScene.IsValid() && subScene.isLoaded)
        {
            // Di chuyển đối tượng vào Scene đích
            SceneManager.MoveGameObjectToScene(objectToMove, subScene);
            Debug.Log($"Đã di chuyển {objectToMove.name} vào SubScene {subSceneName}");
        }
        else
        {
            Debug.LogError($"Scene {subSceneName} không hợp lệ hoặc chưa được tải. Đảm bảo SubScene đã được tải trước khi di chuyển đối tượng.");
        }
    }

    bool IsSubSceneLoaded(string sceneName)
    {
        // Kiểm tra nếu SubScene đã được tải
        Scene subScene = SceneManager.GetSceneByName(sceneName);
        return subScene.IsValid() && subScene.isLoaded;
    }
}