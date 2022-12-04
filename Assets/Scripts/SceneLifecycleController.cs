using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

public class InitEvent : UnityEvent<OSC> {}
public class ResetEvent : UnityEvent {}
public class UnloadEvent : UnityEvent {}

public class SceneLifecycleController : MonoBehaviour
{
    public UnityEvent<OSC> initEvent = new InitEvent();
    public UnityEvent resetEvent = new ResetEvent();
    public UnityEvent unloadEvent = new UnloadEvent();

    [SerializeField] private List<GameObject> receivers = new List<GameObject>();

    private void Start()
    {
        Attach(GetComponents<ILifecycleReceiver>());
        Attach(receivers.Select(r => r.GetComponent<ILifecycleReceiver>()).Where(r => r != null));
    }
    
    private void Attach(IEnumerable<ILifecycleReceiver> recvrs)
    {
        foreach(var recv in recvrs)
        {
            initEvent.AddListener(recv.OnInit);
            resetEvent.AddListener(recv.OnReset);
            unloadEvent.AddListener(recv.OnUnload);
        }
    }

    private void OnDestroy()
    {
        initEvent.RemoveAllListeners();
        resetEvent.RemoveAllListeners();
        unloadEvent.RemoveAllListeners();
    }

    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.R))
        {
            resetEvent.Invoke();
        }
    }
}

public interface ILifecycleReceiver
{
    void OnInit(OSC osc);
    void OnReset();
    void OnUnload();
}
