using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionController : MonoBehaviour
{
    [SerializeField] private string startSceneName;
    [SerializeField] private OSC osc;

    private string currentLoaddedAdditive = null;
    private SceneLifecycleController currentLifecycleController;

    void Start()
    {
        StartCoroutine(LoadScene(startSceneName));
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Escape))
        {
            #if !UNITY_EDITOR
            Application.Quit();
            #endif
            
        }
    }

    private IEnumerator LoadScene(string name)
    {
        if(!string.IsNullOrEmpty(currentLoaddedAdditive))
        {
            var sceneToUnload = SceneManager.GetSceneByName(currentLoaddedAdditive);
            currentLifecycleController.unloadEvent.Invoke();

            var sceneUnload = SceneManager.UnloadSceneAsync(sceneToUnload);
            while (!sceneUnload.isDone)
            {
                yield return null;
            }
        }
        var sceneLoad = SceneManager.LoadSceneAsync(name,UnityEngine.SceneManagement.LoadSceneMode.Additive);
        while (!sceneLoad.isDone)
        {
            yield return null;
        }

        var sceneToLoad = SceneManager.GetSceneByName(name);
        currentLifecycleController = sceneToLoad.GetRootGameObjects()
            .Select(gameObject => gameObject.GetComponent<SceneLifecycleController>())
            .Where(slc => slc != null)
            .FirstOrDefault();

        UnityEngine.Assertions.Assert.IsNotNull(currentLifecycleController);
        currentLifecycleController.initEvent.Invoke(osc);
    }
}
