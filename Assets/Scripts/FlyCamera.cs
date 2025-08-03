using UnityEngine;

[ExecuteInEditMode]
[System.Serializable]
public class FlyCamera : MonoBehaviour
{

    /*
    EXTENDED FLYCAM
        Desi Quintans (CowfaceGames.com), 17 August 2012.
        Based on FlyThrough.js by Slin (http://wiki.unity3d.com/index.php/FlyThrough), 17 May 2011.
    LICENSE
        Free as in speech, and free as in beer.
    FEATURES
        WASD/Arrows:    Movement
                  Q:    Climb
                  E:    Drop
                      Shift:    Move faster
                    Control:    Move slower
 
     Extension: Roger Cabo 05.2019 - Unity 2019.1 update. Activate and deactivate by press Space in Unity Editor to be able to edit in Editor Mode.
                    Space/ECS: Toggle cursor locking to screen (you can also press Ctrl+P, ESC to toggle play mode on and off).
                   [Optional] Pick Scene View Camera position and rotation as initial value
    */

    public float cameraRotationSpeed = 90;
    public float climbSpeed = 4;
    public float normalMoveSpeed = 10;
    public float slowMoveFactor = 0.25f;
    public float fastMoveFactor = 3;
    public bool pickSceneViewCam = false;

    private float rotationX = 0.0f;
    private float rotationY = 0.0f;

    [HideInInspector]
    [SerializeField]
    public Vector3 sceneCamRotation;
    [HideInInspector]
    [SerializeField]
    public Vector3 sceneCamPosition;

    void Start()
    {
#if UNITY_EDITOR
        // Fix: On starting the Unity Editor, all Monobehaviors with [ExecuteInEditMode] excecuted
        // the Start() method. This cause an invivible mouse cursor if Unity is fully initialized.
        if (Application.isPlaying)
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
#endif
        SetOriginCameraRotation();
    }

    private void OnEnable()
    {
        SetOriginCameraRotation();
    }

    void Update()
    {

#if UNITY_EDITOR

        Camera sceneCam = UnityEditor.SceneView.GetAllSceneCameras()[0];
        if (sceneCam)
        {
            sceneCamRotation = sceneCam.transform.eulerAngles;
            sceneCamPosition = sceneCam.transform.position;
        }

        if (!Application.isPlaying) return;

        if (Cursor.lockState == CursorLockMode.Locked)
        {
#endif
            rotationX += Input.GetAxis("Mouse X") * cameraRotationSpeed * Time.deltaTime;
            rotationY += Input.GetAxis("Mouse Y") * cameraRotationSpeed * Time.deltaTime;
            rotationY = Mathf.Clamp(rotationY, -90, 90);

            transform.localRotation = Quaternion.AngleAxis(rotationX, Vector3.up);
            transform.localRotation *= Quaternion.AngleAxis(rotationY, Vector3.left);

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                transform.position += transform.forward * (normalMoveSpeed * fastMoveFactor) * Input.GetAxis("Vertical") * Time.deltaTime;
                transform.position += transform.right * (normalMoveSpeed * fastMoveFactor) * Input.GetAxis("Horizontal") * Time.deltaTime;
            }
            else if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                transform.position += transform.forward * (normalMoveSpeed * slowMoveFactor) * Input.GetAxis("Vertical") * Time.deltaTime;
                transform.position += transform.right * (normalMoveSpeed * slowMoveFactor) * Input.GetAxis("Horizontal") * Time.deltaTime;
            }
            else
            {
                transform.position += transform.forward * normalMoveSpeed * Input.GetAxis("Vertical") * Time.deltaTime;
                transform.position += transform.right * normalMoveSpeed * Input.GetAxis("Horizontal") * Time.deltaTime;
            }

            if (Input.GetKey(KeyCode.Q)) { transform.position += transform.up * climbSpeed * Time.deltaTime; }
            if (Input.GetKey(KeyCode.E)) { transform.position -= transform.up * climbSpeed * Time.deltaTime; }

#if UNITY_EDITOR
        }
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = (Cursor.lockState == CursorLockMode.None) ? CursorLockMode.Locked : CursorLockMode.None;
        }
#endif
    }

    /// <summary>
    /// Make sure the camera look into the original direction
    /// </summary>
    void SetOriginCameraRotation()
    {
        // Swap x and y because of the additionally rotation logic in Update
        rotationX = this.transform.eulerAngles.y;
        rotationY = -this.transform.eulerAngles.x;

#if UNITY_EDITOR
        if (pickSceneViewCam)
        {
            // Swap x and y because of the additionally rotation logic in Update
            rotationX = sceneCamRotation.y;
            rotationY = -sceneCamRotation.x;
            this.transform.position = sceneCamPosition;
        }
#endif
    }

}