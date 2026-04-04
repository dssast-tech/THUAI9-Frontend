using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float rotationSpeed = 100f; // Adjusted for better mouse control
    public float verticalSpeed = 10f;
    public float zoomSpeed = 500f;

    private void Update()
    {
        // WASD movement
        float h = Input.GetAxis("Horizontal"); // A/D
        float v = Input.GetAxis("Vertical");   // W/S
        
        Vector3 forward = transform.forward;
        forward.y = 0; // Keep movement horizontal
        forward.Normalize();
        
        Vector3 right = transform.right;
        right.y = 0;
        right.Normalize();
        
        Vector3 moveDir = forward * v + right * h;
        transform.position += moveDir * moveSpeed * Time.deltaTime;

        // Up/Down movement (Keyboard Up and Down arrows)
        float verticalMove = 0f;
        if (Input.GetKey(KeyCode.UpArrow))
        {
            verticalMove = 1f;
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            verticalMove = -1f;
        }
        transform.position += Vector3.up * verticalMove * verticalSpeed * Time.deltaTime;

// Right mouse button rotation (Horizontal and Vertical)
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            // Horizontal rotation (around Y axis)
            transform.Rotate(Vector3.up, mouseX * rotationSpeed * Time.deltaTime, Space.World);
            
            // Vertical rotation (around local X axis)
            transform.Rotate(Vector3.right, -mouseY * rotationSpeed * Time.deltaTime, Space.Self);

            // Keep Z rotation at 0 to prevent tilting
            Vector3 eulerAngles = transform.eulerAngles;
            eulerAngles.z = 0;
            transform.eulerAngles = eulerAngles;
        }

        // Mouse scroll wheel zoom
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0f)
        {
            transform.position += transform.forward * scroll * zoomSpeed * Time.deltaTime;
        }
    }
}
