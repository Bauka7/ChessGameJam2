using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Позиции камеры")]
    public Vector3 whitePosition = new Vector3(0, 10, -5);   // позиция для белых
    public Vector3 blackPosition = new Vector3(0, 10, 13);   // позиция для чёрных

    public Vector3 whiteRotation = new Vector3(45, 0, 0);    // угол для белых
    public Vector3 blackRotation = new Vector3(45, 180, 0);  // угол для чёрных

    [Header("Скорость поворота")]
    public float rotateSpeed = 2f;

    private Vector3 targetPosition;
    private Quaternion targetRotation;

    private void Start()
    {
        // Начальная позиция — сторона белых
        transform.position = whitePosition;
        transform.rotation = Quaternion.Euler(whiteRotation);
        targetPosition = whitePosition;
        targetRotation = Quaternion.Euler(whiteRotation);
    }

    private void Update()
    {
        // Плавно двигаем камеру к цели
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * rotateSpeed);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * rotateSpeed);
    }

    // Вызывается из BoardManager при смене хода
    public void SwitchToWhite()
    {
        targetPosition = whitePosition;
        targetRotation = Quaternion.Euler(whiteRotation);
    }

    public void SwitchToBlack()
    {
        targetPosition = blackPosition;
        targetRotation = Quaternion.Euler(blackRotation);
    }
}