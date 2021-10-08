using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    // ----- variables
    //model
    private Rigidbody m_Rigidbody;
    private Quaternion m_Rotation;

    [Header("Player Movement")]
    public float _movementSpeed = 7f;
    public float _jumpHeight = 7f;
    public float _distancetoBarrierDeath = 100f; // death occurs after distance traveled
    private Vector3 p_playerSpawn;
    private bool p_isGrounded = true;
    private float p_deathBarrier; // derived from dDistance once to determine how far to fall // should be private
    private bool p_canInstDB = true; // used in falling respawn

    [Header("Camera")]
    new public GameObject camera;
    public float _mouseXSpeed = 1f;
    public float _mouseYSpeed = 1f;
    [Range(1f, 60f)]
    public float _maxLookHeight = 60;
    [Range(-1f, -60f)]
    public float _minLookHeight = -60;
    private Quaternion c_Rotation;

    [Header("Respawn")]
    public GameObject canvas;
    private Image r_fog;
    private bool r_respawning;
    private bool r_respawnOnce;


    // Start is called before the first frame update
    void Start()
    {
        //save players origin as spawn
        p_playerSpawn = this.transform.position;
        m_Rotation = this.transform.rotation;

        //initializes the Player and Camera
        m_Rigidbody = GetComponent<Rigidbody>();
        m_Rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ |RigidbodyConstraints.FreezeRotationY;
        Cursor.lockState = CursorLockMode.Locked;
        c_Rotation = camera.transform.rotation;

        //initialization for respawn
        canvas.SetActive(true);
        r_fog = canvas.GetComponentInChildren<Image>();
        r_respawning = true;
        r_respawnOnce = false;
        //fog.canvasRenderer.SetAlpha(0);

    }
    // Update is called once per frames
    void Update()
    {
        //move the player
        MoveController();

        //player view control X, Y
        ViewController();

        if (Input.GetKeyDown(KeyCode.E))
        {
            SaveSpawn();
        }

        //respawn animation starter
        if (r_respawning && r_respawnOnce == false)
        {
            r_respawnOnce = true;
            r_fog.CrossFadeAlpha(1, .5f, false); // put up respawn fog
            Debug.Log("Run this Once");
            StartCoroutine("RespawnAnimation");
        }
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Killbox") // used for objects (like spikes)
        { 
            r_respawning = true;
        }
        if (other.tag == "Checkpoint")
        {
            SaveSpawn();
        }
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Ground"))
        { 
            p_isGrounded = true;
            p_deathBarrier = -9999999; // set to unrealisticly reachable number as to not walk into it
            p_canInstDB = true;
        }
    }
    private void OnCollisionExit(Collision collision)
    {
        p_isGrounded = false;
    }
    private void SaveSpawn()
    {
        Debug.Log("saving spawn position");
        p_playerSpawn = this.transform.position;
    }
    private void Jump()
    {
        m_Rigidbody.AddForce(Vector3.up * _jumpHeight, ForceMode.Impulse);
        p_isGrounded = false;
    }
    private void Fall()
    {
        float accelerationBoost = 2 * Time.deltaTime;
        gameObject.transform.Translate(0, accelerationBoost * Time.deltaTime, 0);
        InstKillBarrier();   
    }
    private void InstKillBarrier()
    {
        if (p_canInstDB)
        {
            Debug.Log("instantiating a death barrier");
            p_canInstDB = false;
            p_deathBarrier = transform.position.y - _distancetoBarrierDeath;
        }
    }
    private void ActivateKillBarrier()
    {
        if (transform.position.y <= p_deathBarrier )
        {
            Respawn();
        }
    }
    private void ViewController()
    {
        //sets rotation on the x axis
        float xRotation = Input.GetAxis("Mouse X") * _mouseXSpeed;
        m_Rotation.y += xRotation;
        c_Rotation.y += xRotation;

        //sets rotation on the y axis
        c_Rotation.x += Input.GetAxis("Mouse Y") * _mouseYSpeed * (-1); //rotates on the x axis to look up and down
        c_Rotation.x = Mathf.Clamp(c_Rotation.x, _minLookHeight, _maxLookHeight); // clamp locks range to stop neck from breaking

        //rotates view point
        transform.Rotate(0, xRotation, 0);
        camera.transform.rotation = Quaternion.Euler(c_Rotation.x, c_Rotation.y, c_Rotation.z);
        //Debug.Log(c_Rotation.x);
    }
    private void MoveController()
    {
        float translateForwardBack = Input.GetAxis("Vertical");
        float translateSidetoSide = Input.GetAxis("Horizontal");
        gameObject.transform.Translate(0, 0, translateForwardBack * _movementSpeed * Time.deltaTime);
        gameObject.transform.Translate(translateSidetoSide * _movementSpeed * Time.deltaTime, 0, 0);

        //player jumps
        if (Input.GetKeyDown("space") && p_isGrounded)
        {
            Jump();
        }
        if (p_isGrounded == false)
        {
            Debug.Log("falling");
            Fall();
            ActivateKillBarrier();
        }
    }
    public void Respawn()
    {
        Debug.Log("respawn");
        r_respawning = true;
    }

    //handles respawn animations
    IEnumerator RespawnAnimation()
    {
        Debug.Log("Starting respawntimer");
        yield return new WaitForSeconds(1);
        Debug.Log("Respawning");

        //reposition
        gameObject.transform.position = p_playerSpawn;

        //remove respawn fog
        r_fog.CrossFadeAlpha(0, .7f, false);
        r_respawning = false;
        r_respawnOnce = false; // set once to false to respawn once again
    }
}
