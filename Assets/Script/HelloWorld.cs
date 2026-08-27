using UnityEngine;

public class HelloWorld : MonoBehaviour
{
    [SerializeField]
    private string message = "Hello, World!";

    private void Start()
    {
        Debug.Log(message);
    }
}
