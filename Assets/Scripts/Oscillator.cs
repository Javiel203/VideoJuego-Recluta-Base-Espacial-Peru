using UnityEngine;

public class Oscillator : MonoBehaviour
{
    [SerializeField] float speed = 1f;
    [SerializeField] Vector3 movementVector;
    Vector3 startPosision;
    Vector3 endPosision;
    float movementFactor;
    void Start()
    {
        startPosision = transform.position;
        endPosision = startPosision + movementVector;

    }

    // Update is called once per frame
    void Update()
    {
        movementFactor = Mathf.PingPong(Time.time * speed, 1);
        transform.position = Vector3.Lerp(startPosision, endPosision, movementFactor);

    }
}
