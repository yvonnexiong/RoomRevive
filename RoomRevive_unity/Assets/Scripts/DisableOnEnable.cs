using UnityEngine;

public class DisableOnEnable : MonoBehaviour
{
    private void OnValidate()
    {
        this.gameObject.SetActive(false);
    }
    private void OnEnable()
    {
        this.gameObject.SetActive(false);
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
